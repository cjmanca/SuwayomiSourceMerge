using System.Net;
using System.Text.Json;

using HtmlAgilityPack;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Executes Comick API requests using FlareSolverr-only routing with outage-cooldown short-circuit behavior.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway : IComickApiGateway
{
	/// <summary>
	/// Direct Comick API client dependency retained for constructor compatibility.
	/// Live gateway routing is FlareSolverr-only and never calls this client.
	/// </summary>
	private readonly IComickDirectApiClient _directClient;

	/// <summary>
	/// Optional FlareSolverr client dependency.
	/// </summary>
	private readonly IFlaresolverrClient? _flaresolverrClient;

	/// <summary>
	/// Persisted metadata state storage.
	/// </summary>
	private readonly IMetadataStateStore _metadataStateStore;

	/// <summary>
	/// Metadata orchestration settings.
	/// </summary>
	private readonly MetadataOrchestrationOptions _options;

	/// <summary>
	/// Throttle applied to live Comick API and FlareSolverr requests.
	/// </summary>
	private readonly IMetadataApiRequestThrottle _throttle;

	/// <summary>
	/// Clock provider used for sticky-window decisions.
	/// Gateway call sites normalize provider values using <see cref="DateTimeOffset.ToUniversalTime"/> before comparisons.
	/// </summary>
	private readonly Func<DateTimeOffset> _utcNowProvider;

	/// <summary>
	/// Logger dependency.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// HTML selector used to find candidate preformatted nodes in FlareSolverr upstream payloads.
	/// </summary>
	private readonly Func<string, HtmlNodeCollection?> _preNodeSelector;

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudflareAwareComickGateway"/> class.
	/// </summary>
	/// <param name="directClient">Direct Comick API client dependency.</param>
	/// <param name="flaresolverrClient">Optional FlareSolverr client dependency.</param>
	/// <param name="metadataStateStore">Persisted metadata state store.</param>
	/// <param name="options">Metadata orchestration settings.</param>
	/// <param name="throttle">Throttle applied to live API requests.</param>
	/// <param name="logger">Optional logger dependency.</param>
	public CloudflareAwareComickGateway(
		IComickDirectApiClient directClient,
		IFlaresolverrClient? flaresolverrClient,
		IMetadataStateStore metadataStateStore,
		MetadataOrchestrationOptions options,
		IMetadataApiRequestThrottle throttle,
		ISsmLogger? logger = null)
		: this(
			directClient,
			flaresolverrClient,
			metadataStateStore,
			options,
			static () => DateTimeOffset.UtcNow,
			throttle,
			logger)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="CloudflareAwareComickGateway"/> class.
	/// </summary>
	/// <param name="directClient">Direct Comick API client dependency.</param>
	/// <param name="flaresolverrClient">Optional FlareSolverr client dependency.</param>
	/// <param name="metadataStateStore">Persisted metadata state store.</param>
	/// <param name="options">Metadata orchestration settings.</param>
	/// <param name="utcNowProvider">
	/// Clock provider used for sticky-window decisions.
	/// Returned values are normalized to UTC by the gateway before use.
	/// </param>
	/// <param name="throttle">Throttle applied to live API requests.</param>
	/// <param name="logger">Optional logger dependency.</param>
	internal CloudflareAwareComickGateway(
		IComickDirectApiClient directClient,
		IFlaresolverrClient? flaresolverrClient,
		IMetadataStateStore metadataStateStore,
		MetadataOrchestrationOptions options,
		Func<DateTimeOffset> utcNowProvider,
		IMetadataApiRequestThrottle throttle,
		ISsmLogger? logger = null,
		Func<string, HtmlNodeCollection?>? preNodeSelector = null)
	{
		_directClient = directClient ?? throw new ArgumentNullException(nameof(directClient));
		_flaresolverrClient = flaresolverrClient;
		_metadataStateStore = metadataStateStore ?? throw new ArgumentNullException(nameof(metadataStateStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
		_throttle = throttle ?? throw new ArgumentNullException(nameof(throttle));
		_logger = logger ?? NoOpSsmLogger.Instance;
		_preNodeSelector = preNodeSelector ?? SelectPreNodesFromHtml;
	}

	/// <inheritdoc />
	public Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
		string query,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);
		string requestKey = query.Trim();

		Uri endpointUri = ComickEndpointUriBuilder.BuildSearchUri(
			_options.ComickApiBaseUri,
			_options.ComickSearchEndpointPath,
			requestKey,
			_options.ComickSearchMaxResults);
		return ExecuteWithCacheAsync(
			ComickApiCacheEndpointKind.Search,
			requestKey,
			endpointUri,
			ComickPayloadParser.TryParseSearchPayload,
			cancellationToken);
	}

	/// <inheritdoc />
	public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
		string slug,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);
		string requestKey = slug.Trim();

		Uri endpointUri = ComickEndpointUriBuilder.BuildComicUri(
			_options.ComickApiBaseUri,
			_options.ComickComicEndpointPath,
			requestKey);
		return ExecuteWithCacheAsync(
			ComickApiCacheEndpointKind.Comic,
			requestKey,
			endpointUri,
			ComickPayloadParser.TryParseComicPayload,
			cancellationToken);
	}

	/// <summary>
	/// Executes one typed request using FlareSolverr-only routing with persisted outage-cooldown behavior.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="endpointUri">Absolute endpoint URI.</param>
	/// <param name="payloadParser">Typed payload parser for successful response bodies.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Typed Comick request result.</returns>
	private async Task<ComickDirectApiResult<TPayload>> ExecuteWithRoutingAsync<TPayload>(
		Uri endpointUri,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentNullException.ThrowIfNull(payloadParser);
		_ = _directClient;

		DateTimeOffset nowUtc = _utcNowProvider().ToUniversalTime();
		if (!IsFlaresolverrConfigured())
		{
			return CreateFlaresolverrUnavailableResult<TPayload>(
				"FlareSolverr routing is not configured.");
		}

		MetadataStateSnapshot currentState = TryReadMetadataStateSnapshot(
			endpointUri,
			operation: "flaresolverr_cooldown_precheck_read",
			operationKind: MetadataStateStoreOperationKind.Standard);
		if (IsStickyActive(currentState, nowUtc))
		{
			LogCooldownActiveSkip(endpointUri, currentState.StickyFlaresolverrUntilUtc);
			return CreateFlaresolverrUnavailableResult<TPayload>(
				"FlareSolverr outage cooldown is active.");
		}

		ComickDirectApiResult<TPayload> flaresolverrResult = await ExecuteViaFlaresolverrAsync(
			endpointUri,
			payloadParser,
			cancellationToken).ConfigureAwait(false);
		DateTimeOffset postRequestUtc = _utcNowProvider().ToUniversalTime();
		if (flaresolverrResult.Outcome == ComickDirectApiOutcome.FlaresolverrUnavailable)
		{
			DateTimeOffset stickyUntilUtc = postRequestUtc + _options.FlaresolverrDirectRetryInterval;
			LogFlaresolverrUnavailable(endpointUri, stickyUntilUtc, flaresolverrResult.Diagnostic);
			PersistStickyFlaresolverrUntil(stickyUntilUtc);
			return flaresolverrResult;
		}

		TryClearExpiredStickyAfterFlaresolverrResult(postRequestUtc, endpointUri);
		return flaresolverrResult;
	}

	/// <summary>
	/// Executes one request through FlareSolverr and maps the wrapper result into a Comick outcome.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="endpointUri">Absolute Comick endpoint URI.</param>
	/// <param name="payloadParser">Typed payload parser for upstream successful response bodies.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Mapped Comick request result.</returns>
	private async Task<ComickDirectApiResult<TPayload>> ExecuteViaFlaresolverrAsync<TPayload>(
		Uri endpointUri,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentNullException.ThrowIfNull(payloadParser);
		if (_flaresolverrClient is null)
		{
			return CreateFlaresolverrUnavailableResult<TPayload>(
				"FlareSolverr client dependency is not configured.");
		}

		string requestPayloadJson = BuildFlaresolverrRequestPayload(endpointUri);
		FlaresolverrApiResult flaresolverrResult = await _throttle
			.ExecuteAsync(ct => _flaresolverrClient.PostV1Async(requestPayloadJson, ct), cancellationToken)
			.ConfigureAwait(false);
		return MapFlaresolverrResult(endpointUri, flaresolverrResult, payloadParser);
	}

	/// <summary>
	/// Maps one FlareSolverr wrapper result into a Comick result contract.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="flaresolverrResult">FlareSolverr wrapper result.</param>
	/// <param name="payloadParser">Typed payload parser.</param>
	/// <returns>Mapped Comick result.</returns>
	private ComickDirectApiResult<TPayload> MapFlaresolverrResult<TPayload>(
		Uri endpointUri,
		FlaresolverrApiResult flaresolverrResult,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentNullException.ThrowIfNull(flaresolverrResult);
		ArgumentNullException.ThrowIfNull(payloadParser);

		switch (flaresolverrResult.Outcome)
		{
			case FlaresolverrApiOutcome.Success:
				return MapFlaresolverrSuccess(endpointUri, flaresolverrResult, payloadParser);
			case FlaresolverrApiOutcome.HttpFailure:
				return CreateFlaresolverrUnavailableResult<TPayload>(
					$"FlareSolverr HTTP failure: {flaresolverrResult.Diagnostic}");
			case FlaresolverrApiOutcome.TransportFailure:
				return CreateFlaresolverrUnavailableResult<TPayload>(
					$"FlareSolverr transport failure: {flaresolverrResult.Diagnostic}");
			case FlaresolverrApiOutcome.Cancelled:
				return new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.Cancelled,
					payload: default,
					statusCode: null,
					diagnostic: "Request canceled by caller.");
			case FlaresolverrApiOutcome.MalformedPayload:
			default:
				return new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.MalformedPayload,
					payload: default,
					statusCode: flaresolverrResult.StatusCode,
					diagnostic: $"FlareSolverr malformed payload: {flaresolverrResult.Diagnostic}");
		}
	}

	/// <summary>
	/// Maps a successful FlareSolverr wrapper extraction into typed Comick outcomes.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="flaresolverrResult">FlareSolverr wrapper result.</param>
	/// <param name="payloadParser">Typed payload parser.</param>
	/// <returns>Mapped Comick result.</returns>
	private ComickDirectApiResult<TPayload> MapFlaresolverrSuccess<TPayload>(
		Uri endpointUri,
		FlaresolverrApiResult flaresolverrResult,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		if (flaresolverrResult.UpstreamStatusCode is null)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.MalformedPayload,
				payload: default,
				statusCode: flaresolverrResult.StatusCode,
				diagnostic: "FlareSolverr success wrapper did not include solution.status.");
		}

		HttpStatusCode? upstreamStatusCode = TryConvertToHttpStatusCode(flaresolverrResult.UpstreamStatusCode.Value);
		if (upstreamStatusCode is null)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.HttpFailure,
				payload: default,
				statusCode: null,
				diagnostic: $"Upstream status code '{flaresolverrResult.UpstreamStatusCode.Value}' is out of HTTP range.");
		}

		if (upstreamStatusCode == HttpStatusCode.NotFound)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.NotFound,
				payload: default,
				upstreamStatusCode,
				diagnostic: "Resource not found.");
		}

		string rawResponseBody = flaresolverrResult.UpstreamResponseBody ?? string.Empty;
		if (ComickPayloadParser.IsCloudflareCandidateStatus(upstreamStatusCode.Value) &&
			ComickPayloadParser.IsCloudflareChallenge(rawResponseBody))
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.CloudflareBlocked,
				payload: default,
				upstreamStatusCode,
				diagnostic: "Cloudflare challenge detected.");
		}

		if ((int)upstreamStatusCode < 200 || (int)upstreamStatusCode >= 300)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.HttpFailure,
				payload: default,
				upstreamStatusCode,
				diagnostic: $"HTTP failure status code: {(int)upstreamStatusCode.Value}.");
		}

		ResponseNormalizationResult normalizationResult = NormalizeFlaresolverrUpstreamResponse(
			flaresolverrResult.UpstreamResponseBody);
		LogResponseNormalization(endpointUri, flaresolverrResult.UpstreamStatusCode, normalizationResult);
		if (!normalizationResult.Success || normalizationResult.NormalizedBody is null)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.MalformedPayload,
				payload: default,
				statusCode: upstreamStatusCode,
				diagnostic: normalizationResult.Diagnostic);
		}

		string responseBody = normalizationResult.NormalizedBody;

		(bool parseSuccess, TPayload? payload, string diagnostic) = payloadParser(responseBody);
		return parseSuccess
			? new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.Success,
				payload,
				upstreamStatusCode,
				"Success.")
			: new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.MalformedPayload,
				payload: default,
				upstreamStatusCode,
				diagnostic);
	}

	/// <summary>
	/// Converts one integer status code into an HTTP status code when valid.
	/// </summary>
	/// <param name="statusCode">Integer status code value.</param>
	/// <returns>Converted status code when valid; otherwise <see langword="null"/>.</returns>
	private static HttpStatusCode? TryConvertToHttpStatusCode(int statusCode)
	{
		return statusCode is >= 100 and <= 599
			? (HttpStatusCode)statusCode
			: null;
	}

	/// <summary>
	/// Builds one FlareSolverr request payload for HTTP GET execution.
	/// </summary>
	/// <param name="endpointUri">Absolute Comick endpoint URI.</param>
	/// <returns>JSON payload text.</returns>
	private static string BuildFlaresolverrRequestPayload(Uri endpointUri)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);

		return JsonSerializer.Serialize(
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["cmd"] = "request.get",
				["url"] = endpointUri.AbsoluteUri
			});
	}

	/// <summary>
	/// Determines whether sticky FlareSolverr mode is currently active.
	/// </summary>
	/// <param name="state">Persisted metadata state.</param>
	/// <param name="nowUtc">Current timestamp.</param>
	/// <returns><see langword="true"/> when sticky mode is active; otherwise <see langword="false"/>.</returns>
	private static bool IsStickyActive(MetadataStateSnapshot state, DateTimeOffset nowUtc)
	{
		ArgumentNullException.ThrowIfNull(state);
		return state.StickyFlaresolverrUntilUtc is DateTimeOffset stickyUntilUtc
			&& stickyUntilUtc > nowUtc;
	}

	/// <summary>
	/// Attempts to clear sticky FlareSolverr state after a non-cloudflare direct outcome when current sticky state is expired.
	/// </summary>
	/// <param name="nowUtc">Current timestamp.</param>
	/// <param name="endpointUri">Endpoint URI used for diagnostics.</param>
	private void TryClearExpiredStickyAfterFlaresolverrResult(
		DateTimeOffset nowUtc,
		Uri endpointUri)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		MetadataStateSnapshot snapshot = TryReadMetadataStateSnapshot(
			endpointUri,
			operation: "flaresolverr_cooldown_clear_read",
			operationKind: MetadataStateStoreOperationKind.Standard);
		if (snapshot.StickyFlaresolverrUntilUtc is not DateTimeOffset currentStickyUntilUtc ||
			currentStickyUntilUtc > nowUtc)
		{
			return;
		}

		// Pre-read avoids transform/persist overhead in the common no-op case.
		// Transform rechecks the same condition against current state to preserve concurrency safety.
		bool stickyCleared = false;
		if (!TryTransformMetadataStateSnapshot(
			endpointUri,
			operation: "flaresolverr_cooldown_clear_transform",
			operationKind: MetadataStateStoreOperationKind.Standard,
			current =>
			{
				if (current.StickyFlaresolverrUntilUtc is not DateTimeOffset stickyUntilUtc)
				{
					return current;
				}

				if (stickyUntilUtc > nowUtc)
				{
					return current;
				}

				stickyCleared = true;
				return new MetadataStateSnapshot(
					current.TitleCooldownsUtc,
					null,
					current.ComickCache);
			}))
		{
			return;
		}

		// Logging intentionally occurs after transform completion so state-store mutation stays side-effect free
		// and logging does not run under store-internal synchronization.
		if (stickyCleared)
		{
			LogCooldownCleared(endpointUri, nowUtc);
		}
	}

	/// <summary>
	/// Determines whether FlareSolverr fallback is configured and available.
	/// </summary>
	/// <returns><see langword="true"/> when fallback routing is available; otherwise <see langword="false"/>.</returns>
	private bool IsFlaresolverrConfigured()
	{
		return _flaresolverrClient is not null && _options.FlaresolverrServerUri is not null;
	}

	/// <summary>
	/// Persists sticky FlareSolverr routing expiry.
	/// </summary>
	/// <param name="stickyUntilUtc">Sticky routing expiry timestamp.</param>
	private void PersistStickyFlaresolverrUntil(DateTimeOffset stickyUntilUtc)
	{
		DateTimeOffset normalizedStickyUntilUtc = stickyUntilUtc.ToUniversalTime();
		_ = TryTransformMetadataStateSnapshot(
			_options.ComickApiBaseUri,
			operation: "flaresolverr_cooldown_persist_transform",
			operationKind: MetadataStateStoreOperationKind.Standard,
			current =>
			{
				DateTimeOffset? currentStickyUntilUtc = current.StickyFlaresolverrUntilUtc;
				DateTimeOffset nextStickyUntilUtc = currentStickyUntilUtc is DateTimeOffset existingStickyUntilUtc &&
					existingStickyUntilUtc > normalizedStickyUntilUtc
					? existingStickyUntilUtc
					: normalizedStickyUntilUtc;
				if (currentStickyUntilUtc == nextStickyUntilUtc)
				{
					return current;
				}

				return new MetadataStateSnapshot(
					current.TitleCooldownsUtc,
					nextStickyUntilUtc,
					current.ComickCache);
			});
	}

	/// <summary>
	/// Creates one deterministic FlareSolverr-unavailable result payload.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="diagnostic">Diagnostic text.</param>
	/// <returns>Unavailable result.</returns>
	private static ComickDirectApiResult<TPayload> CreateFlaresolverrUnavailableResult<TPayload>(string diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
		return new ComickDirectApiResult<TPayload>(
			ComickDirectApiOutcome.FlaresolverrUnavailable,
			payload: default,
			statusCode: null,
			diagnostic: diagnostic.Trim());
	}

}
