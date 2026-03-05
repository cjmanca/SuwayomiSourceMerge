namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using System.Text.Json;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
public sealed partial class CloudflareAwareComickGatewayTests
{
	/// <summary>
	/// Verifies FlareSolverr success paths are used for both endpoints with no direct Comick probing.
	/// </summary>
	[Theory]
	[InlineData("search")]
	[InlineData("comic")]
	public async Task Execute_Expected_ShouldReturnDirectSuccessWithoutFlaresolverrAsync(string endpoint)
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T00:00:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => endpoint == "search"
				? CreateFlaresolverrSearchSuccess()
				: CreateFlaresolverrComicSuccess());
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		if (endpoint == "search")
		{
			ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("one piece");
			Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
			Assert.NotNull(result.Payload);
		}
		else
		{
			ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("one-piece");
			Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
			Assert.NotNull(result.Payload);
		}

		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Null(stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(1, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies healthy FlareSolverr requests do not activate outage cooldown state.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldFallbackToFlaresolverrAndPersistStickyUntil()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T01:00:00+00:00");
		TimeSpan directRetryInterval = TimeSpan.FromMinutes(60);
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
			directRetryInterval,
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Null(stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Contains("request.get", flaresolverrClient.LastPayload, StringComparison.Ordinal);
		Assert.Contains("api.comick.dev", flaresolverrClient.LastPayload, StringComparison.Ordinal);
		Assert.Contains("limit=4", flaresolverrClient.LastPayload, StringComparison.Ordinal);
		Assert.Contains("tachiyomi=true", flaresolverrClient.LastPayload, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies active outage cooldown short-circuits request execution with an unavailable outcome.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldUseStickyFlaresolverrRouteWithoutDirectProbe()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T02:00:00+00:00");
		DateTimeOffset stickyUntilUtc = nowUtc.AddMinutes(20);
		StubComickDirectApiClient directClient = new(
			_ => throw new InvalidOperationException("Direct search should not be invoked in sticky mode."),
			_ => throw new InvalidOperationException("Direct comic should not be invoked in sticky mode."));
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
			nowUtc);

		ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("comic-slug");

		Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Equal(stickyUntilUtc, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(0, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies expired cooldown state is cleared after a successful FlareSolverr request.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldProbeDirectAfterStickyExpiryAndClearStickyOnSuccess()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:00:00+00:00");
		DateTimeOffset stickyUntilUtc = nowUtc.AddMinutes(-1);
		Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal)
		{
			["preserved-title"] = nowUtc.AddHours(12)
		};
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				cooldowns,
				stickyUntilUtc));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		MetadataStateSnapshot updatedState = stateStore.Read();
		Assert.Null(updatedState.StickyFlaresolverrUntilUtc);
		Assert.Equal(cooldowns["preserved-title"], updatedState.TitleCooldownsUtc["preserved-title"]);
		Assert.Equal(2, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies expired cooldown state clears after successful FlareSolverr responses.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldClearExpiredSticky_WhenDirectOutcomeIsMalformedPayload()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:15:00+00:00");
		DateTimeOffset stickyUntilUtc = nowUtc.AddMinutes(-1);
		Dictionary<string, DateTimeOffset> cooldowns = new(StringComparer.Ordinal)
		{
			["cooldown-title"] = nowUtc.AddHours(12)
		};
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchMalformedPayload(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				cooldowns,
				stickyUntilUtc));
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		MetadataStateSnapshot updatedState = stateStore.Read();
		Assert.Null(updatedState.StickyFlaresolverrUntilUtc);
		Assert.Equal(cooldowns["cooldown-title"], updatedState.TitleCooldownsUtc["cooldown-title"]);
		Assert.Equal(2, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies stale clear attempts do not remove a newer cooldown value that appears during request execution.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldNotClearNewerSticky_WhenDirectHandlerUpdatesStickyToFuture()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T03:30:00+00:00");
		DateTimeOffset initialExpiredStickyUtc = nowUtc.AddMinutes(-1);
		DateTimeOffset interleavedFutureStickyUtc = nowUtc.AddMinutes(30);
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				initialExpiredStickyUtc));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ =>
			{
				stateStore.Transform(
					current =>
						new MetadataStateSnapshot(
							current.TitleCooldownsUtc,
							interleavedFutureStickyUtc));
				return CreateFlaresolverrSearchSuccess();
			});
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Equal(interleavedFutureStickyUtc, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(2, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies missing FlareSolverr configuration returns explicit unavailable outcomes.
	/// </summary>
	[Theory]
	[InlineData("search")]
	[InlineData("comic")]
	public async Task Execute_Failure_ShouldReturnDirectCloudflareBlockedWhenFlaresolverrNotConfiguredAsync(string endpoint)
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T04:00:00+00:00");
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
			nowUtc);

		if (endpoint == "search")
		{
			ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");
			Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		}
		else
		{
			ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("slug");
			Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		}

		Assert.Equal(0, stateStore.TransformCallCount);
		Assert.Null(stateStore.Read().StickyFlaresolverrUntilUtc);
	}

	/// <summary>
	/// Verifies sticky-mode FlareSolverr transport failures return unavailable outcomes and keep cooldown state unchanged.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldFailClosedAndRetainSticky_WhenStickyFlaresolverrRequestFails()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T05:00:00+00:00");
		DateTimeOffset stickyUntilUtc = nowUtc.AddMinutes(20);
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.TransportFailure,
				statusCode: null,
				upstreamStatusCode: null,
				upstreamResponseBody: null,
				diagnostic: "socket failure"));
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
			nowUtc);

		ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("slug");

		Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Equal(stickyUntilUtc, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(0, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies invalid upstream payloads returned by FlareSolverr map to malformed-payload outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenFlaresolverrUpstreamPayloadInvalid()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:00:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"""{"not":"a valid search payload"}""",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(HttpStatusCode.OK, result.StatusCode);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies non-success upstream status mapping remains HttpFailure even when body cannot be normalized.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnHttpFailure_WhenFlaresolverrUpstreamStatusIs500WithNonJsonBody()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:02:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				500,
				"<html><body>server error</body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.HttpFailure, result.Outcome);
		Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
		Assert.Equal("HTTP failure status code: 500.", result.Diagnostic);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies Cloudflare challenge status/body is classified as CloudflareBlocked before normalization.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnCloudflareBlocked_WhenFlaresolverrUpstreamStatusIs403WithChallengeBody()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:03:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				403,
				"<html><head><title>Just a moment</title></head><body>challenge</body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.CloudflareBlocked, result.Outcome);
		Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
		Assert.Equal("Cloudflare challenge detected.", result.Diagnostic);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies non-challenge 403 responses map to HttpFailure before normalization.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnHttpFailure_WhenFlaresolverrUpstreamStatusIs403WithoutChallengeMarkers()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:04:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				403,
				"<html><body>access denied</body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.HttpFailure, result.Outcome);
		Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
		Assert.Equal("HTTP failure status code: 403.", result.Diagnostic);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies HTML-wrapped search payloads extracted from <c>&lt;pre&gt;</c> succeed through FlareSolverr fallback.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldSucceed_WhenFlaresolverrUpstreamPayloadIsHtmlWrappedPreJson()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:05:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"""
				<html>
				  <body>
				    <pre>[{"hid":"hid-1","slug":"slug-1","title":"Title One","statistics":[],"md_titles":[{"title":"Title One"}],"md_covers":[{"b2key":"cover.jpg"}]}]</pre>
				  </body>
				</html>
				""",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload.Comics);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies HTML-wrapped comic payloads with uppercase pre tags succeed after extraction and decode.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Expected_ShouldSucceed_WhenFlaresolverrUpstreamPayloadUsesUppercasePreAndHtmlEntities()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:07:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicCloudflareBlocked());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"""
				<html><body><PRE class="json">
				  {&quot;comic&quot;:{&quot;hid&quot;:&quot;comic-hid-1&quot;,&quot;slug&quot;:&quot;comic-slug-1&quot;,&quot;title&quot;:&quot;Comic One&quot;,&quot;links&quot;:{},&quot;statistics&quot;:[],&quot;recommendations&quot;:[],&quot;relate_from&quot;:[],&quot;md_titles&quot;:[{&quot;title&quot;:&quot;Comic One&quot;}],&quot;md_covers&quot;:[{&quot;b2key&quot;:&quot;cover.jpg&quot;}],&quot;md_comic_md_genres&quot;:[]}}
				</PRE></body></html>
				""",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("slug");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.NotNull(result.Payload.Comic);
		Assert.Equal("comic-slug-1", result.Payload.Comic.Slug);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies FlareSolverr fallback payloads with null search entries map to malformed outcomes without throw leakage.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenFlaresolverrUpstreamSearchItemIsNull()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:10:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"""[null]""",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies non-JSON HTML payloads without a pre wrapper map to malformed outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenFlaresolverrHtmlPayloadHasNoPreWrapper()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:15:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"<html><body>not json</body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Equal(
			"FlareSolverr upstream response was not JSON and did not contain an HTML <pre> wrapper.",
			result.Diagnostic);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies raw JSON payloads prefixed with UTF BOM are normalized before parsing.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldSucceed_WhenFlaresolverrPayloadIsRawJsonWithBom()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:18:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"\uFEFF[{\"hid\":\"hid-1\",\"slug\":\"slug-1\",\"title\":\"Title One\",\"statistics\":[],\"md_titles\":[{\"title\":\"Title One\"}],\"md_covers\":[{\"b2key\":\"cover.jpg\"}]}]",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload.Comics);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies extraction scans multiple html pre wrappers and uses the first JSON-root-compatible payload.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldSucceed_WhenFirstFlaresolverrPrePayloadIsNotJsonButLaterPrePayloadIsJson()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:19:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"""
				<html><body>
				<pre>not json</pre>
				<pre>[{"hid":"hid-1","slug":"slug-1","title":"Title One","statistics":[],"md_titles":[{"title":"Title One"}],"md_covers":[{"b2key":"cover.jpg"}]}]</pre>
				</body></html>
				""",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload.Comics);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies non-<c>pre</c> tags with a <c>pre</c> prefix do not block extraction of later valid <c>&lt;pre&gt;</c> JSON.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldSucceed_WhenHtmlContainsPreloadTagBeforeValidPrePayload()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:19:30+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"""
				<html><body>
				<preload>cache</preload>
				<pre>[{"hid":"hid-1","slug":"slug-1","title":"Title One","statistics":[],"md_titles":[{"title":"Title One"}],"md_covers":[{"b2key":"cover.jpg"}]}]</pre>
				</body></html>
				""",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload.Comics);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies html pre wrappers that do not contain JSON map to malformed outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenFlaresolverrPrePayloadIsNotJson()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:20:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"<html><body><pre>this is not json</pre></body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Equal(
			"FlareSolverr HTML-wrapped response contained <pre> blocks but none began with a JSON root token.",
			result.Diagnostic);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies malformed html wrappers with missing pre close markers return malformed outcomes without throw leakage.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenFlaresolverrPrePayloadIsMissingClosingTag()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:22:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"<html><body><pre>[{\"hid\":\"hid-1\"}</body></html>",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.NotNull(result.Diagnostic);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies non-fatal parser exceptions are mapped to malformed outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenPreNodeSelectorThrowsNonFatalException()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:23:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
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
			preNodeSelector: static _ => throw new InvalidOperationException("Parser failure."));

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Contains("could not be parsed: InvalidOperationException", result.Diagnostic, StringComparison.Ordinal);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies fatal parser exceptions are rethrown and not normalized into malformed outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldRethrow_WhenPreNodeSelectorThrowsFatalException()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:24:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
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
			preNodeSelector: static _ => throw new OutOfMemoryException("Fatal parser failure."));

		await Assert.ThrowsAsync<OutOfMemoryException>(
			async () => await gateway.SearchAsync("title"));
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies empty FlareSolverr upstream payloads map to malformed outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenFlaresolverrUpstreamPayloadIsEmpty()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:25:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Success,
				HttpStatusCode.OK,
				200,
				"",
				"Success."));
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Equal("FlareSolverr upstream response body was empty.", result.Diagnostic);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies outage-cooldown persistence is monotonic and never shortens an existing later expiry.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldKeepLaterSticky_WhenFallbackComputesEarlierSticky()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T06:30:00+00:00");
		DateTimeOffset interleavedLaterStickyUtc = nowUtc.AddMinutes(90);
		InMemoryMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				stickyFlaresolverrUntilUtc: null));
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(
			_ =>
			{
				stateStore.Transform(
					current =>
						new MetadataStateSnapshot(
							current.TitleCooldownsUtc,
							interleavedLaterStickyUtc));
				return new FlaresolverrApiResult(
					FlaresolverrApiOutcome.TransportFailure,
					statusCode: null,
					upstreamStatusCode: null,
					upstreamResponseBody: null,
					diagnostic: "socket failure");
			});
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Equal(interleavedLaterStickyUtc, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(2, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies FlareSolverr fallback preserves endpoint routing for both search and comic requests.
	/// </summary>
	[Theory]
	[InlineData("search")]
	[InlineData("comic")]
	public async Task Execute_Edge_ShouldFallbackThroughFlaresolverrForBothEndpointsAsync(string endpoint)
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T07:00:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicCloudflareBlocked());
		StubFlaresolverrClient flaresolverrClient = new(
			_ => endpoint == "search"
				? CreateFlaresolverrSearchSuccess()
				: CreateFlaresolverrComicSuccess());
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		if (endpoint == "search")
		{
			ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");
			Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
			Assert.NotNull(result.Payload);
		}
		else
		{
			ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("slug");
			Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
			Assert.NotNull(result.Payload);
		}

		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.NotNull(flaresolverrClient.LastPayload);
		using JsonDocument payloadDocument = JsonDocument.Parse(flaresolverrClient.LastPayload!);
		Assert.Equal("request.get", payloadDocument.RootElement.GetProperty("cmd").GetString());
		string? url = payloadDocument.RootElement.GetProperty("url").GetString();
		Assert.NotNull(url);
		Assert.StartsWith("https://api.comick.dev/", url, StringComparison.Ordinal);
		if (endpoint == "search")
		{
			Assert.Contains("limit=4", url, StringComparison.Ordinal);
		}
		Assert.Contains("tachiyomi=true", url, StringComparison.Ordinal);
	}

}
