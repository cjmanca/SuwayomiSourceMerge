namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Tests canonical title resolution from YAML-backed manga-equivalence mappings.
/// </summary>
public sealed class MangaEquivalenceServiceTests
{
	[Fact]
	public void TryResolveCanonicalTitle_ShouldReturnCanonical_WhenAliasIsMapped()
	{
		MangaEquivalenceService service = new(CreateDocument());

		bool wasResolved = service.TryResolveCanonicalTitle("The Manga Alpha", out string canonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga Alpha", canonicalTitle);
	}

	[Fact]
	public void ResolveCanonicalOrInput_ShouldReturnOriginalInput_WhenNoMappingExists()
	{
		MangaEquivalenceService service = new(CreateDocument());

		string resolved = service.ResolveCanonicalOrInput("Unknown Title");

		Assert.Equal("Unknown Title", resolved);
	}

	[Fact]
	public void TryResolveCanonicalTitle_ShouldReturnFalseAndEmpty_WhenNonEmptyTitleIsNotMapped()
	{
		MangaEquivalenceService service = new(CreateDocument());

		bool wasResolved = service.TryResolveCanonicalTitle("Unknown Title", out string canonicalTitle);

		Assert.False(wasResolved);
		Assert.Equal(string.Empty, canonicalTitle);
	}

	[Fact]
	public void TryResolveCanonicalTitle_ShouldResolveMatcherAwareSuffixVariant_WhenMatcherProvided()
	{
		MangaEquivalenceService service = new(
			new MangaEquivalentsDocument
			{
				Groups =
				[
					new MangaEquivalentGroup
					{
						Canonical = "Manga Alpha",
						Aliases = ["Manga Alpha [Official]"]
					}
				]
			},
			new SceneTagMatcher(["official"]));

		bool wasResolved = service.TryResolveCanonicalTitle("Manga Alpha [Official]", out string canonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga Alpha", canonicalTitle);
	}

	[Fact]
	public void TryResolveCanonicalTitle_ShouldReuseCachedMatcherAwareNormalization_ForRepeatedInput()
	{
		CountingSceneTagMatcher matcher = new(["official"]);
		MangaEquivalenceService service = new(
			new MangaEquivalentsDocument
			{
				Groups =
				[
					new MangaEquivalentGroup
					{
						Canonical = "Manga Alpha",
						Aliases = ["Manga Alpha"]
					}
				]
			},
			matcher);

		bool firstResolved = service.TryResolveCanonicalTitle("Manga Alpha [Official]", out string firstCanonicalTitle);
		int countAfterFirst = matcher.MatchCallCount;
		bool secondResolved = service.TryResolveCanonicalTitle("Manga Alpha [Official]", out string secondCanonicalTitle);

		Assert.True(firstResolved);
		Assert.True(secondResolved);
		Assert.Equal("Manga Alpha", firstCanonicalTitle);
		Assert.Equal(firstCanonicalTitle, secondCanonicalTitle);
		Assert.True(countAfterFirst > 0);
		Assert.Equal(countAfterFirst, matcher.MatchCallCount);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenAliasesMapToConflictingCanonicals()
	{
		MangaEquivalentsDocument document = new()
		{
			Groups =
			[
				new MangaEquivalentGroup
				{
					Canonical = "Manga Alpha",
					Aliases = ["Shared Alias"]
				},
				new MangaEquivalentGroup
				{
					Canonical = "Manga Beta",
					Aliases = ["Shared Alias"]
				}
			]
		};

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new MangaEquivalenceService(document));
		Assert.Contains("conflicting canonical titles", exception.Message);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenGroupsListIsMissing()
	{
		MangaEquivalentsDocument document = new()
		{
			Groups = null
		};

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new MangaEquivalenceService(document));
		Assert.Contains("groups list", exception.Message);
	}

	[Fact]
	public void TryResolveCanonicalTitle_ShouldReturnFalse_WhenInputNormalizesToEmpty()
	{
		MangaEquivalenceService service = new(CreateDocument());

		bool wasResolved = service.TryResolveCanonicalTitle("!!!", out string canonicalTitle);

		Assert.False(wasResolved);
		Assert.Equal(string.Empty, canonicalTitle);
	}

	[Fact]
	public void TryResolveCanonicalTitle_ShouldThrow_WhenInputTitleIsNull()
	{
		MangaEquivalenceService service = new(CreateDocument());

		Assert.Throws<ArgumentNullException>(() => service.TryResolveCanonicalTitle(null!, out _));
	}

	private static MangaEquivalentsDocument CreateDocument()
	{
		return new MangaEquivalentsDocument
		{
			Groups =
			[
				new MangaEquivalentGroup
				{
					Canonical = "Manga Alpha",
					Aliases = ["The Manga Alpha", "Manga-Alpha"]
				}
			]
		};
	}
}
