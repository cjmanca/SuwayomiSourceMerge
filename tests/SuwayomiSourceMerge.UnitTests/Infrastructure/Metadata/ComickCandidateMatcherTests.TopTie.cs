namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Top-tie result semantics coverage for <see cref="ComickCandidateMatcher"/>.
/// </summary>
public sealed partial class ComickCandidateMatcherTests
{
	/// <summary>
	/// Verifies matched results report top-tie metadata when the selected candidate is part of a top-similarity tie.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Edge_ShouldSetHadTopTieTrue_WhenMatchedCandidateIsInTopSimilarityTie()
	{
		RecordingComickApiGateway gateway = new(
			slug => CreateSuccessResult(
				CreateDetailPayload(
					comicTitle: slug == "first-slug" ? "Target Title" : "Different Title")));
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "Target Title"),
				CreateSearchCandidate("second-slug", "Target Title")
			],
			["target title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.True(result.HadTopTie);
		Assert.Equal(["first-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies matched results clear top-tie metadata when the selected candidate is not part of the top-similarity tie set.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Edge_ShouldSetHadTopTieFalse_WhenMatchedCandidateIsOutsideTopSimilarityTie()
	{
		RecordingComickApiGateway gateway = new(
			slug => CreateSuccessResult(
				CreateDetailPayload(
					comicTitle: slug == "first-slug" ? "Target Title" : "Different Title")));
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "zzz"),
				CreateSearchCandidate("second-slug", "Target Title"),
				CreateSearchCandidate("third-slug", "Target Title")
			],
			["target title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(["first-slug"], gateway.RequestedSlugs);
	}
}
