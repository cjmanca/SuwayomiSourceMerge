namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.Domain.Normalization;

public sealed class ValidationKeyNormalizerTests
{
    [Fact]
    public void NormalizeTitleKey_ShouldNormalizeArticlesPunctuationAndPluralSuffixes()
    {
        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("The Mäñgas: Title-s!!");

        Assert.Equal("mangatitles", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldReturnEmpty_WhenInputIsWhitespace()
    {
        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("   ");

        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldThrow_WhenInputIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ValidationKeyNormalizer.NormalizeTitleKey(null!));
    }

    [Fact]
    public void NormalizeTitleKey_WithMatcherOverload_ShouldThrow_WhenInputIsNull()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        Assert.Throws<ArgumentNullException>(() => ValidationKeyNormalizer.NormalizeTitleKey(null!, matcher));
    }

    [Fact]
    public void NormalizeTitleKey_WithNullMatcher_ShouldMatchBaseOverload()
    {
        const string input = "The Manga Title-s!";

        string expected = ValidationKeyNormalizer.NormalizeTitleKey(input);
        string actual = ValidationKeyNormalizer.NormalizeTitleKey(input, sceneTagMatcher: null);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void NormalizeTokenKey_ShouldNormalizeCaseAndPunctuation()
    {
        string normalized = ValidationKeyNormalizer.NormalizeTokenKey("Asura-Scan! Official");

        Assert.Equal("asura scan official", normalized);
    }

    [Fact]
    public void NormalizeTokenKey_ShouldCollapseWhitespaceRuns()
    {
        string normalized = ValidationKeyNormalizer.NormalizeTokenKey("  Team   Argo\tScans ");

        Assert.Equal("team argo scans", normalized);
    }

    [Fact]
    public void NormalizeTokenKey_ShouldThrow_WhenInputIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ValidationKeyNormalizer.NormalizeTokenKey(null!));
    }

    [Fact]
    public void NormalizeTitleKey_ShouldStripTrailingBracketedSceneTag_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (Official)", matcher);

        Assert.Equal("mangatitle", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldStripTrailingSceneTagsRepeatedly_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official", "colored"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (Official) - Colored", matcher);

        Assert.Equal("mangatitle", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldPreserveNonMatchingTrailingSuffix_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title - Side Story", matcher);

        Assert.Equal("mangatitlesidestory", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldNotStripSubstringInsideWord_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Officially Manga", matcher);

        Assert.Equal("officiallymanga", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldStripTrailingColonSceneTag_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title: Official", matcher);

        Assert.Equal("mangatitle", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldStripTrailingPunctuationOnlySceneTag_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["!!!"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title - !!!", matcher);

        Assert.Equal("mangatitle", normalized);
    }

    [Theory]
    [InlineData("Manga Title (Official")]
    [InlineData("- Official")]
    [InlineData(": Official")]
    public void NormalizeTitleKey_ShouldNotStripMalformedOrLeadingDelimitedSuffixes_WhenMatcherProvided(string input)
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey(input, matcher);

        Assert.NotEqual(string.Empty, normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldPropagateMatcherExceptions_WhenMatcherThrows()
    {
        ISceneTagMatcher matcher = new ThrowingSceneTagMatcher();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (official)", matcher));

        Assert.Equal("matcher-failure", exception.Message);
    }

    private sealed class ThrowingSceneTagMatcher : ISceneTagMatcher
    {
        public bool IsMatch(string candidate)
        {
            throw new InvalidOperationException("matcher-failure");
        }
    }
}
