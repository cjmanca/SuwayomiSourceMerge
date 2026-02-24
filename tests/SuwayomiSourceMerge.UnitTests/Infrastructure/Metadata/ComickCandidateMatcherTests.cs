namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.Configuration.Validation;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="ComickCandidateMatcher"/>.
/// </summary>
public sealed class ComickCandidateMatcherTests
{
	/// <summary>
	/// Verifies a unique exact comic-title match is selected with the highest score.
	/// </summary>
	[Fact]
	public void Match_Expected_ShouldSelectUniqueComicTitleMatch()
	{
		ComickCandidateMatcher matcher = new();
		ComickComicResponse firstCandidate = CreateCandidate("Unrelated Title", "Alias One");
		ComickComicResponse secondCandidate = CreateCandidate("The Target Title", "Other Alias");

		ComickCandidateMatchResult result = matcher.Match(
			[firstCandidate, secondCandidate],
			["target title", "input title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Same(secondCandidate, result.MatchedCandidate);
		Assert.Equal(1, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(2, result.MatchScore);
	}

	/// <summary>
	/// Verifies a unique exact alternate-title match is selected when comic title does not match.
	/// </summary>
	[Fact]
	public void Match_Expected_ShouldSelectUniqueMdTitleMatch_WhenComicTitleDoesNotMatch()
	{
		ComickCandidateMatcher matcher = new();
		ComickComicResponse candidate = CreateCandidate("Different Title", "Target Alias");

		ComickCandidateMatchResult result = matcher.Match(
			[candidate],
			["target alias", "input title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Same(candidate, result.MatchedCandidate);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(1, result.MatchScore);
	}

	/// <summary>
	/// Verifies equal top-score ties keep first candidate and set tie indicator.
	/// </summary>
	[Fact]
	public void Match_Edge_ShouldSelectFirstCandidate_WhenTopScoreIsTied()
	{
		ComickCandidateMatcher matcher = new();
		ComickComicResponse firstCandidate = CreateCandidate("Shared Title");
		ComickComicResponse secondCandidate = CreateCandidate("The Shared Title");
		ComickComicResponse thirdCandidate = CreateCandidate("Unrelated");

		ComickCandidateMatchResult result = matcher.Match(
			[firstCandidate, secondCandidate, thirdCandidate],
			["shared title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Same(firstCandidate, result.MatchedCandidate);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.True(result.HadTopTie);
		Assert.Equal(2, result.MatchScore);
	}

	/// <summary>
	/// Verifies comic-title matches outrank alternate-title matches.
	/// </summary>
	[Fact]
	public void Match_Edge_ShouldPreferComicTitleMatch_OverMdTitleMatch()
	{
		ComickCandidateMatcher matcher = new();
		ComickComicResponse mdTitleOnlyCandidate = CreateCandidate("Different", "Target Title");
		ComickComicResponse comicTitleCandidate = CreateCandidate("Target Title", "Other Alias");

		ComickCandidateMatchResult result = matcher.Match(
			[mdTitleOnlyCandidate, comicTitleCandidate],
			["target title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Same(comicTitleCandidate, result.MatchedCandidate);
		Assert.Equal(1, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(2, result.MatchScore);
	}

	/// <summary>
	/// Verifies no exact matches produce no-high-confidence outcome.
	/// </summary>
	[Fact]
	public void Match_Failure_ShouldReturnNoHighConfidenceMatch_WhenNoExactMatchesExist()
	{
		ComickCandidateMatcher matcher = new();

		ComickCandidateMatchResult result = matcher.Match(
			[CreateCandidate("First"), CreateCandidate("Second", "Another")],
			["unmatched"]);

		Assert.Equal(ComickCandidateMatchOutcome.NoHighConfidenceMatch, result.Outcome);
		Assert.Null(result.MatchedCandidate);
		Assert.Equal(ComickCandidateMatchResult.NoMatchCandidateIndex, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(0, result.MatchScore);
	}

	/// <summary>
	/// Verifies null input collections are rejected with argument-null guards.
	/// </summary>
	[Fact]
	public void Match_Failure_ShouldThrow_WhenArgumentsAreNull()
	{
		ComickCandidateMatcher matcher = new();

		Assert.Throws<ArgumentNullException>(() => matcher.Match(null!, ["title"]));
		Assert.Throws<ArgumentNullException>(() => matcher.Match([], null!));
	}

	/// <summary>
	/// Verifies matcher normalization stays aligned with established scene-tag stripping fixtures.
	/// </summary>
	/// <param name="rawTitle">Raw fixture title text.</param>
	/// <param name="expectedStrippedTitle">Expected stripped fixture title text.</param>
	[Theory]
	[MemberData(nameof(ValidationKeyNormalizerTests.GetTagStrippingFixtures), MemberType = typeof(ValidationKeyNormalizerTests))]
	public void Match_Expected_ShouldReuseNormalizationFixtures_WhenSceneTagMatcherConfigured(
		string rawTitle,
		string expectedStrippedTitle)
	{
		ComickCandidateMatcher matcher = new(new SceneTagMatcher(SceneTagsDocumentDefaults.Create().Tags!));
		ComickComicResponse candidate = CreateCandidate(expectedStrippedTitle);

		ComickCandidateMatchResult result = matcher.Match([candidate], [rawTitle]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Same(candidate, result.MatchedCandidate);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(2, result.MatchScore);
	}

	/// <summary>
	/// Verifies trailing scene-tag normalization on comic title yields a high-confidence primary-title match.
	/// </summary>
	[Fact]
	public void Match_Expected_ShouldMatchComicTitleAfterTrailingSceneTagNormalization()
	{
		ComickCandidateMatcher matcher = new(new SceneTagMatcher(["official", "digital"]));
		ComickComicResponse candidate = CreateCandidate("Manga Title");

		ComickCandidateMatchResult result = matcher.Match(
			[candidate],
			["Manga Title [Official]"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Same(candidate, result.MatchedCandidate);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(2, result.MatchScore);
	}

	/// <summary>
	/// Verifies trailing scene-tag normalization on alternate titles yields an alias-only match score.
	/// </summary>
	[Fact]
	public void Match_Expected_ShouldMatchMdTitleAfterTrailingSceneTagNormalization()
	{
		ComickCandidateMatcher matcher = new(new SceneTagMatcher(["official", "digital"]));
		ComickComicResponse candidate = CreateCandidate(
			"Different Display Title",
			"Manga Title");

		ComickCandidateMatchResult result = matcher.Match(
			[candidate],
			["Manga Title [Official]"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Same(candidate, result.MatchedCandidate);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(1, result.MatchScore);
	}

	/// <summary>
	/// Creates one minimal candidate payload for matcher tests.
	/// </summary>
	/// <param name="comicTitle">Primary comic title.</param>
	/// <param name="mdTitles">Alternate titles.</param>
	/// <returns>Candidate payload.</returns>
	private static ComickComicResponse CreateCandidate(string comicTitle, params string[] mdTitles)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(comicTitle);
		ArgumentNullException.ThrowIfNull(mdTitles);

		return new ComickComicResponse
		{
			Comic = new ComickComicDetails
			{
				Title = comicTitle,
				MdTitles = mdTitles
					.Select(
						title => new ComickTitleAlias
						{
							Title = title
						})
					.ToArray()
			}
		};
	}
}
