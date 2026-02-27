namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// State-store fallback behavior coverage for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
public sealed partial class CloudflareAwareComickGatewayTests
{
	/// <summary>
	/// Verifies non-fatal metadata-state read failures fall back to direct request behavior.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldContinueWhenStateReadFailsNonFatally()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T07:15:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		FaultInjectingMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty)
		{
			ReadException = new IOException("simulated read failure")
		};
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.state_store.failed" &&
				entry.Context is not null &&
				entry.Context.TryGetValue("operation", out string? operationValue) &&
				string.Equals(operationValue, "sticky_precheck_read", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies non-fatal sticky persistence failures do not prevent fallback routing success.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldContinueWhenStickyPersistTransformFailsNonFatally()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T07:30:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchCloudflareBlocked(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		FaultInjectingMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty)
		{
			TransformException = new IOException("simulated transform failure")
		};
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(1, flaresolverrClient.CallCount);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.state_store.failed" &&
				entry.Context is not null &&
				entry.Context.TryGetValue("operation", out string? operationValue) &&
				string.Equals(operationValue, "sticky_persist_transform", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies non-fatal sticky-clear transform failures do not interrupt direct request outcomes.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldContinueWhenStickyClearTransformFailsNonFatally()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T07:45:00+00:00");
		RecordingLogger logger = new();
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchMalformedPayload(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		FaultInjectingMetadataStateStore stateStore = new(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal),
				nowUtc.AddMinutes(-5)))
		{
			TransformException = new IOException("simulated sticky-clear failure")
		};
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc,
			logger);

		ComickDirectApiResult<ComickSearchResponse> result = await gateway.SearchAsync("title");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Equal(1, directClient.SearchCallCount);
		Assert.Equal(0, flaresolverrClient.CallCount);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "metadata.cloudflare.state_store.failed" &&
				entry.Context is not null &&
				entry.Context.TryGetValue("operation", out string? operationValue) &&
				string.Equals(operationValue, "sticky_clear_transform", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies fatal metadata-state failures are still propagated.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldRethrowFatalException_WhenStateReadThrowsFatal()
	{
		DateTimeOffset nowUtc = ParseUtcTimestamp("2026-02-24T08:00:00+00:00");
		StubComickDirectApiClient directClient = new(
			_ => CreateDirectSearchSuccess(),
			_ => CreateDirectComicSuccess());
		StubFlaresolverrClient flaresolverrClient = new(_ => CreateFlaresolverrSearchSuccess());
		FaultInjectingMetadataStateStore stateStore = new(MetadataStateSnapshot.Empty)
		{
			ReadException = new OutOfMemoryException("fatal read failure")
		};
		CloudflareAwareComickGateway gateway = CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			new Uri("http://flaresolverr.local/"),
			TimeSpan.FromMinutes(60),
			nowUtc);

		await Assert.ThrowsAsync<OutOfMemoryException>(() => gateway.SearchAsync("title"));
	}
}
