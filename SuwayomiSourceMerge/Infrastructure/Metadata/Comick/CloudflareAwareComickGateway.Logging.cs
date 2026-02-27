using System.Globalization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Logging helpers for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway
{
	/// <summary>
	/// Event id emitted when sticky mode routes directly to FlareSolverr.
	/// </summary>
	private const string StickyRouteEvent = "metadata.cloudflare.fallback.sticky_route";

	/// <summary>
	/// Event id emitted when Cloudflare block activates sticky FlareSolverr fallback.
	/// </summary>
	private const string FallbackActivatedEvent = "metadata.cloudflare.fallback.activated";

	/// <summary>
	/// Event id emitted when Cloudflare block is observed but FlareSolverr is unavailable.
	/// </summary>
	private const string FallbackUnavailableEvent = "metadata.cloudflare.fallback.unavailable";

	/// <summary>
	/// Event id emitted when expired sticky routing state is cleared.
	/// </summary>
	private const string StickyClearedEvent = "metadata.cloudflare.fallback.sticky_cleared";

	/// <summary>
	/// Event id emitted when metadata state-store operations fail and fallback behavior is applied.
	/// </summary>
	private const string StateStoreFailedEvent = "metadata.cloudflare.state_store.failed";

	/// <summary>
	/// Logs sticky-route diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="stickyUntilUtc">Sticky expiry timestamp.</param>
	private void LogStickyRoute(Uri endpointUri, DateTimeOffset? stickyUntilUtc)
	{
		_logger.Debug(
			StickyRouteEvent,
			"Using sticky FlareSolverr route for Comick request.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("sticky_until_utc", stickyUntilUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))));
	}

	/// <summary>
	/// Logs fallback activation diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="stickyUntilUtc">Sticky expiry timestamp.</param>
	private void LogFallbackActivated(Uri endpointUri, DateTimeOffset stickyUntilUtc)
	{
		_logger.Warning(
			FallbackActivatedEvent,
			"Cloudflare block detected; activating sticky FlareSolverr fallback routing.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("sticky_until_utc", stickyUntilUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
				("direct_retry_minutes", _options.FlaresolverrDirectRetryInterval.TotalMinutes.ToString(CultureInfo.InvariantCulture))));
	}

	/// <summary>
	/// Logs fallback-unavailable diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="diagnostic">Direct-request diagnostic.</param>
	private void LogFallbackUnavailable(Uri endpointUri, string diagnostic)
	{
		_logger.Warning(
			FallbackUnavailableEvent,
			"Cloudflare block detected but FlareSolverr fallback is not configured.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("diagnostic", diagnostic)));
	}

	/// <summary>
	/// Logs sticky-clear diagnostics.
	/// </summary>
	/// <param name="endpointUri">Endpoint URI.</param>
	/// <param name="directOutcome">Direct request outcome.</param>
	/// <param name="nowUtc">Current UTC timestamp.</param>
	private void LogStickyCleared(Uri endpointUri, ComickDirectApiOutcome directOutcome, DateTimeOffset nowUtc)
	{
		_logger.Debug(
			StickyClearedEvent,
			"Cleared expired sticky FlareSolverr fallback routing state.",
			BuildContext(
				("endpoint", endpointUri.AbsoluteUri),
				("direct_outcome", directOutcome.ToString()),
				("timestamp_utc", nowUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))));
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
