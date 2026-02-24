namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Classifies one Comick candidate-match attempt outcome.
/// </summary>
internal enum ComickCandidateMatchOutcome
{
	/// <summary>
	/// One high-confidence candidate was selected.
	/// </summary>
	Matched,

	/// <summary>
	/// No high-confidence candidate was found.
	/// </summary>
	NoHighConfidenceMatch
}
