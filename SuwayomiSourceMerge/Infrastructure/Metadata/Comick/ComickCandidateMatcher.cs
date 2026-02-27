using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Resolves Comick search candidates to comic-detail payloads and performs strict exact-key matching.
/// </summary>
internal sealed partial class ComickCandidateMatcher : IComickCandidateMatcher
{
	/// <summary>
	/// Score used when candidate fields do not match expected title keys.
	/// </summary>
	private const int NoMatchScore = 0;

	/// <summary>
	/// Score used when one candidate has an alternate-title key match only.
	/// </summary>
	private const int MdTitleMatchScore = 1;

	/// <summary>
	/// Score used when one candidate has a primary comic-title key match.
	/// </summary>
	private const int ComicTitleMatchScore = 2;

	/// <summary>
	/// Shared cached title normalizer used for expected and candidate key comparison.
	/// </summary>
	private readonly ITitleComparisonNormalizer _titleComparisonNormalizer;

	/// <summary>
	/// Comick API gateway used to resolve search candidates to comic-detail payloads.
	/// </summary>
	private readonly IComickApiGateway _comickApiGateway;

	/// <summary>
	/// Logger dependency.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickCandidateMatcher"/> class.
	/// </summary>
	/// <param name="comickApiGateway">Comick API gateway used for <c>/comic/{slug}/</c> requests.</param>
	/// <param name="sceneTagMatcher">
	/// Optional scene-tag matcher used for title-key normalization.
	/// </param>
	/// <param name="logger">Optional logger dependency.</param>
	public ComickCandidateMatcher(
		IComickApiGateway comickApiGateway,
		ISceneTagMatcher? sceneTagMatcher = null,
		ISsmLogger? logger = null)
	{
		_comickApiGateway = comickApiGateway ?? throw new ArgumentNullException(nameof(comickApiGateway));
		_titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);
		_logger = logger ?? NoOpSsmLogger.Instance;
	}

	/// <inheritdoc />
	public async Task<ComickCandidateMatchResult> MatchAsync(
		IReadOnlyList<ComickSearchComic> candidates,
		IReadOnlyList<string> expectedTitles,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		ArgumentNullException.ThrowIfNull(expectedTitles);

		ValidateCandidateEntries(candidates);
		HashSet<string> expectedTitleKeys = BuildExpectedTitleKeys(expectedTitles);
		if (expectedTitleKeys.Count == 0)
		{
			return CreateNoHighConfidenceResult();
		}

		(bool hasTopSimilarityTie, double topSimilarity, int tiedCandidateCount) = GetTopSimilarityTieInfo(candidates, expectedTitleKeys);
		IReadOnlyList<int> evaluationOrder = BuildEvaluationOrder(candidates, expectedTitleKeys);
		bool hadServiceInterruption = false;
		for (int orderIndex = 0; orderIndex < evaluationOrder.Count; orderIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			int candidateIndex = evaluationOrder[orderIndex];
			ComickSearchComic searchCandidate = candidates[candidateIndex];
			if (string.IsNullOrWhiteSpace(searchCandidate.Slug))
			{
				continue;
			}

			ComickDirectApiResult<ComickComicResponse> detailResult;
			try
			{
				detailResult = await _comickApiGateway
					.GetComicAsync(searchCandidate.Slug, cancellationToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				hadServiceInterruption = true;
				continue;
			}

			if (detailResult.Outcome == ComickDirectApiOutcome.Cancelled)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					throw new OperationCanceledException(cancellationToken);
				}

				hadServiceInterruption = true;
				continue;
			}

			if (IsServiceInterruptionOutcome(detailResult.Outcome))
			{
				hadServiceInterruption = true;
			}

			if (detailResult.Outcome != ComickDirectApiOutcome.Success || detailResult.Payload is null)
			{
				continue;
			}

			int matchScore = EvaluateCandidateScore(detailResult.Payload, expectedTitleKeys);
			if (matchScore == NoMatchScore)
			{
				continue;
			}

			if (hasTopSimilarityTie)
			{
				LogCandidateAmbiguity(candidates.Count, expectedTitleKeys.Count, topSimilarity, tiedCandidateCount);
			}

			return new ComickCandidateMatchResult(
				ComickCandidateMatchOutcome.Matched,
				detailResult.Payload,
				candidateIndex,
				hadTopTie: false,
				matchScore,
				hadServiceInterruption);
		}

		if (hasTopSimilarityTie)
		{
			LogCandidateAmbiguity(candidates.Count, expectedTitleKeys.Count, topSimilarity, tiedCandidateCount);
		}

		return CreateNoHighConfidenceResult(hadServiceInterruption);
	}

	/// <summary>
	/// Creates a no-match result with canonical sentinel values.
	/// </summary>
	/// <returns>No-high-confidence match result.</returns>
	private static ComickCandidateMatchResult CreateNoHighConfidenceResult(bool hadServiceInterruption = false)
	{
		return new ComickCandidateMatchResult(
			ComickCandidateMatchOutcome.NoHighConfidenceMatch,
			matchedCandidate: null,
			ComickCandidateMatchResult.NoMatchCandidateIndex,
			hadTopTie: false,
			matchScore: NoMatchScore,
			hadServiceInterruption);
	}

	/// <summary>
	/// Determines whether one direct-API detail outcome indicates Comick service interruption.
	/// </summary>
	/// <param name="outcome">Detail-request outcome.</param>
	/// <returns><see langword="true"/> when outcome indicates interruption; otherwise <see langword="false"/>.</returns>
	private static bool IsServiceInterruptionOutcome(ComickDirectApiOutcome outcome)
	{
		return outcome == ComickDirectApiOutcome.TransportFailure
			|| outcome == ComickDirectApiOutcome.CloudflareBlocked
			|| outcome == ComickDirectApiOutcome.HttpFailure
			|| outcome == ComickDirectApiOutcome.MalformedPayload;
	}

	/// <summary>
	/// Validates that candidate collections do not include null entries.
	/// </summary>
	/// <param name="candidates">Candidate collection.</param>
	private static void ValidateCandidateEntries(IReadOnlyList<ComickSearchComic> candidates)
	{
		for (int index = 0; index < candidates.Count; index++)
		{
			if (candidates[index] is null)
			{
				throw new ArgumentException(
					$"Candidates must not contain null values. Invalid item at index {index}.",
					nameof(candidates));
			}
		}
	}

	/// <summary>
	/// Builds normalized expected title keys from one raw expected-title list.
	/// </summary>
	/// <param name="expectedTitles">Raw expected-title values.</param>
	/// <returns>Distinct normalized title keys.</returns>
	private HashSet<string> BuildExpectedTitleKeys(IReadOnlyList<string> expectedTitles)
	{
		HashSet<string> expectedTitleKeys = new(StringComparer.Ordinal);
		for (int index = 0; index < expectedTitles.Count; index++)
		{
			string? expectedTitle = expectedTitles[index];
			if (string.IsNullOrWhiteSpace(expectedTitle))
			{
				continue;
			}

			string normalizedKey = _titleComparisonNormalizer.NormalizeTitleKey(expectedTitle);
			if (string.IsNullOrWhiteSpace(normalizedKey))
			{
				continue;
			}

			expectedTitleKeys.Add(normalizedKey);
		}

		return expectedTitleKeys;
	}

	/// <summary>
	/// Builds ordered candidate indices used for detail-request evaluation.
	/// </summary>
	/// <remarks>
	/// Search index zero is always evaluated first. Remaining candidates are ordered by descending normalized
	/// Levenshtein similarity against expected title keys, then by original index.
	/// </remarks>
	/// <param name="candidates">Search candidates.</param>
	/// <param name="expectedTitleKeys">Expected normalized title keys.</param>
	/// <returns>Ordered candidate indices.</returns>
	private IReadOnlyList<int> BuildEvaluationOrder(
		IReadOnlyList<ComickSearchComic> candidates,
		IReadOnlySet<string> expectedTitleKeys)
	{
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
			ComickSearchComic candidate = candidates[candidateIndex];
			double similarity = ComputeSearchCandidateOrderingSimilarity(candidate, expectedTitleKeys);
			rankedCandidates.Add((candidateIndex, similarity));
		}

		evaluationOrder.AddRange(
			rankedCandidates
				.OrderByDescending(static item => item.SimilarityScore)
				.ThenBy(static item => item.Index)
				.Select(static item => item.Index));
		return evaluationOrder;
	}

	/// <summary>
	/// Computes one ranking similarity score for a search candidate using search title and search alias hints.
	/// </summary>
	/// <param name="candidate">Search candidate.</param>
	/// <param name="expectedTitleKeys">Expected normalized title keys.</param>
	/// <returns>Similarity score from <c>0.0</c> to <c>1.0</c>.</returns>
	private double ComputeSearchCandidateOrderingSimilarity(
		ComickSearchComic candidate,
		IReadOnlySet<string> expectedTitleKeys)
	{
		ArgumentNullException.ThrowIfNull(candidate);

		double maxSimilarity = ComputeMaxNormalizedLevenshteinSimilarity(
			NormalizeKeyOrEmpty(candidate.Title),
			expectedTitleKeys);
		if (candidate.MdTitles is null)
		{
			return maxSimilarity;
		}

		for (int aliasIndex = 0; aliasIndex < candidate.MdTitles.Count; aliasIndex++)
		{
			ComickTitleAlias? alias = candidate.MdTitles[aliasIndex];
			if (alias is null)
			{
				continue;
			}

			double aliasSimilarity = ComputeMaxNormalizedLevenshteinSimilarity(
				NormalizeKeyOrEmpty(alias.Title),
				expectedTitleKeys);
			if (aliasSimilarity > maxSimilarity)
			{
				maxSimilarity = aliasSimilarity;
			}
		}

		return maxSimilarity;
	}

	/// <summary>
	/// Normalizes one title-like value to a comparison key, returning an empty value for blank input.
	/// </summary>
	/// <param name="value">Input value.</param>
	/// <returns>Normalized key or an empty string.</returns>
	private string NormalizeKeyOrEmpty(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: _titleComparisonNormalizer.NormalizeTitleKey(value);
	}

	/// <summary>
	/// Computes the best normalized Levenshtein similarity score between one candidate key and expected keys.
	/// </summary>
	/// <param name="normalizedCandidateTitle">Normalized candidate title key.</param>
	/// <param name="expectedTitleKeys">Expected normalized title keys.</param>
	/// <returns>Similarity score from <c>0.0</c> to <c>1.0</c>.</returns>
	private static double ComputeMaxNormalizedLevenshteinSimilarity(
		string normalizedCandidateTitle,
		IReadOnlySet<string> expectedTitleKeys)
	{
		if (string.IsNullOrWhiteSpace(normalizedCandidateTitle))
		{
			return 0d;
		}

		double maxSimilarity = 0d;
		foreach (string expectedKey in expectedTitleKeys)
		{
			double similarity = ComputeNormalizedLevenshteinSimilarity(normalizedCandidateTitle, expectedKey);
			if (similarity > maxSimilarity)
			{
				maxSimilarity = similarity;
			}

			if (maxSimilarity == 1d)
			{
				return maxSimilarity;
			}
		}

		return maxSimilarity;
	}

	/// <summary>
	/// Computes normalized Levenshtein similarity between two normalized title keys.
	/// </summary>
	/// <param name="left">Left normalized key.</param>
	/// <param name="right">Right normalized key.</param>
	/// <returns>Similarity score from <c>0.0</c> to <c>1.0</c>.</returns>
	private static double ComputeNormalizedLevenshteinSimilarity(string left, string right)
	{
		if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
		{
			return 0d;
		}

		int maxLength = Math.Max(left.Length, right.Length);
		int distance = ComputeLevenshteinDistance(left, right);
		return 1d - ((double)distance / maxLength);
	}

	/// <summary>
	/// Computes the Levenshtein edit distance between two normalized title keys.
	/// </summary>
	/// <param name="left">Left key.</param>
	/// <param name="right">Right key.</param>
	/// <returns>Edit distance value.</returns>
	private static int ComputeLevenshteinDistance(string left, string right)
	{
		// Defensive contract checks remain intentional in case future call sites bypass current pre-validation.
		ArgumentNullException.ThrowIfNull(left);
		ArgumentNullException.ThrowIfNull(right);

		if (left.Length == 0)
		{
			return right.Length;
		}

		if (right.Length == 0)
		{
			return left.Length;
		}

		int[] previousRow = new int[right.Length + 1];
		int[] currentRow = new int[right.Length + 1];
		for (int column = 0; column <= right.Length; column++)
		{
			previousRow[column] = column;
		}

		for (int row = 1; row <= left.Length; row++)
		{
			currentRow[0] = row;
			for (int column = 1; column <= right.Length; column++)
			{
				int substitutionCost = left[row - 1] == right[column - 1] ? 0 : 1;
				int insertion = currentRow[column - 1] + 1;
				int deletion = previousRow[column] + 1;
				int substitution = previousRow[column - 1] + substitutionCost;
				currentRow[column] = Math.Min(Math.Min(insertion, deletion), substitution);
			}

			(previousRow, currentRow) = (currentRow, previousRow);
		}

		return previousRow[right.Length];
	}

	/// <summary>
	/// Evaluates one candidate score against expected normalized title keys.
	/// </summary>
	/// <param name="candidate">Candidate to score.</param>
	/// <param name="expectedTitleKeys">Expected normalized title keys.</param>
	/// <returns>Candidate match score.</returns>
	private int EvaluateCandidateScore(
		ComickComicResponse candidate,
		IReadOnlySet<string> expectedTitleKeys)
	{
		ComickComicDetails? comic = candidate.Comic;
		if (comic is null)
		{
			return NoMatchScore;
		}

		if (IsExpectedTitle(comic.Title, expectedTitleKeys))
		{
			return ComicTitleMatchScore;
		}

		if (comic.MdTitles is null)
		{
			return NoMatchScore;
		}

		for (int index = 0; index < comic.MdTitles.Count; index++)
		{
			ComickTitleAlias? alias = comic.MdTitles[index];
			if (alias is null)
			{
				continue;
			}

			if (IsExpectedTitle(alias.Title, expectedTitleKeys))
			{
				return MdTitleMatchScore;
			}
		}

		return NoMatchScore;
	}

	/// <summary>
	/// Determines whether one raw title value matches any expected normalized title key.
	/// </summary>
	/// <param name="title">Raw title value.</param>
	/// <param name="expectedTitleKeys">Expected normalized title keys.</param>
	/// <returns><see langword="true"/> when title matches one expected key; otherwise <see langword="false"/>.</returns>
	private bool IsExpectedTitle(string? title, IReadOnlySet<string> expectedTitleKeys)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return false;
		}

		string normalizedKey = _titleComparisonNormalizer.NormalizeTitleKey(title);
		if (string.IsNullOrWhiteSpace(normalizedKey))
		{
			return false;
		}

		return expectedTitleKeys.Contains(normalizedKey);
	}
}
