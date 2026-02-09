namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Validation;

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
}
