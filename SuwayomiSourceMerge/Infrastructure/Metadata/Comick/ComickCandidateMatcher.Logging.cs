using System.Globalization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Candidate-ambiguity logging helpers for <see cref="ComickCandidateMatcher"/>.
/// </summary>
internal sealed partial class ComickCandidateMatcher
{
	/// <summary>
	/// Event id emitted when ranking hints indicate an ambiguous top-candidate tie.
	/// </summary>
	private const string CandidateAmbiguityEvent = "metadata.candidate.ambiguity";

	/// <summary>
	/// Equality tolerance used for floating-point similarity tie checks.
	/// The ranking helper computes normalized Levenshtein scores with repeated double arithmetic, so tie comparisons
	/// use a small fixed tolerance to avoid platform/runtime rounding jitter.
	/// </summary>
	private const double SimilarityTieEpsilon = 1e-9d;

	/// <summary>
	/// Computes top-similarity tie information from ranking hints.
	/// </summary>
	/// <param name="candidates">Candidate collection.</param>
	/// <param name="expectedTitleKeys">Expected normalized title keys.</param>
	/// <returns>Tuple containing tie state, top similarity, and tied candidate count.</returns>
	private (bool HasTopSimilarityTie, double TopSimilarity, int TiedCandidateCount) GetTopSimilarityTieInfo(
		IReadOnlyList<ComickSearchComic> candidates,
		IReadOnlySet<string> expectedTitleKeys)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		ArgumentNullException.ThrowIfNull(expectedTitleKeys);

		if (candidates.Count < 2)
		{
			return (false, 0d, 0);
		}

		double topSimilarity = double.NegativeInfinity;
		int topSimilarityCount = 0;
		for (int index = 0; index < candidates.Count; index++)
		{
			ComickSearchComic candidate = candidates[index];
			double similarity = ComputeSearchCandidateOrderingSimilarity(candidate, expectedTitleKeys);
			if (similarity > topSimilarity + SimilarityTieEpsilon)
			{
				topSimilarity = similarity;
				topSimilarityCount = 1;
			}
			else if (Math.Abs(similarity - topSimilarity) <= SimilarityTieEpsilon)
			{
				topSimilarityCount++;
			}
		}

		bool hasTie = topSimilarityCount > 1 && topSimilarity > 0d;
		return (hasTie, hasTie ? topSimilarity : 0d, hasTie ? topSimilarityCount : 0);
	}

	/// <summary>
	/// Emits one candidate-ambiguity warning event.
	/// </summary>
	/// <param name="candidateCount">Candidate count.</param>
	/// <param name="expectedTitleCount">Expected-title count.</param>
	/// <param name="topSimilarity">Top ranking-hint similarity.</param>
	/// <param name="tiedCandidateCount">Number of candidates tied at top similarity.</param>
	private void LogCandidateAmbiguity(
		int candidateCount,
		int expectedTitleCount,
		double topSimilarity,
		int tiedCandidateCount)
	{
		_logger.Warning(
			CandidateAmbiguityEvent,
			"Ranking hints produced an ambiguous top-candidate tie.",
			BuildContext(
				("candidate_count", candidateCount.ToString(CultureInfo.InvariantCulture)),
				("expected_title_count", expectedTitleCount.ToString(CultureInfo.InvariantCulture)),
				("top_similarity", topSimilarity.ToString("F6", CultureInfo.InvariantCulture)),
				("tied_candidate_count", tiedCandidateCount.ToString(CultureInfo.InvariantCulture))));
	}

	/// <summary>
	/// Builds one structured logging context dictionary from non-empty values.
	/// </summary>
	/// <param name="pairs">Context key/value pairs.</param>
	/// <returns>Structured context dictionary.</returns>
	private static IReadOnlyDictionary<string, string> BuildContext(params (string Key, string? Value)[] pairs)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		for (int index = 0; index < pairs.Length; index++)
		{
			(string key, string? value) = pairs[index];
			if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			context[key] = value;
		}

		return context;
	}
}
