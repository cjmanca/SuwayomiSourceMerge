namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Logging coverage for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
public sealed partial class CloudflareAwareComickGatewayTests
{
	/// <summary>
	/// Verifies sticky-route requests emit debug transition diagnostics.
	/// </summary>
	[Fact]
	public async Task Execute_Expected_ShouldLogStickyRouteDebug_WhenStickyFallbackIsActive()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T02:00:00+00:00");
		DateTimeOffset stickyUntilUtc = nowUtc.AddMinutes(20);
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrComicSuccess());
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				stickyUntilUtc));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.GetComicAsync("comic-slug");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.fallback.sticky_route");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Contains("/comic/comic-slug", logEvent.Context!["endpoint"], StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies Cloudflare blocks with configured fallback emit activation warnings.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldLogFallbackActivatedWarning_WhenDirectCloudflareBlockedAndFallbackConfigured()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T01:00:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.SearchAsync("one piece");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.fallback.activated");
		Assert.Equal(LogLevel.Warning, logEvent.Level);
		Assert.Contains("/v1.0/search/", logEvent.Context!["endpoint"], StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies Cloudflare blocks without fallback emit unavailable warnings.
	/// </summary>
	[Fact]
	public async Task Execute_Failure_ShouldLogFallbackUnavailableWarning_WhenCloudflareBlockedAndFallbackMissing()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T04:00:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicCloudflareBlocked());
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient: null,
			flaresolverrServerUri: null,
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.GetComicAsync("slug");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.fallback.unavailable");
		Assert.Equal(LogLevel.Warning, logEvent.Level);
		Assert.Equal("Cloudflare challenge detected.", logEvent.Context!["diagnostic"]);
	}

	/// <summary>
	/// Verifies expired sticky clears emit debug transition diagnostics.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldLogStickyClearedDebug_WhenExpiredStickyStateIsRemoved()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:15:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchMalformedPayload(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				nowUtc.AddMinutes(-1)));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.SearchAsync("title");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.fallback.sticky_cleared");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal("MalformedPayload", logEvent.Context!["direct_outcome"]);
	}

	/// <summary>
	/// Verifies FlareSolverr raw-json payloads emit normalization diagnostics with raw mode.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldLogResponseNormalizationRawJsonMode_WhenFlaresolverrPayloadIsRawJson()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:20:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.SearchAsync("title");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.response.normalized");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal("raw_json", logEvent.Context!["normalization_mode"]);
		Assert.Equal("not_detected", logEvent.Context["html_wrapper_detection"]);
		Assert.Equal("false", logEvent.Context!["is_html_wrapped"]);
		Assert.Equal("true", logEvent.Context!["success"]);
	}

	/// <summary>
	/// Verifies FlareSolverr HTML-wrapped payloads emit normalization diagnostics with HTML extraction mode.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldLogResponseNormalizationHtmlMode_WhenFlaresolverrPayloadIsHtmlWrapped()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:25:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				System.Net.HttpStatusCode.OK,
				200,
				"<html><body><pre>[{\"hid\":\"hid-1\",\"slug\":\"slug-1\",\"title\":\"Title One\",\"statistics\":[],\"md_titles\":[{\"title\":\"Title One\"}],\"md_covers\":[{\"b2key\":\"cover.jpg\"}]}]</pre></body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.SearchAsync("title");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.response.normalized");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal("html_pre_extracted", logEvent.Context!["normalization_mode"]);
		Assert.Equal("detected", logEvent.Context["html_wrapper_detection"]);
		Assert.Equal("true", logEvent.Context!["is_html_wrapped"]);
		Assert.Equal("true", logEvent.Context!["success"]);
	}

	/// <summary>
	/// Verifies normalization failures emit failed-mode diagnostics with the failure reason.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldLogResponseNormalizationFailedMode_WhenFlaresolverrPayloadCannotBeNormalized()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:30:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				System.Net.HttpStatusCode.OK,
				200,
				"<html><body>no pre wrapper</body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.SearchAsync("title");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.response.normalized");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal("failed", logEvent.Context!["normalization_mode"]);
		Assert.Equal("not_detected", logEvent.Context["html_wrapper_detection"]);
		Assert.Equal("false", logEvent.Context["is_html_wrapped"]);
		Assert.Equal("false", logEvent.Context!["success"]);
		Assert.Contains("did not contain an HTML <pre> wrapper", logEvent.Context["diagnostic"], StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies failed normalization keeps wrapper detection as detected when pre tags exist but none are JSON-compatible.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldLogResponseNormalizationDetectedWrapper_WhenFlaresolverrPrePayloadIsNotJson()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:35:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				System.Net.HttpStatusCode.OK,
				200,
				"<html><body><pre>not json</pre></body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		_ = await gateway.SearchAsync("title");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.response.normalized");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal("failed", logEvent.Context!["normalization_mode"]);
		Assert.Equal("detected", logEvent.Context["html_wrapper_detection"]);
		Assert.Equal("true", logEvent.Context["is_html_wrapped"]);
		Assert.Equal("false", logEvent.Context["success"]);
		Assert.Contains("contained <pre> blocks", logEvent.Context["diagnostic"], StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies parser failures emit unknown wrapper-detection diagnostics.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldLogResponseNormalizationUnknownWrapper_WhenPreNodeSelectorThrows()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:40:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				System.Net.HttpStatusCode.OK,
				200,
				"<html><body><pre>[{\"hid\":\"hid-1\"}]</pre></body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger,
			preNodeSelector: static _ => throw new InvalidOperationException("Parser failure."));

		_ = await gateway.SearchAsync("title");

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.response.normalized");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal("failed", logEvent.Context!["normalization_mode"]);
		Assert.Equal("unknown", logEvent.Context["html_wrapper_detection"]);
		Assert.Equal("false", logEvent.Context["is_html_wrapped"]);
		Assert.Equal("false", logEvent.Context["success"]);
		Assert.Contains("could not be parsed: InvalidOperationException", logEvent.Context["diagnostic"], StringComparison.Ordinal);
	}
}
