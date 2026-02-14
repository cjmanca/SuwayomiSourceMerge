namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Tests override canonical title resolution from existing override directory names.
/// </summary>
public sealed class OverrideCanonicalResolverTests
{
	[Fact]
	public void TryResolveOverrideCanonical_ShouldReturnExactOverrideTitle_WhenMatchExists()
	{
		OverrideCanonicalResolver resolver = new(["Manga Title 1", "Another Series"]);

		bool wasResolved = resolver.TryResolveOverrideCanonical("The Manga Title 1!!!", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga Title 1", overrideCanonicalTitle);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldResolveMatcherAwareSuffixVariant_WhenMatcherProvided()
	{
		OverrideCanonicalResolver resolver = new(["Manga Title"], new SceneTagMatcher(["official"]));

		bool wasResolved = resolver.TryResolveOverrideCanonical("Manga Title [Official]", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga Title", overrideCanonicalTitle);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldReuseCachedMatcherAwareNormalization_ForRepeatedInput()
	{
		CountingSceneTagMatcher matcher = new(["official"]);
		OverrideCanonicalResolver resolver = new(["Manga Title"], matcher);

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
	public void Constructor_ShouldKeepFirstTitle_WhenNormalizedCollisionsExist()
	{
		OverrideCanonicalResolver resolver = new(["Manga-Title", "Manga Title"]);

		bool wasResolved = resolver.TryResolveOverrideCanonical("Manga Title", out string overrideCanonicalTitle);

		Assert.True(wasResolved);
		Assert.Equal("Manga-Title", overrideCanonicalTitle);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldReturnFalseAndEmpty_WhenNonEmptyTitleIsNotMapped()
	{
		OverrideCanonicalResolver resolver = new(["Manga Title 1", "Another Series"]);

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
	public void Constructor_ShouldThrow_WhenAnyExistingTitleIsWhitespace()
	{
		ArgumentException exception = Assert.Throws<ArgumentException>(() => new OverrideCanonicalResolver(["Manga Title", " "]));
		Assert.Equal("existingOverrideTitles", exception.ParamName);
	}

	[Fact]
	public void TryResolveOverrideCanonical_ShouldThrow_WhenInputTitleIsNull()
	{
		OverrideCanonicalResolver resolver = new(["Manga Title"]);

		Assert.Throws<ArgumentNullException>(() => resolver.TryResolveOverrideCanonical(null!, out _));
	}

	private sealed class CountingSceneTagMatcher : ISceneTagMatcher
	{
		private readonly ISceneTagMatcher _innerMatcher;

		public CountingSceneTagMatcher(IEnumerable<string> configuredTags)
		{
			_innerMatcher = new SceneTagMatcher(configuredTags);
		}

		public int MatchCallCount { get; private set; }

		public bool IsMatch(string candidate)
		{
			MatchCallCount++;
			return _innerMatcher.IsMatch(candidate);
		}
	}
}
