namespace SuwayomiSourceMerge.UnitTests.Domain.Normalization;

using SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Verifies shared source-name key normalization behavior.
/// </summary>
public sealed class SourceNameKeyNormalizerTests
{
	[Fact]
	public void NormalizeSourceKey_ShouldTrimAndLowerCase()
	{
		string key = SourceNameKeyNormalizer.NormalizeSourceKey("  Source Name  ");

		Assert.Equal("source name", key);
	}

	[Fact]
	public void NormalizeSourceKey_ShouldReturnEmpty_WhenInputIsWhitespace()
	{
		string key = SourceNameKeyNormalizer.NormalizeSourceKey("   ");

		Assert.Equal(string.Empty, key);
	}

	[Fact]
	public void NormalizeSourceKey_ShouldThrow_WhenInputIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => SourceNameKeyNormalizer.NormalizeSourceKey(null!));
	}
}
