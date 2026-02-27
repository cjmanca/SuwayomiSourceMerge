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
	/// Represents top-similarity tie metadata derived from ranking-hint scores.
	/// </summary>
	private readonly record struct TopSimilarityTieInfo(
		bool HasTopSimilarityTie,
		double TopSimilarity,
		int TiedCandidateCount,
		IReadOnlySet<int> TiedCandidateIndices)
	{
		/// <summary>
		/// Determines whether the provided candidate index is part of the top-similarity tie set.
		/// </summary>
		/// <param name="candidateIndex">Candidate index.</param>
		/// <returns><see langword="true"/> when candidate index is tied at top similarity; otherwise <see langword="false"/>.</returns>
		public bool IsCandidateInTie(int candidateIndex)
		{
			return HasTopSimilarityTie && TiedCandidateIndices.Contains(candidateIndex);
		}
	}

	/// <summary>
	/// Computes top-similarity tie information from ranking-hint scores.
	/// </summary>
	/// <param name="candidateSimilarityScores">Ranking-hint similarity scores by candidate index.</param>
	/// <returns>Top-similarity tie metadata.</returns>
	private static TopSimilarityTieInfo GetTopSimilarityTieInfo(IReadOnlyList<double> candidateSimilarityScores)
	{
		ArgumentNullException.ThrowIfNull(candidateSimilarityScores);

		if (candidateSimilarityScores.Count < 2)
		{
			return new TopSimilarityTieInfo(false, 0d, 0, new HashSet<int>());
		}

		double topSimilarity = double.NegativeInfinity;
		HashSet<int> tiedCandidateIndices = new();
		for (int index = 0; index < candidateSimilarityScores.Count; index++)
		{
			double similarity = candidateSimilarityScores[index];
			if (similarity > topSimilarity + SimilarityTieEpsilon)
			{
				topSimilarity = similarity;
				tiedCandidateIndices.Clear();
				tiedCandidateIndices.Add(index);
			}
			else if (Math.Abs(similarity - topSimilarity) <= SimilarityTieEpsilon)
			{
				tiedCandidateIndices.Add(index);
			}
		}

		bool hasTie = tiedCandidateIndices.Count > 1 && topSimilarity > 0d;
		return hasTie
			? new TopSimilarityTieInfo(true, topSimilarity, tiedCandidateIndices.Count, tiedCandidateIndices)
			: new TopSimilarityTieInfo(false, 0d, 0, new HashSet<int>());
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
