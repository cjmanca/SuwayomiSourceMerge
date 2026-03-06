namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Verifies sticky retry anchor timing behavior for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
public sealed partial class CloudflareAwareComickGatewayTests
{
	/// <summary>
	/// Verifies outage-cooldown expiry is anchored to FlareSolverr-unavailable detection time.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldPersistStickyFromPostDirectBlockTimestamp()
	{
		DateTimeOffset requestStartUtc = ParseUtcTimestamp("2026-02-24T01:00:00+00:00");
		DateTimeOffset blockDetectedUtc = requestStartUtc.AddMinutes(10);
		TimeSpan directRetryInterval = TimeSpan.FromMinutes(60);
		int utcNowCallCount = 0;
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
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			directRetryInterval,
			() =>
			{
				utcNowCallCount++;
				return utcNowCallCount == 1
					? requestStartUtc
					: blockDetectedUtc;
			});

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.FlaresolverrUnavailable, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Equal(blockDetectedUtc + directRetryInterval, stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.NotEqual(requestStartUtc + directRetryInterval, stateStore.Read().StickyFlaresolverrUntilUtc);
	}

	/// <summary>
	/// Verifies cooldown state that becomes expired during request execution is cleared using post-request timestamp.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldClearStickyWhenInterleavedStickyExpiresBeforeDirectResult()
	{
		DateTimeOffset requestStartUtc = ParseUtcTimestamp("2026-02-24T03:00:00+00:00");
		DateTimeOffset postDirectUtc = requestStartUtc.AddMinutes(10);
		DateTimeOffset interleavedStickyUntilUtc = requestStartUtc.AddMinutes(2);
		int utcNowCallCount = 0;
		InMemoryMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty);
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
							interleavedStickyUntilUtc));
				return CreateFlaresolverrSearchSuccess();
			});
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			() =>
			{
				utcNowCallCount++;
				return utcNowCallCount == 1
					? requestStartUtc
					: postDirectUtc;
			});

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal(0, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Null(stateStore.Read().StickyFlaresolverrUntilUtc);
		Assert.Equal(3, stateStore.TransformCallCount);
	}
}
