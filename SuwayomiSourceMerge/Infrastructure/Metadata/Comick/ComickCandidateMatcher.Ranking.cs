namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Candidate ranking helpers for <see cref="ComickCandidateMatcher"/>.
/// </summary>
internal sealed partial class ComickCandidateMatcher
{
	/// <summary>
	/// Computes ranking-hint similarity scores for each search candidate index.
	/// </summary>
	/// <param name="candidates">Search candidates.</param>
	/// <param name="expectedTitleKeys">Expected normalized title keys.</param>
	/// <returns>Similarity scores by candidate index.</returns>
	private double[] BuildCandidateSimilarityScores(
		IReadOnlyList<ComickSearchComic> candidates,
		IReadOnlySet<string> expectedTitleKeys)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		ArgumentNullException.ThrowIfNull(expectedTitleKeys);

		double[] similarityScores = new double[candidates.Count];
		for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
		{
			similarityScores[candidateIndex] = ComputeSearchCandidateOrderingSimilarity(
				candidates[candidateIndex],
				expectedTitleKeys);
		}

		return similarityScores;
	}

	/// <summary>
	/// Builds ordered candidate indices used for detail-request evaluation.
	/// </summary>
	/// <remarks>
	/// Search index zero is always evaluated first. Remaining candidates are ordered by descending normalized
	/// Levenshtein similarity against expected title keys, then by original index.
	/// </remarks>
	/// <param name="candidates">Search candidates.</param>
	/// <param name="candidateSimilarityScores">Ranking-hint similarity scores by candidate index.</param>
	/// <returns>Ordered candidate indices.</returns>
	private IReadOnlyList<int> BuildEvaluationOrder(
		IReadOnlyList<ComickSearchComic> candidates,
		IReadOnlyList<double> candidateSimilarityScores)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		ArgumentNullException.ThrowIfNull(candidateSimilarityScores);
		if (candidates.Count != candidateSimilarityScores.Count)
		{
			throw new ArgumentException(
				"Candidate similarity score count must match candidate count.",
				nameof(candidateSimilarityScores));
		}

		List<int> evaluationOrder = new(candidates.Count);
		if (candidates.Count == 0)
		{
			return evaluationOrder;
		}

		evaluationOrder.Add(0);
		if (candidates.Count == 1)
		{
			return evaluationOrder;
		}

		List<(int Index, double SimilarityScore)> rankedCandidates = new(candidates.Count - 1);
		for (int candidateIndex = 1; candidateIndex < candidates.Count; candidateIndex++)
		{
			rankedCandidates.Add((candidateIndex, candidateSimilarityScores[candidateIndex]));
		}

		evaluationOrder.AddRange(
			rankedCandidates
				.OrderByDescending(static item => item.SimilarityScore)
				.ThenBy(static item => item.Index)
				.Select(static item => item.Index));
		return evaluationOrder;
	}
}
