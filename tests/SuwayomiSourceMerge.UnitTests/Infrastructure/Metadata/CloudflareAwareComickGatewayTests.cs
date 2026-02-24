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
	/// Verifies direct success short-circuits FlareSolverr and preserves sticky state when none is present.
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
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
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

		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Null(stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(0, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies direct Cloudflare-blocked search falls back to FlareSolverr and enables sticky mode.
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
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Equal(nowUtc + directRetryInterval, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Contains("request.get", flaresolverrClient.LastPayload, StringComparison.Ordinal);
		Assert.Contains("api.comick.dev", flaresolverrClient.LastPayload, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies sticky-active mode routes directly to FlareSolverr without direct probing.
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

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Equal(stickyUntilUtc, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(0, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies sticky-expired mode probes direct and clears sticky after non-blocked success.
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
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		MetadataStateSnapshot updatedState = stateStore.Read();
		Assert.Null(updatedState.StickyFlaresolverrUntilUtc);
		Assert.Equal(cooldowns["preserved-title"], updatedState.TitleCooldownsUtc["preserved-title"]);
		Assert.Equal(1, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies expired sticky clears for non-cloudflare malformed direct outcomes.
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

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		MetadataStateSnapshot updatedState = stateStore.Read();
		Assert.Null(updatedState.StickyFlaresolverrUntilUtc);
		Assert.Equal(cooldowns["cooldown-title"], updatedState.TitleCooldownsUtc["cooldown-title"]);
		Assert.Equal(1, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies stale clear attempts do not remove a newer sticky value that appears during direct request execution.
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
			_ =>
			{
				stateStore.Transform(
					current =>
						new MetadataStateSnapshot(
							current.TitleCooldownsUtc,
							interleavedFutureStickyUtc));
				return CreateDirectSearchMalformedPayload();
			},
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Equal(interleavedFutureStickyUtc, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(1, stateStore.TransformCallCount);
	}

	/// <summary>
	/// Verifies Cloudflare-blocked direct results return unchanged when FlareSolverr is not configured.
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
			Assert.Equal(ComickDirectApiOutcome.CloudflareBlocked, result.Outcome);
		}
		else
		{
			ComickDirectApiResult<ComickComicResponse> result = await gateway.GetComicAsync("slug");
			Assert.Equal(ComickDirectApiOutcome.CloudflareBlocked, result.Outcome);
		}

		Assert.Equal(0, stateStore.TransformCallCount);
		Assert.Null(stateStore.Read().StickyFlaresolverrUntilUtc);
	}

	/// <summary>
	/// Verifies sticky-mode FlareSolverr failures return failure results and keep sticky state unchanged.
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

		Assert.Equal(ComickDirectApiOutcome.TransportFailure, result.Outcome);
		Assert.Equal(0, directClient.ComicCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
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
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
	}

	/// <summary>
	/// Verifies sticky updates are monotonic and never shorten an existing later sticky expiry.
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
			_ =>
			{
				stateStore.Transform(
					current =>
						new MetadataStateSnapshot(
							current.TitleCooldownsUtc,
							interleavedLaterStickyUtc));
				return CreateDirectSearchCloudflareBlocked();
			},
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal(1, directClient.SearchCallCount);
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
	}
}
