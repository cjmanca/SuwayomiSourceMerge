namespace SuwayomiSourceMerge.UnitTests.Domain.Normalization;

using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies title-key normalization behavior used by runtime and validation flows.
/// </summary>
public sealed class TitleKeyNormalizerTests
{
	[Fact]
	public void NormalizeTitleKey_ShouldNormalizeArticlesPunctuationAndPluralSuffixes()
	{
		string normalized = TitleKeyNormalizer.NormalizeTitleKey("The M\u00E4\u00F1gas: Title-s!!");

		Assert.Equal("mangatitles", normalized);
	}

	[Fact]
	public void NormalizeTitleKey_ShouldReturnEmpty_WhenInputIsWhitespace()
	{
		string normalized = TitleKeyNormalizer.NormalizeTitleKey("   ");

		Assert.Equal(string.Empty, normalized);
	}

	[Fact]
	public void NormalizeTitleKey_ShouldThrow_WhenInputIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => TitleKeyNormalizer.NormalizeTitleKey(null!));
	}

	[Fact]
	public void NormalizeTitleKey_ShouldStripTrailingSceneTag_WhenMatcherProvided()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(["official"]);

		string normalized = TitleKeyNormalizer.NormalizeTitleKey("Manga Title (Official)", matcher);

		Assert.Equal("mangatitle", normalized);
	}

	[Fact]
	public void NormalizeTitleKey_ShouldThrow_WhenMatcherThrows()
	{
		ISceneTagMatcher matcher = new ThrowingSceneTagMatcher();

		Assert.Throws<InvalidOperationException>(() => TitleKeyNormalizer.NormalizeTitleKey("Manga (Official)", matcher));
	}

	[Fact]
	public void NormalizeTokenKey_ShouldNormalizeCaseAndPunctuation()
	{
		string normalized = TitleKeyNormalizer.NormalizeTokenKey("Asura-Scan! Official");

		Assert.Equal("asura scan official", normalized);
	}

	[Fact]
	public void NormalizeTokenKey_ShouldThrow_WhenInputIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => TitleKeyNormalizer.NormalizeTokenKey(null!));
	}

	[Fact]
	public void StripTrailingSceneTagSuffixes_ShouldStripSuffixAndPreserveDisplayText()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(["official", "tapas official"]);

		string stripped = TitleKeyNormalizer.StripTrailingSceneTagSuffixes("Log Into The Future [Tapas Official]", matcher);

		Assert.Equal("Log Into The Future", stripped);
	}

	[Fact]
	public void StripTrailingSceneTagSuffixes_ShouldReturnInput_WhenMatcherIsNull()
	{
		string stripped = TitleKeyNormalizer.StripTrailingSceneTagSuffixes("Solo Leveling [Official]", sceneTagMatcher: null);

		Assert.Equal("Solo Leveling [Official]", stripped);
	}

	[Fact]
	public void StripTrailingSceneTagSuffixes_ShouldThrow_WhenInputIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => TitleKeyNormalizer.StripTrailingSceneTagSuffixes(null!, new SceneTagMatcher(["official"])));
	}
}
