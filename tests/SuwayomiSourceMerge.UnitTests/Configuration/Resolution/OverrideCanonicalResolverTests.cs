namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Tests override canonical title resolution from existing override directory names.
/// </summary>
public sealed class OverrideCanonicalResolverTests
{
	[Fact]
	public void TryResolveOverrideCanonical_ShouldReturnExactOverrideTitle_WhenMatchExists()
	{
		OverrideCanonicalResolver resolver = new(
		[
			CreateCatalogEntry("Manga Title 1"),
			CreateCatalogEntry("Another Series")
		]);

		bool wasResolved = resolver.TryResolveOverrideCanonical("The Manga Title 1!!!", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga Title 1", overrideCanonicalTitle);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldResolveMatcherAwareSuffixVariant_WhenMatcherProvided()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);
		OverrideCanonicalResolver resolver = new(
		[
			CreateCatalogEntry("Manga Title", matcher)
		],
			matcher);

		bool wasResolved = resolver.TryResolveOverrideCanonical("Manga Title [Official]", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga Title", overrideCanonicalTitle);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldReuseCachedMatcherAwareNormalization_ForRepeatedInput()
	{
		CountingSceneTagMatcher matcher = new(["official"]);
		OverrideCanonicalResolver resolver = new(
		[
			CreateCatalogEntry("Manga Title", matcher)
		],
			matcher);

		bool firstResolved = resolver.TryResolveOverrideCanonical("Manga Title [Official]", out string firstOverrideCanonicalTitle);
		int countAfterFirst = matcher.MatchCallCount;
		bool secondResolved = resolver.TryResolveOverrideCanonical("Manga Title [Official]", out string secondOverrideCanonicalTitle);

		Assert.True(firstResolved);
		Assert.True(secondResolved);
		Assert.Equal("Manga Title", firstOverrideCanonicalTitle);
		Assert.Equal(firstOverrideCanonicalTitle, secondOverrideCanonicalTitle);
		Assert.True(countAfterFirst > 0);
		Assert.Equal(countAfterFirst, matcher.MatchCallCount);
	}

	[Fact]
	public void Constructor_ShouldChooseDeterministicTitle_WhenNormalizedCollisionsExist()
	{
		OverrideCanonicalResolver resolver = new(
		[
			CreateCatalogEntry("Manga-Title"),
			CreateCatalogEntry("Manga Title")
		]);

		bool wasResolved = resolver.TryResolveOverrideCanonical("Manga Title", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga Title", overrideCanonicalTitle);
	}

	[Fact]
	public void Constructor_ShouldChooseUntaggedTitle_WhenTaggedAndUntaggedVariantsExist()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);
		OverrideCanonicalResolver resolver = new(
		[
			CreateCatalogEntry("Solo Leveling [Official]", matcher),
			CreateCatalogEntry("Solo Leveling", matcher)
		],
			matcher);

		bool wasResolved = resolver.TryResolveOverrideCanonical("Solo Leveling [Official]", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Solo Leveling", overrideCanonicalTitle);
		Assert.Empty(resolver.Advisories);
	}

	[Fact]
	public void Constructor_ShouldEmitAdvisory_WhenOnlyTaggedVariantExists()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);
		string taggedPath = Path.Combine(Path.GetTempPath(), "ssm-tests", "Solo Leveling [Official]");
		OverrideCanonicalResolver resolver = new(
		[
			CreateCatalogEntry("Solo Leveling [Official]", matcher, taggedPath)
		],
			matcher);

		bool wasResolved = resolver.TryResolveOverrideCanonical("Solo Leveling", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Solo Leveling [Official]", overrideCanonicalTitle);
		OverrideCanonicalAdvisory advisory = Assert.Single(resolver.Advisories);
		Assert.Equal("Solo Leveling [Official]", advisory.SelectedTitle);
		Assert.Equal("Solo Leveling", advisory.SuggestedStrippedTitle);
		Assert.Equal(Path.GetFullPath(taggedPath), advisory.SelectedDirectoryPath);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldReturnFalseAndEmpty_WhenNonEmptyTitleIsNotMapped()
	{
		OverrideCanonicalResolver resolver = new(
		[
			CreateCatalogEntry("Manga Title 1"),
			CreateCatalogEntry("Another Series")
		]);

		bool wasResolved = resolver.TryResolveOverrideCanonical("Unknown Title", out string overrideCanonicalTitle);

		Assert.False(wasResolved);
		Assert.Equal(string.Empty, overrideCanonicalTitle);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenExistingTitlesCollectionIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => new OverrideCanonicalResolver(null!));
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenAnyEntryIsNull()
	{
		ArgumentException exception = Assert.Throws<ArgumentException>(() => new OverrideCanonicalResolver(
			[
				CreateCatalogEntry("Manga Title"),
				null!
			]));
		Assert.Equal("existingOverrideEntries", exception.ParamName);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenEntryNormalizedKeyMismatchesTitleNormalization()
	{
		OverrideTitleCatalogEntry invalid = new(
			"Manga Title",
			Path.Combine(Path.GetTempPath(), "ssm-tests", "Manga Title"),
			"invalid",
			"Manga Title",
			isSuffixTagged: false);

		ArgumentException exception = Assert.Throws<ArgumentException>(() => new OverrideCanonicalResolver([invalid]));
		Assert.Equal("existingOverrideEntries", exception.ParamName);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldThrow_WhenInputTitleIsNull()
	{
		OverrideCanonicalResolver resolver = new([CreateCatalogEntry("Manga Title")]);

		Assert.Throws<ArgumentNullException>(() => resolver.TryResolveOverrideCanonical(null!, out _));
	}

	private static OverrideTitleCatalogEntry CreateCatalogEntry(
		string title,
		ISceneTagMatcher? sceneTagMatcher = null,
		string? directoryPath = null)
	{
		ITitleComparisonNormalizer normalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);
		string normalizedKey = normalizer.NormalizeTitleKey(title);
		string strippedTitle = TitleKeyNormalizer.StripTrailingSceneTagSuffixes(title, sceneTagMatcher);
		bool isSuffixTagged = !string.Equals(strippedTitle, title.Trim(), StringComparison.Ordinal);
		return new OverrideTitleCatalogEntry(
			title,
			directoryPath ?? Path.Combine(Path.GetTempPath(), "ssm-tests", title),
			normalizedKey,
			strippedTitle,
			isSuffixTagged);
	}
}
