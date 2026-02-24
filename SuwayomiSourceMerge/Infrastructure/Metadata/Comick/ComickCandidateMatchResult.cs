namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents one deterministic Comick candidate-match selection result.
/// </summary>
internal sealed class ComickCandidateMatchResult
{
	/// <summary>
	/// Sentinel index used when no candidate is selected.
	/// </summary>
	public const int NoMatchCandidateIndex = -1;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickCandidateMatchResult"/> class.
	/// </summary>
	/// <param name="outcome">Match outcome classification.</param>
	/// <param name="matchedCandidate">Selected candidate when matched; otherwise <see langword="null"/>.</param>
	/// <param name="matchedCandidateIndex">Selected candidate index when matched; otherwise <see cref="NoMatchCandidateIndex"/>.</param>
	/// <param name="hadTopTie">Whether one or more candidates tied at the selected top score.</param>
	/// <param name="matchScore">Top match score used for selection.</param>
	public ComickCandidateMatchResult(
		ComickCandidateMatchOutcome outcome,
		ComickComicResponse? matchedCandidate,
		int matchedCandidateIndex,
		bool hadTopTie,
		int matchScore)
	{
		if (matchScore < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(matchScore), matchScore, "Match score must be >= 0.");
		}

		if (outcome == ComickCandidateMatchOutcome.Matched)
		{
			if (matchedCandidate is null)
			{
				throw new ArgumentException(
					"Matched candidate is required when outcome is Matched.",
					nameof(matchedCandidate));
			}

			if (matchedCandidateIndex < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(matchedCandidateIndex),
					matchedCandidateIndex,
					"Matched candidate index must be >= 0 when outcome is Matched.");
			}

			if (matchScore == 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(matchScore),
					matchScore,
					"Match score must be > 0 when outcome is Matched.");
			}
		}
		else
		{
			if (matchedCandidate is not null)
			{
				throw new ArgumentException(
					"Matched candidate must be null when no match is selected.",
					nameof(matchedCandidate));
			}

			if (matchedCandidateIndex != NoMatchCandidateIndex)
			{
				throw new ArgumentOutOfRangeException(
					nameof(matchedCandidateIndex),
					matchedCandidateIndex,
					$"Matched candidate index must be {NoMatchCandidateIndex} when no match is selected.");
			}

			if (hadTopTie)
			{
				throw new ArgumentException(
					"Top-tie flag must be false when no match is selected.",
					nameof(hadTopTie));
			}

			if (matchScore != 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(matchScore),
					matchScore,
					"Match score must be 0 when no match is selected.");
			}
		}

		Outcome = outcome;
		MatchedCandidate = matchedCandidate;
		MatchedCandidateIndex = matchedCandidateIndex;
		HadTopTie = hadTopTie;
		MatchScore = matchScore;
	}

	/// <summary>
	/// Gets match outcome classification.
	/// </summary>
	public ComickCandidateMatchOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets selected candidate when matched; otherwise <see langword="null"/>.
	/// </summary>
	public ComickComicResponse? MatchedCandidate
	{
		get;
	}

	/// <summary>
	/// Gets selected candidate index when matched; otherwise <see cref="NoMatchCandidateIndex"/>.
	/// </summary>
	public int MatchedCandidateIndex
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether one or more candidates tied at the selected top score.
	/// </summary>
	public bool HadTopTie
	{
		get;
	}

	/// <summary>
	/// Gets the selected top score.
	/// </summary>
	public int MatchScore
	{
		get;
	}
}
