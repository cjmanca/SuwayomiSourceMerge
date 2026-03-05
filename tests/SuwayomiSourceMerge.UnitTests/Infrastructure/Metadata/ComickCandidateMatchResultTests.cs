namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies constructor behavior for <see cref="ComickCandidateMatchResult"/>.
/// </summary>
public sealed class ComickCandidateMatchResultTests
{
	/// <summary>
	/// Verifies required-lookup-failure metadata is accepted for no-match outcomes.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldSetRequiredLookupFailure_WhenNoMatch()
	{
		ComickCandidateMatchResult result = new(
			ComickCandidateMatchOutcome.NoHighConfidenceMatch,
			matchedCandidate: null,
			ComickCandidateMatchResult.NoMatchCandidateIndex,
			hadTopTie: false,
			matchScore: 0,
			hadServiceInterruption: false,
			hadFlaresolverrUnavailable: false,
			hadRequiredLookupFailure: true);

		Assert.True(result.HadRequiredLookupFailure);
		Assert.False(result.HadServiceInterruption);
	}

	/// <summary>
	/// Verifies required-lookup-failure metadata defaults to false for matched outcomes.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldDefaultRequiredLookupFailureFalse_WhenMatched()
	{
		ComickComicResponse matchedCandidate = new()
		{
			Comic = new ComickComicDetails
			{
				Title = "Canonical Title"
			}
		};
		ComickCandidateMatchResult result = new(
			ComickCandidateMatchOutcome.Matched,
			matchedCandidate,
			matchedCandidateIndex: 0,
			hadTopTie: false,
			matchScore: 2);

		Assert.False(result.HadRequiredLookupFailure);
		Assert.Same(matchedCandidate, result.MatchedCandidate);
	}

	/// <summary>
	/// Verifies matched outcomes reject required-lookup-failure metadata.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenRequiredLookupFailureIsSetForMatchedOutcome()
	{
		Assert.Throws<ArgumentException>(
			() => new ComickCandidateMatchResult(
				ComickCandidateMatchOutcome.Matched,
				new ComickComicResponse(),
				matchedCandidateIndex: 0,
				hadTopTie: false,
				matchScore: 2,
				hadServiceInterruption: false,
				hadFlaresolverrUnavailable: false,
				hadRequiredLookupFailure: true));
	}
}
