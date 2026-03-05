namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using System.Text.Json;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Comick response-cache behavior coverage for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
public sealed partial class CloudflareAwareComickGatewayTests
{
	/// <summary>
	/// Verifies cache hits bypass direct calls and throttle execution.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldReturnCacheHitWithoutLiveRequestOrThrottle()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T00:00:00+00:00");
		ComickSearchResponse cachedPayload = BuildSearchPayload("cached-slug");
		ComickApiCacheEntry cacheEntry = new(
			ComickApiCacheEndpointKind.Search,
			"one piece",
			ComickDirectApiOutcome.Success,
			statusCode: 200,
			diagnostic: "cached",
			payloadJson: JsonSerializer.SerializeToElement(cachedPayload),
			expiresAtUtc: nowUtc.AddHours(1));
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				null,
				[cacheEntry]));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		TrackingMetadataApiRequestThrottle throttle = new();
		RecordingLogger logger = new();
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				404,
				upstreamResponseBody: string.Empty,
				diagnostic: "Success."));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger,
			throttle);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync(" one piece ");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Equal("cached-slug", result.Payload!.Comics[0].Slug);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(0, throttle.CallCount);
		Assert.Equal(0, stateStore.TransformCallCount);
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.hit");
	}

	/// <summary>
	/// Verifies active outage cooldown still allows cache-hit responses without live request execution.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnCacheHitDuringActiveCooldownWithoutLiveRequestOrThrottle()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T00:30:00+00:00");
		DateTimeOffset stickyUntilUtc = nowUtc.AddMinutes(45);
		ComickSearchResponse cachedPayload = BuildSearchPayload("cached-during-cooldown");
		ComickApiCacheEntry cacheEntry = new(
			ComickApiCacheEndpointKind.Search,
			"cooldown-query",
			ComickDirectApiOutcome.Success,
			statusCode: 200,
			diagnostic: "cached",
			payloadJson: JsonSerializer.SerializeToElement(cachedPayload),
			expiresAtUtc: nowUtc.AddHours(1));
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				stickyUntilUtc,
				[cacheEntry]));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		TrackingMetadataApiRequestThrottle throttle = new();
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger: null,
			throttle);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("cooldown-query");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal("cached-during-cooldown", result.Payload!.Comics[0].Slug);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Equal(0, throttle.CallCount);
		Assert.Equal(0, stateStore.TransformCallCount);
		Assert.Equal(stickyUntilUtc, stateStore.Read().StickyFlaresolverrUntilUtc);
	}

	/// <summary>
	/// Verifies cache misses execute live requests and persist cacheable outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldPersistCacheEntryAfterLiveNotFoundMiss()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T01:00:00+00:00");
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		StubComickDirectApiClient directClient = new(
			_ => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.NotFound,
				payload: null,
				statusCode: HttpStatusCode.NotFound,
				diagnostic: "not found"),
			_ => CreateDirectComicSuccess());
		TrackingMetadataApiRequestThrottle throttle = new();
		RecordingLogger logger = new();
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				404,
				upstreamResponseBody: string.Empty,
				diagnostic: "Success."));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger,
			throttle);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("query");

		Assert.Equal(ComickDirectApiOutcome.NotFound, result.Outcome);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, throttle.CallCount);
		Assert.Equal(1, stateStore.TransformCallCount);
		ComickApiCacheEntry persistedEntry = Assert.Single(stateStore.Read().ComickCache);
		Assert.Equal(ComickApiCacheEndpointKind.Search, persistedEntry.EndpointKind);
		Assert.Equal("query", persistedEntry.RequestKey);
		Assert.Equal(ComickDirectApiOutcome.NotFound, persistedEntry.Outcome);
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.miss");
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.persisted");
	}

	/// <summary>
	/// Verifies active outage cooldown returns unavailable when cache miss occurs for the requested title.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnUnavailableDuringActiveCooldown_WhenCacheMissOccurs()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T01:30:00+00:00");
		DateTimeOffset stickyUntilUtc = nowUtc.AddMinutes(45);
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				stickyUntilUtc,
				comickCache: []));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		TrackingMetadataApiRequestThrottle throttle = new();
		RecordingLogger logger = new();
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger,
			throttle);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("cooldown-miss");

		Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Equal(0, throttle.CallCount);
		Assert.Equal(0, stateStore.TransformCallCount);
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.miss");
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.cloudflare.fallback.cooldown_active");
	}

	/// <summary>
	/// Verifies expired cache entries refresh through live routing and replacement persistence.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldRefreshExpiredCacheEntryWithLiveResult()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T02:00:00+00:00");
		ComickApiCacheEntry expiredEntry = new(
			ComickApiCacheEndpointKind.Search,
			"query",
			ComickDirectApiOutcome.Success,
			statusCode: 200,
			diagnostic: "expired",
			payloadJson: JsonSerializer.SerializeToElement(BuildSearchPayload("expired-slug")),
			expiresAtUtc: nowUtc.AddMinutes(-1));
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				null,
				[expiredEntry]));
		StubComickDirectApiClient directClient = new(
			_ => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.Success,
				BuildSearchPayload("live-slug"),
				HttpStatusCode.OK,
				"live"),
			_ => CreateDirectComicSuccess());
		TrackingMetadataApiRequestThrottle throttle = new();
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger: null,
			throttle);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("query");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal("slug-1", result.Payload!.Comics[0].Slug);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, throttle.CallCount);
		ComickApiCacheEntry refreshedEntry = Assert.Single(stateStore.Read().ComickCache);
		Assert.Equal("query", refreshedEntry.RequestKey);
		Assert.Equal(ComickDirectApiOutcome.Success, refreshedEntry.Outcome);
	}

	/// <summary>
	/// Verifies non-cacheable outcomes are skipped from cache persistence.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldSkipPersistenceForNonCacheableOutcome()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T03:00:00+00:00");
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		StubComickDirectApiClient directClient = new(
			_ => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.HttpFailure,
				payload: null,
				statusCode: HttpStatusCode.ServiceUnavailable,
				diagnostic: "service unavailable"),
			_ => CreateDirectComicSuccess());
		RecordingLogger logger = new();
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient: null,
			flaresolverrServerUri: null,
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("query");

		Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		Assert.Empty(stateStore.Read().ComickCache);
		Assert.Equal(0, stateStore.TransformCallCount);
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.skipped");
	}

	/// <summary>
	/// Verifies malformed cached payloads are treated as misses and refreshed from live requests.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldTreatMalformedCachedPayloadAsMissAndRefresh()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T04:00:00+00:00");
		ComickApiCacheEntry malformedEntry = new(
			ComickApiCacheEndpointKind.Search,
			"query",
			ComickDirectApiOutcome.Success,
			statusCode: 200,
			diagnostic: "malformed",
			payloadJson: JsonSerializer.SerializeToElement("invalid payload"),
			expiresAtUtc: nowUtc.AddHours(1));
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				null,
				[malformedEntry]));
		StubComickDirectApiClient directClient = new(
			_ => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.Success,
				BuildSearchPayload("refreshed-slug"),
				HttpStatusCode.OK,
				"live"),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("query");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal("slug-1", result.Payload!.Comics[0].Slug);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Single(stateStore.Read().ComickCache);
	}

	/// <summary>
	/// Verifies cache read failures emit cache-specific state-store warning telemetry.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldLogCacheStateStoreFailed_WhenCacheReadFailsNonFatally()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T05:00:00+00:00");
		FaultInjectingMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty)
		{
			ReadException = new IOException("simulated read failure")
		};
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		RecordingLogger logger = new();
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("query");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "metadata.comick.cache.state_store_failed" &&
				entry.Context is not null &&
				entry.Context.TryGetValue("operation", out string? operationValue) &&
				string.Equals(operationValue, "cache_read", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies comic cache hits bypass direct calls and throttle execution.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Expected_ShouldReturnCacheHitWithoutLiveRequestOrThrottle()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T00:15:00+00:00");
		ComickComicResponse cachedPayload = BuildComicPayload("cached-comic-slug");
		ComickApiCacheEntry cacheEntry = new(
			ComickApiCacheEndpointKind.Comic,
			"cached-comic-slug",
			ComickDirectApiOutcome.Success,
			statusCode: 200,
			diagnostic: "cached",
			payloadJson: JsonSerializer.SerializeToElement(cachedPayload),
			expiresAtUtc: nowUtc.AddHours(1));
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				null,
				[cacheEntry]));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		TrackingMetadataApiRequestThrottle throttle = new();
		RecordingLogger logger = new();
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				404,
				upstreamResponseBody: string.Empty,
				diagnostic: "Success."));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger,
			throttle);

		ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync(" cached-comic-slug ");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Equal("cached-comic-slug", result.Payload!.Comic!.Slug);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(0, throttle.CallCount);
		Assert.Equal(0, stateStore.TransformCallCount);
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.hit");
	}

	/// <summary>
	/// Verifies comic cache misses execute live requests and persist cacheable outcomes.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Expected_ShouldPersistCacheEntryAfterLiveNotFoundMiss()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T01:15:00+00:00");
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => new ComickDirectApiResult<ComickComicResponse>(
				ComickDirectApiOutcome.NotFound,
				payload: null,
				statusCode: HttpStatusCode.NotFound,
				diagnostic: "not found"));
		TrackingMetadataApiRequestThrottle throttle = new();
		RecordingLogger logger = new();
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				404,
				upstreamResponseBody: string.Empty,
				diagnostic: "Success."));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger,
			throttle);

		ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("comic-slug");

		Assert.Equal(ComickDirectApiOutcome.NotFound, result.Outcome);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(1, throttle.CallCount);
		Assert.Equal(1, stateStore.TransformCallCount);
		ComickApiCacheEntry persistedEntry = Assert.Single(stateStore.Read().ComickCache);
		Assert.Equal(ComickApiCacheEndpointKind.Comic, persistedEntry.EndpointKind);
		Assert.Equal("comic-slug", persistedEntry.RequestKey);
		Assert.Equal(ComickDirectApiOutcome.NotFound, persistedEntry.Outcome);
		Assert.False(persistedEntry.PayloadJson.HasValue);
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.miss");
		Assert.Contains(logger.Events, static e => e.EventId == "metadata.comick.cache.persisted");
	}

	/// <summary>
	/// Verifies expired comic cache entries refresh through live routing and replacement persistence.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldRefreshExpiredCacheEntryWithLiveResult()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T02:15:00+00:00");
		ComickApiCacheEntry expiredEntry = new(
			ComickApiCacheEndpointKind.Comic,
			"comic-slug",
			ComickDirectApiOutcome.Success,
			statusCode: 200,
			diagnostic: "expired",
			payloadJson: JsonSerializer.SerializeToElement(BuildComicPayload("expired-slug")),
			expiresAtUtc: nowUtc.AddMinutes(-1));
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				null,
				[expiredEntry]));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => new ComickDirectApiResult<ComickComicResponse>(
				ComickDirectApiOutcome.Success,
				BuildComicPayload("live-slug"),
				HttpStatusCode.OK,
				"live"));
		TrackingMetadataApiRequestThrottle throttle = new();
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrComicSuccess());
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc,
			logger: null,
			throttle);

		ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("comic-slug");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal("comic-slug-1", result.Payload!.Comic!.Slug);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(1, throttle.CallCount);
		ComickApiCacheEntry refreshedEntry = Assert.Single(stateStore.Read().ComickCache);
		Assert.Equal("comic-slug", refreshedEntry.RequestKey);
		Assert.Equal(ComickDirectApiOutcome.Success, refreshedEntry.Outcome);
	}

	/// <summary>
	/// Verifies malformed cached comic payloads are treated as misses and refreshed from live requests.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldTreatMalformedCachedPayloadAsMissAndRefresh()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-03-01T04:15:00+00:00");
		ComickApiCacheEntry malformedEntry = new(
			ComickApiCacheEndpointKind.Comic,
			"comic-slug",
			ComickDirectApiOutcome.Success,
			statusCode: 200,
			diagnostic: "malformed",
			payloadJson: JsonSerializer.SerializeToElement("invalid payload"),
			expiresAtUtc: nowUtc.AddHours(1));
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				null,
				[malformedEntry]));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => new ComickDirectApiResult<ComickComicResponse>(
				ComickDirectApiOutcome.Success,
				BuildComicPayload("refreshed-slug"),
				HttpStatusCode.OK,
				"live"));
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrComicSuccess());
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri: new Uri("http://flaresolverr.local/"),
			directRetryInterval: TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("comic-slug");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal("comic-slug-1", result.Payload!.Comic!.Slug);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Single(stateStore.Read().ComickCache);
	}

	/// <summary>
	/// Creates one minimal deterministic comic payload.
	/// </summary>
	/// <param name="slug">Slug value.</param>
	/// <returns>Comic payload.</returns>
	private static ComickComicResponse BuildComicPayload(string slug)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);
		return new ComickComicResponse
		{
			Comic = new ComickComicDetails
			{
				Hid = $"hid-{slug}",
				Slug = slug,
				Title = "Title",
				Links = new ComickComicLinks(),
				Statistics = [new ComickStatistic()],
				Recommendations = [],
				RelateFrom = [],
				MdTitles = [new ComickTitleAlias { Title = "Title" }],
				MdCovers = [new ComickCover { B2Key = "cover.jpg" }],
				GenreMappings = []
			}
		};
	}

	/// <summary>
	/// Creates one minimal deterministic search payload.
	/// </summary>
	/// <param name="slug">Slug value.</param>
	/// <returns>Search payload.</returns>
	private static ComickSearchResponse BuildSearchPayload(string slug)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);
		return new ComickSearchResponse(
		[
			new ComickSearchComic
			{
				Slug = slug,
				Title = "Title"
			}
		]);
	}
}
