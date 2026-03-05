using System.Globalization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Logging helpers for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway
{
	/// <summary>
	/// Event id emitted when FlareSolverr is unavailable and outage cooldown is activated.
	/// </summary>
	private const string FallbackUnavailableEvent = "metadata.cloudflare.fallback.unavailable";

	/// <summary>
	/// Event id emitted when FlareSolverr outage cooldown is active and request execution is skipped.
	/// </summary>
	private const string CooldownActiveEvent = "metadata.cloudflare.fallback.cooldown_active";

	/// <summary>
	/// Event id emitted when expired FlareSolverr outage cooldown state is cleared.
	/// </summary>
	private const string CooldownClearedEvent = "metadata.cloudflare.fallback.cooldown_cleared";

	/// <summary>
	/// Event id emitted when metadata state-store operations fail and fallback behavior is applied.
	/// </summary>
	private const string StateStoreFailedEvent = "metadata.cloudflare.state_store.failed";

	/// <summary>
	/// Event id emitted when Comick cache returns a hit.
	/// </summary>
	private const string CacheHitEvent = "metadata.comick.cache.hit";

	/// <summary>
	/// Event id emitted when Comick cache lookup misses.
	/// </summary>
	private const string CacheMissEvent = "metadata.comick.cache.miss";

	/// <summary>
	/// Event id emitted when one Comick cache entry is persisted.
	/// </summary>
	private const string CachePersistedEvent = "metadata.comick.cache.persisted";

	/// <summary>
	/// Event id emitted when cache persistence is skipped.
	/// </summary>
	private const string CacheSkippedEvent = "metadata.comick.cache.skipped";

	/// <summary>
	/// Event id emitted when cache-specific state-store operations fail.
	/// </summary>
	private const string CacheStateStoreFailedEvent = "metadata.comick.cache.state_store_failed";

	/// <summary>
	/// Event id emitted when FlareSolverr upstream response normalization is evaluated.
	/// </summary>
	private const string ResponseNormalizedEvent = "metadata.cloudflare.response.normalized";

	/// <summary>
	/// Logs FlareSolverr-unavailable activation diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="stickyUntilUtc">Sticky expiry timestamp.</param>
	/// <param name="diagnostic">Unavailable diagnostic.</param>
	private void LogFlaresolverrUnavailable(Uri endpointUri, DateTimeOffset stickyUntilUtc, string diagnostic)
	{
		_logger.Warning(
			FallbackUnavailableEvent,
			"FlareSolverr unavailable; activating outage cooldown for Comick requests.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("sticky_until_utc", stickyUntilUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
				("direct_retry_minutes", _options.FlaresolverrDirectRetryInterval.TotalMinutes.ToString(CultureInfo.InvariantCulture)),
				("diagnostic", diagnostic)));
	}

	/// <summary>
	/// Logs cooldown-active skip diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="stickyUntilUtc">Cooldown expiry timestamp.</param>
	private void LogCooldownActiveSkip(Uri endpointUri, DateTimeOffset? stickyUntilUtc)
	{
		_logger.Debug(
			CooldownActiveEvent,
			"Skipping Comick request while FlareSolverr outage cooldown is active.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("sticky_until_utc", stickyUntilUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))));
	}

	/// <summary>
	/// Logs cooldown-clear diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="nowUtc">Current UTC timestamp.</param>
	private void LogCooldownCleared(Uri endpointUri, DateTimeOffset nowUtc)
	{
		_logger.Debug(
			CooldownClearedEvent,
			"Cleared expired FlareSolverr outage cooldown state.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("timestamp_utc", nowUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))));
	}

	/// <summary>
	/// Logs FlareSolverr upstream response-normalization diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="upstreamStatusCode">Upstream status code from FlareSolverr wrapper.</param>
	/// <param name="normalizationResult">Normalization result.</param>
	private void LogResponseNormalization(
		Uri endpointUri,
		int? upstreamStatusCode,
		ResponseNormalizationResult normalizationResult)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		_logger.Debug(
			ResponseNormalizedEvent,
			"Normalized FlareSolverr upstream response for Comick parsing.",
				BuildContext(
					("endpoint", endpointUri.AbsoluteUri),
					("upstream_status", upstreamStatusCode?.ToString(CultureInfo.InvariantCulture)),
					("normalization_mode", NormalizeResponseMode(normalizationResult.Mode)),
					("html_wrapper_detection", NormalizeHtmlWrapperDetection(normalizationResult.HtmlWrapperDetection)),
					("is_html_wrapped", normalizationResult.HtmlWrapperDetection == HtmlWrapperDetectionState.Detected ? "true" : "false"),
					("success", normalizationResult.Success ? "true" : "false"),
					("diagnostic", normalizationResult.Diagnostic),
					("response_prefix", normalizationResult.ResponsePrefix)));
	}

	/// <summary>
	/// Converts one response-normalization mode into the canonical diagnostics token.
	/// </summary>
	/// <param name="mode">Normalization mode.</param>
	/// <returns>Canonical diagnostics token.</returns>
	private static string NormalizeResponseMode(ResponseNormalizationMode mode)
	{
		return mode switch
		{
			ResponseNormalizationMode.RawJson => "raw_json",
			ResponseNormalizationMode.HtmlPreExtracted => "html_pre_extracted",
			_ => "failed"
		};
	}

	/// <summary>
	/// Converts one HTML-wrapper detection state into the canonical diagnostics token.
	/// </summary>
	/// <param name="state">HTML-wrapper detection state.</param>
	/// <returns>Canonical diagnostics token.</returns>
	private static string NormalizeHtmlWrapperDetection(HtmlWrapperDetectionState state)
	{
		return state switch
		{
			HtmlWrapperDetectionState.Detected => "detected",
			HtmlWrapperDetectionState.Unknown => "unknown",
			_ => "not_detected"
		};
	}

	/// <summary>
	/// Logs Comick response-cache hit diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="endpointKind">Endpoint kind.</param>
	/// <param name="requestKey">Trimmed request key.</param>
	/// <param name="outcome">Cached outcome.</param>
	/// <param name="detail">Cache-hit detail token.</param>
	private void LogCacheHit(
		Uri endpointUri,
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		ComickDirectApiOutcome outcome,
		string detail)
	{
		_logger.Debug(
			CacheHitEvent,
			"Comick response-cache hit.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("endpoint_kind", endpointKind.ToString().ToLowerInvariant()),
				("request_key", requestKey),
				("outcome", outcome.ToString()),
				("detail", detail)));
	}

	/// <summary>
	/// Logs Comick response-cache miss diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="endpointKind">Endpoint kind.</param>
	/// <param name="requestKey">Trimmed request key.</param>
	/// <param name="detail">Cache-miss detail token.</param>
	private void LogCacheMiss(
		Uri endpointUri,
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		string detail)
	{
		_logger.Debug(
			CacheMissEvent,
			"Comick response-cache miss.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("endpoint_kind", endpointKind.ToString().ToLowerInvariant()),
				("request_key", requestKey),
				("detail", detail)));
	}

	/// <summary>
	/// Logs Comick response-cache persistence diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="endpointKind">Endpoint kind.</param>
	/// <param name="requestKey">Trimmed request key.</param>
	/// <param name="outcome">Persisted outcome.</param>
	/// <param name="expiresAtUtc">Cache expiry timestamp.</param>
	private void LogCachePersisted(
		Uri endpointUri,
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		ComickDirectApiOutcome outcome,
		DateTimeOffset expiresAtUtc)
	{
		_logger.Debug(
			CachePersistedEvent,
			"Comick response-cache entry persisted.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("endpoint_kind", endpointKind.ToString().ToLowerInvariant()),
				("request_key", requestKey),
				("outcome", outcome.ToString()),
				("expires_at_utc", expiresAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))));
	}

	/// <summary>
	/// Logs Comick response-cache skip diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="endpointKind">Endpoint kind.</param>
	/// <param name="requestKey">Trimmed request key.</param>
	/// <param name="outcome">Observed live outcome.</param>
	/// <param name="reason">Skip reason token.</param>
	private void LogCacheSkipped(
		Uri endpointUri,
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		ComickDirectApiOutcome outcome,
		string reason)
	{
		_logger.Debug(
			CacheSkippedEvent,
			"Comick response-cache persistence skipped.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("endpoint_kind", endpointKind.ToString().ToLowerInvariant()),
				("request_key", requestKey),
				("outcome", outcome.ToString()),
				("reason", reason)));
	}

	/// <summary>
	/// Logs cache-specific metadata state-store operation failures.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI associated with the failed operation.</param>
	/// <param name="operation">Operation identifier.</param>
	/// <param name="exception">Observed non-fatal exception.</param>
	private void LogCacheStateStoreOperationFailed(Uri endpointUri, string operation, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(operation);
		ArgumentNullException.ThrowIfNull(exception);

		_logger.Warning(
			CacheStateStoreFailedEvent,
			"Comick cache state-store operation failed; continuing with best-effort fallback behavior.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("operation", operation),
				("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
				("message", exception.Message)));
	}

	/// <summary>
	/// Logs metadata state-store operation failure diagnostics when best-effort fallback behavior is applied.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI associated with the failed state operation.</param>
	/// <param name="operation">Operation identifier.</param>
	/// <param name="exception">Observed non-fatal exception.</param>
	private void LogStateStoreOperationFailed(Uri endpointUri, string operation, Exception exception)
	{
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentException.ThrowIfNullOrWhiteSpace(operation);
		ArgumentNullException.ThrowIfNull(exception);

		_logger.Warning(
			StateStoreFailedEvent,
			"Metadata state-store operation failed; continuing with best-effort fallback behavior.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("operation", operation),
				("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
				("message", exception.Message)));
	}

	/// <summary>
	/// Builds one structured logging context dictionary from non-empty values.
	/// </summary>
	/// <param name="pairs">Context key/value pairs.</param>
	/// <returns>Structured context dictionary.</returns>
	private static IReadOnlyDictionary<string, string> BuildContext(params (string Key, string? Value)[] pairs)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		for (int index = 0; index < pairs.Length; index++)
		{
			(string key, string? value) = pairs[index];
			if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			context[key] = value;
		}

		return context;
	}
}
