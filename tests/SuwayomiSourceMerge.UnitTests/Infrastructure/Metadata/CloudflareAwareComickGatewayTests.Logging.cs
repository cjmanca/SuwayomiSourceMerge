namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Logging coverage for <see cref="CloudflareAwareComickGatewayTests"/>.
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
}
