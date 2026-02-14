namespace SuwayomiSourceMerge.UnitTests.Domain.Normalization;

using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies cached title/token comparison-key normalization behavior.
/// </summary>
public sealed class CachedTitleComparisonNormalizerTests
{
	[Fact]
	public void NormalizeTitleKey_ShouldCacheMatcherAwareResult_ForRepeatedInput()
	{
		CountingSceneTagMatcher matcher = new(["official"]);
		CachedTitleComparisonNormalizer normalizer = new(matcher);

		string first = normalizer.NormalizeTitleKey("Manga Title (Official)");
		int countAfterFirst = matcher.MatchCallCount;

		string second = normalizer.NormalizeTitleKey("Manga Title (Official)");

		Assert.Equal("mangatitle", first);
		Assert.Equal(first, second);
		Assert.True(countAfterFirst > 0);
		Assert.Equal(countAfterFirst, matcher.MatchCallCount);
	}

	[Fact]
	public void NormalizeTitleKey_ShouldReuseFirstComputedValue_WhenMatcherBehaviorChanges()
	{
		FlippingSceneTagMatcher matcher = new(initialResult: true);
		CachedTitleComparisonNormalizer normalizer = new(matcher);

		string first = normalizer.NormalizeTitleKey("Manga Title (Official)");
		string second = normalizer.NormalizeTitleKey("Manga Title (Official)");

		Assert.Equal("mangatitle", first);
		Assert.Equal(first, second);
		Assert.Equal(1, matcher.MatchCallCount);
	}

	[Fact]
	public void NormalizeTitleKey_ShouldReturnEmpty_WhenInputIsWhitespace()
	{
		CountingSceneTagMatcher matcher = new(["official"]);
		CachedTitleComparisonNormalizer normalizer = new(matcher);

		string first = normalizer.NormalizeTitleKey("   ");
		int countAfterFirst = matcher.MatchCallCount;

		string second = normalizer.NormalizeTitleKey("   ");

		Assert.Equal(string.Empty, first);
		Assert.Equal(first, second);
		Assert.Equal(0, countAfterFirst);
		Assert.Equal(countAfterFirst, matcher.MatchCallCount);
	}

	[Fact]
	public void NormalizeTitleKey_ShouldThrow_WhenInputIsNull()
	{
		CachedTitleComparisonNormalizer normalizer = new();

		Assert.Throws<ArgumentNullException>(() => normalizer.NormalizeTitleKey(null!));
	}

	[Fact]
	public void NormalizeTokenKey_ShouldCacheNormalizedValue_ForRepeatedInput()
	{
		CachedTitleComparisonNormalizer normalizer = new();

		string first = normalizer.NormalizeTokenKey("Asura-Scan! Official");
		string second = normalizer.NormalizeTokenKey("Asura-Scan! Official");

		Assert.Equal("asura scan official", first);
		Assert.Same(first, second);
	}

	[Fact]
	public void NormalizeTokenKey_ShouldThrow_WhenInputIsNull()
	{
		CachedTitleComparisonNormalizer normalizer = new();

		Assert.Throws<ArgumentNullException>(() => normalizer.NormalizeTokenKey(null!));
	}

	[Fact]
	public void NormalizeTitleKey_ShouldKeepDifferentInputsIsolated()
	{
		CachedTitleComparisonNormalizer normalizer = new();

		string first = normalizer.NormalizeTitleKey("The Manga Alpha");
		string second = normalizer.NormalizeTitleKey("The Manga Betas");

		Assert.Equal("mangaalpha", first);
		Assert.Equal("mangabeta", second);
		Assert.NotEqual(first, second);
	}

	[Fact]
	public void ProviderGet_ShouldReturnSharedInstance_WhenMatcherIsNull()
	{
		ITitleComparisonNormalizer first = TitleComparisonNormalizerProvider.Get(sceneTagMatcher: null);
		ITitleComparisonNormalizer second = TitleComparisonNormalizerProvider.Get(sceneTagMatcher: null);

		Assert.Same(first, second);
	}

	[Fact]
	public void ProviderGet_ShouldReturnSharedInstance_ForSameMatcherReference()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

		ITitleComparisonNormalizer first = TitleComparisonNormalizerProvider.Get(matcher);
		ITitleComparisonNormalizer second = TitleComparisonNormalizerProvider.Get(matcher);

		Assert.Same(first, second);
	}

	[Fact]
	public void ProviderGet_ShouldReturnDifferentInstances_ForDifferentMatcherReferences()
	{
		ISceneTagMatcher firstMatcher = new SceneTagMatcher(["official"]);
		ISceneTagMatcher secondMatcher = new SceneTagMatcher(["official"]);

		ITitleComparisonNormalizer first = TitleComparisonNormalizerProvider.Get(firstMatcher);
		ITitleComparisonNormalizer second = TitleComparisonNormalizerProvider.Get(secondMatcher);

		Assert.NotSame(first, second);
	}
}
