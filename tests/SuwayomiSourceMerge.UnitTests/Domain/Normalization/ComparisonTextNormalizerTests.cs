namespace SuwayomiSourceMerge.UnitTests.Domain.Normalization;

using SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Verifies shared comparison-text normalization primitives.
/// </summary>
public sealed class ComparisonTextNormalizerTests
{
	[Fact]
	public void NormalizeTokenKey_ShouldNormalizeCaseAsciiAndPunctuation()
	{
		string normalized = ComparisonTextNormalizer.NormalizeTokenKey("Asura-Scan! Official");

		Assert.Equal("asura scan official", normalized);
	}

	[Fact]
	public void NormalizeTokenKey_ShouldReturnEmpty_WhenInputIsWhitespace()
	{
		string normalized = ComparisonTextNormalizer.NormalizeTokenKey("   ");

		Assert.Equal(string.Empty, normalized);
	}

	[Fact]
	public void NormalizeTokenKey_ShouldThrow_WhenInputIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => ComparisonTextNormalizer.NormalizeTokenKey(null!));
	}

	[Fact]
	public void FoldToAscii_ShouldRemoveDiacriticMarks()
	{
		string folded = ComparisonTextNormalizer.FoldToAscii("Mäñga");

		Assert.Equal("Manga", folded);
	}

	[Fact]
	public void FoldToAscii_ShouldReturnEmpty_WhenInputIsEmpty()
	{
		string folded = ComparisonTextNormalizer.FoldToAscii(string.Empty);

		Assert.Equal(string.Empty, folded);
	}

	[Fact]
	public void FoldToAscii_ShouldThrow_WhenInputIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => ComparisonTextNormalizer.FoldToAscii(null!));
	}

	[Fact]
	public void ReplacePunctuationWithSpace_ShouldReplaceNonAlphanumericCharacters()
	{
		string replaced = ComparisonTextNormalizer.ReplacePunctuationWithSpace("Manga-Title! v2");

		Assert.Equal("Manga Title  v2", replaced);
	}

	[Fact]
	public void ReplacePunctuationWithSpace_ShouldReturnEmpty_WhenInputIsEmpty()
	{
		string replaced = ComparisonTextNormalizer.ReplacePunctuationWithSpace(string.Empty);

		Assert.Equal(string.Empty, replaced);
	}

	[Fact]
	public void ReplacePunctuationWithSpace_ShouldThrow_WhenInputIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => ComparisonTextNormalizer.ReplacePunctuationWithSpace(null!));
	}
}
