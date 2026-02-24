namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Selects one best Comick comic candidate from a candidate set using normalized expected-title keys.
/// </summary>
internal interface IComickCandidateMatcher
{
	/// <summary>
	/// Attempts to select one best candidate by resolving search candidates to comic-detail payloads.
	/// </summary>
	/// <param name="candidates">Search candidates to evaluate and resolve by slug.</param>
	/// <param name="expectedTitles">Expected raw title values used to build normalized match keys.</param>
	/// <param name="cancellationToken">Cancellation token for detail requests.</param>
	/// <returns>Deterministic candidate-match result.</returns>
	Task<ComickCandidateMatchResult> MatchAsync(
		IReadOnlyList<ComickSearchComic> candidates,
		IReadOnlyList<string> expectedTitles,
		CancellationToken cancellationToken = default);
}
