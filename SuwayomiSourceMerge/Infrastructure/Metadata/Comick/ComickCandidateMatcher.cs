using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Performs strict exact-key candidate matching across Comick comic title and alternate-title fields.
/// </summary>
internal sealed class ComickCandidateMatcher : IComickCandidateMatcher
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
	/// Initializes a new instance of the <see cref="ComickCandidateMatcher"/> class.
	/// </summary>
	/// <param name="sceneTagMatcher">
	/// Optional scene-tag matcher used for title-key normalization.
	/// </param>
	public ComickCandidateMatcher(ISceneTagMatcher? sceneTagMatcher = null)
	{
		_titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);
	}

	/// <inheritdoc />
	public ComickCandidateMatchResult Match(
		IReadOnlyList<ComickComicResponse> candidates,
		IReadOnlyList<string> expectedTitles)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		ArgumentNullException.ThrowIfNull(expectedTitles);

		HashSet<string> expectedTitleKeys = BuildExpectedTitleKeys(expectedTitles);
		if (expectedTitleKeys.Count == 0)
		{
			return CreateNoHighConfidenceResult();
		}

		int bestScore = NoMatchScore;
		int bestCandidateIndex = ComickCandidateMatchResult.NoMatchCandidateIndex;
		ComickComicResponse? bestCandidate = null;
		bool hadTopTie = false;

		for (int index = 0; index < candidates.Count; index++)
		{
			ComickComicResponse candidate = candidates[index]
				?? throw new ArgumentException(
					$"Candidates must not contain null values. Invalid item at index {index}.",
					nameof(candidates));
			int score = EvaluateCandidateScore(candidate, expectedTitleKeys);
			if (score > bestScore)
			{
				bestScore = score;
				bestCandidateIndex = index;
				bestCandidate = candidate;
				hadTopTie = false;
				continue;
			}

			if (score == bestScore && score > NoMatchScore)
			{
				hadTopTie = true;
			}
		}

		if (bestScore == NoMatchScore || bestCandidate is null)
		{
			return CreateNoHighConfidenceResult();
		}

		return new ComickCandidateMatchResult(
			ComickCandidateMatchOutcome.Matched,
			bestCandidate,
			bestCandidateIndex,
			hadTopTie,
			bestScore);
	}

	/// <summary>
	/// Creates a no-match result with canonical sentinel values.
	/// </summary>
	/// <returns>No-high-confidence match result.</returns>
	private static ComickCandidateMatchResult CreateNoHighConfidenceResult()
	{
		return new ComickCandidateMatchResult(
			ComickCandidateMatchOutcome.NoHighConfidenceMatch,
			matchedCandidate: null,
			ComickCandidateMatchResult.NoMatchCandidateIndex,
			hadTopTie: false,
			matchScore: NoMatchScore);
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
