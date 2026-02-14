namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Rename;

using SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Verifies shell-parity chapter-directory sanitization behavior.
/// </summary>
/// <remarks>
/// Baseline parity follows shell v1 logic in <c>sanitize_chapter_dirname</c> and <c>is_chapterish</c>,
/// with documented docs-first embedded-token behavior retained for compact chapter names.
/// </remarks>
public sealed class ShellParityChapterRenameSanitizerTests
{
	/// <summary>
	/// Verifies numeric release-group prefixes are stripped for underscore chapter names.
	/// </summary>
	[Fact]
	public void Sanitize_Expected_ShouldStripNumericGroupPrefix_ForUnderscoreFormat()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("Team-S3_MangaChapter6");

		Assert.Equal("Team-S_MangaChapter6", sanitized);
	}

	/// <summary>
	/// Verifies numeric release-group prefixes are stripped for space-delimited chapter names.
	/// </summary>
	[Fact]
	public void Sanitize_Expected_ShouldStripNumericGroupPrefix_ForPrefixSpaceFormat()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("Asura1 Chapter 7");

		Assert.Equal("Asura Chapter 7", sanitized);
	}

	/// <summary>
	/// Verifies chapter-like numeric prefixes are sanitized per shell-style matching.
	/// </summary>
	[Fact]
	public void Sanitize_Expected_ShouldSanitizeNumericChapterPrefix()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("ch3_Chapter 10");

		Assert.Equal("ch_Chapter 10", sanitized);
	}

	/// <summary>
	/// Verifies episode numeric prefixes are sanitized per shell-style matching.
	/// </summary>
	[Fact]
	public void Sanitize_Expected_ShouldSanitizeNumericEpisodePrefix()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("ep2_Chapter 4");

		Assert.Equal("ep_Chapter 4", sanitized);
	}

	/// <summary>
	/// Verifies embedded abbreviated chapter tokens are treated as chapter-like markers.
	/// </summary>
	[Fact]
	public void Sanitize_Expected_ShouldSanitize_WhenEmbeddedAbbreviatedChapterTokenIsPresent()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("Team9_MangaCh.6");

		Assert.Equal("Team_MangaCh.6", sanitized);
	}

	/// <summary>
	/// Verifies embedded abbreviated episode tokens are treated as chapter-like markers.
	/// </summary>
	[Fact]
	public void Sanitize_Expected_ShouldSanitize_WhenEmbeddedAbbreviatedEpisodeTokenIsPresent()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("Team9_MangaEp.4");

		Assert.Equal("Team_MangaEp.4", sanitized);
	}

	/// <summary>
	/// Verifies only the first prefix token is sanitized when the underscore prefix includes spaces.
	/// </summary>
	[Fact]
	public void Sanitize_Edge_ShouldOnlyRewriteFirstPrefixToken_WhenPrefixContainsSpaces()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("Team9 Alpha_Chapter 1");

		Assert.Equal("Team Alpha_Chapter 1", sanitized);
	}

	/// <summary>
	/// Verifies non-chapter suffixes remain unchanged.
	/// </summary>
	[Fact]
	public void Sanitize_Edge_ShouldKeepOriginal_WhenSuffixIsNotChapterLike()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("Team9_Release Notes");

		Assert.Equal("Team9_Release Notes", sanitized);
	}

	/// <summary>
	/// Verifies null inputs are rejected.
	/// </summary>
	[Fact]
	public void Sanitize_Failure_ShouldThrow_WhenInputIsNull()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		Assert.Throws<ArgumentNullException>(() => sanitizer.Sanitize(null!));
	}

	/// <summary>
	/// Verifies whitespace names are returned as-is.
	/// </summary>
	[Fact]
	public void Sanitize_Edge_ShouldReturnWhitespaceInputUnchanged()
	{
		ShellParityChapterRenameSanitizer sanitizer = new();

		string sanitized = sanitizer.Sanitize("  ");

		Assert.Equal("  ", sanitized);
	}
}
