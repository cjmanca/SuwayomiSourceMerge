using System.Net;
using System.Text.Json;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Executes Comick API requests with direct-first routing and sticky FlareSolverr fallback for Cloudflare challenge responses.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway : IComickApiGateway
{
	/// <summary>
	/// Canonical default Comick API base URI.
	/// </summary>
	private static readonly Uri _defaultComickBaseUri = NormalizeBaseUri(
		new Uri(ComickDirectApiClientOptions.DefaultBaseUri, UriKind.Absolute));

	/// <summary>
	/// Direct Comick API client dependency.
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
	/// Base URI used for Comick endpoint request composition.
	/// </summary>
	private readonly Uri _comickBaseUri;

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
	/// Initializes a new instance of the <see cref="CloudflareAwareComickGateway"/> class.
	/// </summary>
	/// <param name="directClient">Direct Comick API client dependency.</param>
	/// <param name="flaresolverrClient">Optional FlareSolverr client dependency.</param>
	/// <param name="metadataStateStore">Persisted metadata state store.</param>
	/// <param name="options">Metadata orchestration settings.</param>
	/// <param name="logger">Optional logger dependency.</param>
	public CloudflareAwareComickGateway(
		IComickDirectApiClient directClient,
		IFlaresolverrClient? flaresolverrClient,
		IMetadataStateStore metadataStateStore,
		MetadataOrchestrationOptions options,
		ISsmLogger? logger = null)
		: this(
			directClient,
			flaresolverrClient,
			metadataStateStore,
			options,
			_defaultComickBaseUri,
			static () => DateTimeOffset.UtcNow,
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
	/// <param name="comickBaseUri">Comick API base URI used for gateway-routed FlareSolverr requests.</param>
	/// <param name="utcNowProvider">
	/// Clock provider used for sticky-window decisions.
	/// Returned values are normalized to UTC by the gateway before use.
	/// </param>
	/// <param name="logger">Optional logger dependency.</param>
	internal CloudflareAwareComickGateway(
		IComickDirectApiClient directClient,
		IFlaresolverrClient? flaresolverrClient,
		IMetadataStateStore metadataStateStore,
		MetadataOrchestrationOptions options,
		Uri comickBaseUri,
		Func<DateTimeOffset> utcNowProvider,
		ISsmLogger? logger = null)
	{
		_directClient = directClient ?? throw new ArgumentNullException(nameof(directClient));
		_flaresolverrClient = flaresolverrClient;
		_metadataStateStore = metadataStateStore ?? throw new ArgumentNullException(nameof(metadataStateStore));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
		_comickBaseUri = NormalizeBaseUri(comickBaseUri);
		_logger = logger ?? NoOpSsmLogger.Instance;
	}

	/// <inheritdoc />
	public Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
		string query,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);

		Uri endpointUri = ComickEndpointUriBuilder.BuildSearchUri(_comickBaseUri, query);
		return ExecuteWithRoutingAsync(
			() => _directClient.SearchAsync(query, cancellationToken),
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

		Uri endpointUri = ComickEndpointUriBuilder.BuildComicUri(_comickBaseUri, slug);
		return ExecuteWithRoutingAsync(
			() => _directClient.GetComicAsync(slug, cancellationToken),
			endpointUri,
			ComickPayloadParser.TryParseComicPayload,
			cancellationToken);
	}

	/// <summary>
	/// Executes one typed request with direct-first and sticky-FlareSolverr routing semantics.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="directRequest">Direct request callback.</param>
	/// <param name="endpointUri">Absolute endpoint URI.</param>
	/// <param name="payloadParser">Typed payload parser for successful response bodies.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Typed Comick request result.</returns>
	private async Task<ComickDirectApiResult<TPayload>> ExecuteWithRoutingAsync<TPayload>(
		Func<Task<ComickDirectApiResult<TPayload>>> directRequest,
		Uri endpointUri,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(directRequest);
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentNullException.ThrowIfNull(payloadParser);

		DateTimeOffset nowUtc = _utcNowProvider().ToUniversalTime();
		MetadataStateSnapshot currentState = _metadataStateStore.Read();
		bool flaresolverrConfigured = IsFlaresolverrConfigured();
		if (IsStickyActive(currentState, nowUtc) && flaresolverrConfigured)
		{
			LogStickyRoute(endpointUri, currentState.StickyFlaresolverrUntilUtc);
			return await ExecuteViaFlaresolverrAsync(endpointUri, payloadParser, cancellationToken).ConfigureAwait(false);
		}

		ComickDirectApiResult<TPayload> directResult = await directRequest().ConfigureAwait(false);
		DateTimeOffset postDirectNowUtc = _utcNowProvider().ToUniversalTime();
		if (directResult.Outcome == ComickDirectApiOutcome.CloudflareBlocked && flaresolverrConfigured)
		{
			DateTimeOffset stickyUntilUtc = postDirectNowUtc + _options.FlaresolverrDirectRetryInterval;
			LogFallbackActivated(endpointUri, stickyUntilUtc);
			PersistStickyFlaresolverrUntil(stickyUntilUtc);
			return await ExecuteViaFlaresolverrAsync(endpointUri, payloadParser, cancellationToken).ConfigureAwait(false);
		}
		else if (directResult.Outcome == ComickDirectApiOutcome.CloudflareBlocked)
		{
			LogFallbackUnavailable(endpointUri, directResult.Diagnostic);
		}

		TryClearExpiredStickyAfterDirectNonCloudflare(postDirectNowUtc, directResult, endpointUri);

		return directResult;
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
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.TransportFailure,
				payload: default,
				statusCode: null,
				diagnostic: "FlareSolverr client is not configured.");
		}

		string requestPayloadJson = BuildFlaresolverrRequestPayload(endpointUri);
		FlaresolverrApiResult flaresolverrResult = await _flaresolverrClient
			.PostV1Async(requestPayloadJson, cancellationToken)
			.ConfigureAwait(false);
		return MapFlaresolverrResult(flaresolverrResult, payloadParser);
	}

	/// <summary>
	/// Maps one FlareSolverr wrapper result into a Comick result contract.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="flaresolverrResult">FlareSolverr wrapper result.</param>
	/// <param name="payloadParser">Typed payload parser.</param>
	/// <returns>Mapped Comick result.</returns>
	private static ComickDirectApiResult<TPayload> MapFlaresolverrResult<TPayload>(
		FlaresolverrApiResult flaresolverrResult,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser)
	{
		ArgumentNullException.ThrowIfNull(flaresolverrResult);
		ArgumentNullException.ThrowIfNull(payloadParser);

		switch (flaresolverrResult.Outcome)
		{
			case FlaresolverrApiOutcome.Success:
				return MapFlaresolverrSuccess(flaresolverrResult, payloadParser);
			case FlaresolverrApiOutcome.HttpFailure:
				return new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.HttpFailure,
					payload: default,
					statusCode: flaresolverrResult.StatusCode,
					diagnostic: $"FlareSolverr HTTP failure: {flaresolverrResult.Diagnostic}");
			case FlaresolverrApiOutcome.TransportFailure:
				return new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.TransportFailure,
					payload: default,
					statusCode: null,
					diagnostic: $"FlareSolverr transport failure: {flaresolverrResult.Diagnostic}");
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
	private static ComickDirectApiResult<TPayload> MapFlaresolverrSuccess<TPayload>(
		FlaresolverrApiResult flaresolverrResult,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser)
	{
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

		string responseBody = flaresolverrResult.UpstreamResponseBody ?? string.Empty;
		if (upstreamStatusCode == HttpStatusCode.NotFound)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.NotFound,
				payload: default,
				upstreamStatusCode,
				diagnostic: "Resource not found.");
		}

		if (ComickPayloadParser.IsCloudflareCandidateStatus(upstreamStatusCode.Value) &&
			ComickPayloadParser.IsCloudflareChallenge(responseBody))
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
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="nowUtc">Current timestamp.</param>
	/// <param name="directResult">Direct request result.</param>
	private void TryClearExpiredStickyAfterDirectNonCloudflare<TPayload>(
		DateTimeOffset nowUtc,
		ComickDirectApiResult<TPayload> directResult,
		Uri endpointUri)
	{
		ArgumentNullException.ThrowIfNull(directResult);
		ArgumentNullException.ThrowIfNull(endpointUri);
		if (directResult.Outcome == ComickDirectApiOutcome.CloudflareBlocked)
		{
			return;
		}

		MetadataStateSnapshot snapshot = _metadataStateStore.Read();
		if (snapshot.StickyFlaresolverrUntilUtc is not DateTimeOffset currentStickyUntilUtc ||
			currentStickyUntilUtc > nowUtc)
		{
			return;
		}

		// Pre-read avoids transform/persist overhead in the common no-op case.
		// Transform rechecks the same condition against current state to preserve concurrency safety.
		bool stickyCleared = false;
		_metadataStateStore.Transform(
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
					null);
			});
		// Logging intentionally occurs after transform completion so state-store mutation stays side-effect free
		// and logging does not run under store-internal synchronization.
		if (stickyCleared)
		{
			LogStickyCleared(endpointUri, directResult.Outcome, nowUtc);
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
		_metadataStateStore.Transform(
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
					nextStickyUntilUtc);
			});
	}

	/// <summary>
	/// Normalizes one Comick API base URI to an absolute http/https URI with exactly one trailing slash.
	/// </summary>
	/// <param name="baseUri">Base URI to normalize.</param>
	/// <returns>Normalized base URI.</returns>
	/// <exception cref="ArgumentException">Thrown when URI is not absolute or does not use http/https.</exception>
	private static Uri NormalizeBaseUri(Uri baseUri)
	{
		ArgumentNullException.ThrowIfNull(baseUri);
		if (!baseUri.IsAbsoluteUri)
		{
			throw new ArgumentException("Comick API base URI must be absolute.", nameof(baseUri));
		}

		if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
			!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
		{
			throw new ArgumentException("Comick API base URI must use http or https.", nameof(baseUri));
		}

		return new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/", UriKind.Absolute);
	}
}
