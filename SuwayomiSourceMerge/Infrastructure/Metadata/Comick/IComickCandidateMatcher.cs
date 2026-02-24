namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Selects one best Comick comic candidate from a candidate set using normalized expected-title keys.
/// </summary>
internal interface IComickCandidateMatcher
{
	/// <summary>
	/// Attempts to select one best candidate from the provided candidate list.
	/// </summary>
	/// <param name="candidates">Candidate comic payloads to evaluate.</param>
	/// <param name="expectedTitles">Expected raw title values used to build normalized match keys.</param>
	/// <returns>Deterministic candidate-match result.</returns>
	ComickCandidateMatchResult Match(
		IReadOnlyList<ComickComicResponse> candidates,
		IReadOnlyList<string> expectedTitles);
}
