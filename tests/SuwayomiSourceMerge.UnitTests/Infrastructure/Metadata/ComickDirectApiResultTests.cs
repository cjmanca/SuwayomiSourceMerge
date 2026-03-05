namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;

using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies constructor behavior for <see cref="ComickDirectApiResult{TPayload}"/>.
/// </summary>
public sealed class ComickDirectApiResultTests
{
	/// <summary>
	/// Verifies valid cache-only miss metadata is accepted and exposed.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldSetCacheOnlyMissTrue_WhenConfigured()
	{
		ComickDirectApiResult<ComickSearchResponse> result = new(
			ComickDirectApiOutcome.NotFound,
			payload: null,
			statusCode: HttpStatusCode.NotFound,
			diagnostic: "cache-only miss",
			isCacheOnlyMiss: true);

		Assert.Equal(ComickDirectApiOutcome.NotFound, result.Outcome);
		Assert.True(result.IsCacheOnlyMiss);
	}

	/// <summary>
	/// Verifies cache-only miss metadata defaults to disabled for standard results.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldDefaultCacheOnlyMissFalse_WhenNotProvided()
	{
		ComickSearchResponse payload = new([]);
		ComickDirectApiResult<ComickSearchResponse> result = new(
			ComickDirectApiOutcome.Success,
			payload,
			statusCode: HttpStatusCode.OK,
			diagnostic: "success");

		Assert.False(result.IsCacheOnlyMiss);
		Assert.Same(payload, result.Payload);
	}

	/// <summary>
	/// Verifies cache-only miss metadata rejects non-NotFound outcomes.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenCacheOnlyMissIsUsedWithNonNotFoundOutcome()
	{
		Assert.Throws<ArgumentException>(
			() => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.Success,
				payload: new ComickSearchResponse([]),
				statusCode: HttpStatusCode.OK,
				diagnostic: "invalid",
				isCacheOnlyMiss: true));
	}

	/// <summary>
	/// Verifies cache-only miss metadata rejects non-null payload values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenCacheOnlyMissPayloadIsNotNull()
	{
		Assert.Throws<ArgumentException>(
			() => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.NotFound,
				payload: new ComickSearchResponse([]),
				statusCode: HttpStatusCode.NotFound,
				diagnostic: "invalid",
				isCacheOnlyMiss: true));
	}
}
