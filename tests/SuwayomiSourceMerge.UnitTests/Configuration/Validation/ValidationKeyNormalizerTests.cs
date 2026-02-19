namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

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
    public void NormalizeTitleKey_ShouldStripTrailingBracketedSceneTag_WhenTrailingPunctuationNoiseExists()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (Official)!!!", matcher);

        Assert.Equal("mangatitle", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldStripTrailingDelimitedSceneTag_WhenTrailingPunctuationNoiseExists()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title - Official!!!", matcher);

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

    [Fact]
    public void NormalizeTitleKey_ShouldStripTrailingHyphenatedSceneTag_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["asura scan"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga - Asura-Scan", matcher);

        Assert.Equal("manga", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldStripTrailingColonDelimitedSceneTagContainingColon_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["asura scan"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga: Asura:Scan", matcher);

        Assert.Equal("manga", normalized);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldStripBracketedSceneTagContainingPunctuation_WhenMatcherProvided()
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(["asura scan"]);

        string normalized = ValidationKeyNormalizer.NormalizeTitleKey("Manga [Asura-Scan]", matcher);

        Assert.Equal("manga", normalized);
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

    [Fact]
    public void NormalizeTitleKey_ShouldReuseCachedMatcherAwareNormalization_ForRepeatedInput()
    {
        CountingSceneTagMatcher matcher = new(["official"]);

        string first = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (Official)", matcher);
        int countAfterFirst = matcher.MatchCallCount;

        string second = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (Official)", matcher);

        Assert.Equal("mangatitle", first);
        Assert.Equal(first, second);
        Assert.True(countAfterFirst > 0);
        Assert.Equal(countAfterFirst, matcher.MatchCallCount);
    }

    [Fact]
    public void NormalizeTitleKey_ShouldReuseFirstComputedValue_WhenMatcherBehaviorChanges()
    {
        FlippingSceneTagMatcher matcher = new(initialResult: true);

        string first = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (Official)", matcher);
        string second = ValidationKeyNormalizer.NormalizeTitleKey("Manga Title (Official)", matcher);

        Assert.Equal("mangatitle", first);
        Assert.Equal(first, second);
        Assert.Equal(1, matcher.MatchCallCount);
    }

    [Theory]
    [MemberData(nameof(GetTagStrippingFixtures))]
    public void NormalizeTitleKey_ShouldMatchExpectedFixtureOutcomes_WhenUsingDefaultSceneTags(
        string rawTitle,
        string expectedStrippedTitle)
    {
        ISceneTagMatcher matcher = new SceneTagMatcher(SceneTagsDocumentDefaults.Create().Tags!);

        string normalizedFromRaw = ValidationKeyNormalizer.NormalizeTitleKey(rawTitle, matcher);
        string normalizedFromExpected = ValidationKeyNormalizer.NormalizeTitleKey(expectedStrippedTitle);

        Assert.Equal(normalizedFromExpected, normalizedFromRaw);
    }

    public static IEnumerable<object[]> GetTagStrippingFixtures()
    {
        yield return ["Berserk of Gluttony (Official)", "Berserk of Gluttony"];
        yield return ["Jack Be Invincible [Official]", "Jack Be Invincible"];
        yield return ["Genius Archer’s Streaming [Asura Scan]", "Genius Archer’s Streaming"];
        yield return ["I'm a Curse Crafter, and I Don't Need an S-Rank Party! [Official]", "I'm a Curse Crafter, and I Don't Need an S-Rank Party!"];
        yield return ["Log Into The Future [Tapas Official]", "Log Into The Future"];
        yield return ["Solo Farming In The Tower [All Chapters]", "Solo Farming In The Tower"];
        yield return ["The Returned C-Rank Tank Won't Die! (Valir Scans)", "The Returned C-Rank Tank Won't Die! (Valir Scans)"];
    }
}
