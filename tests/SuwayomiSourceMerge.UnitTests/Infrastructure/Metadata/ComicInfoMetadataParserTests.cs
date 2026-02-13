namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies strict and fallback ComicInfo.xml parsing behavior.
/// </summary>
public sealed class ComicInfoMetadataParserTests
{

	/// <summary>
	/// Confirms a valid ComicInfo.xml document parses through strict XML parsing.
	/// </summary>
	[Fact]
	public void TryParse_Expected_ShouldParseStrictXml()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(
			xmlPath,
			"""
			<ComicInfo>
			  <Series>Series Name</Series>
			  <Writer>Writer Name</Writer>
			  <Penciller>Penciller Name</Penciller>
			  <Summary>Line 1&#10;Line 2</Summary>
			  <Genre>Action, Drama</Genre>
			  <Status>Completed</Status>
			</ComicInfo>
			""");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.True(parsed);
		Assert.NotNull(metadata);
		Assert.Equal("Series Name", metadata.Series);
		Assert.Equal("Writer Name", metadata.Writer);
		Assert.Equal("Penciller Name", metadata.Penciller);
		Assert.Equal("Line 1\nLine 2", metadata.Summary);
		Assert.Equal("Action, Drama", metadata.Genre);
		Assert.Equal("Completed", metadata.Status);
	}

	/// <summary>
	/// Confirms strict parsing preserves inline Summary break tags for downstream description normalization.
	/// </summary>
	[Fact]
	public void TryParse_Expected_ShouldPreserveInlineBrMarkup_InStrictSummary()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(
			xmlPath,
			"""
			<ComicInfo>
			  <Summary>Line 1<br />Line 2</Summary>
			</ComicInfo>
			""");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.True(parsed);
		Assert.NotNull(metadata);
		Assert.Equal("Line 1<br />Line 2", metadata.Summary);
	}

	/// <summary>
	/// Confirms status fallback parsing uses PublishingStatusTachiyomi when Status is absent.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldUsePublishingStatusFallback_WhenStatusMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(
			xmlPath,
			"""
			<ComicInfo>
			  <Writer>Writer Name</Writer>
			  <PublishingStatusTachiyomi>ongoing</PublishingStatusTachiyomi>
			</ComicInfo>
			""");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.True(parsed);
		Assert.NotNull(metadata);
		Assert.Equal("ongoing", metadata.Status);
	}

	/// <summary>
	/// Confirms malformed XML falls back to tolerant parsing when supported tags can still be extracted.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldParseMalformedXml_WithFallbackExtractor()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(
			xmlPath,
			"""
			<ComicInfo><Writer>Writer Name</Writer><Summary>Line 1 &amp; Line 2</Summary>
			""");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.True(parsed);
		Assert.NotNull(metadata);
		Assert.Equal("Line 1 & Line 2", metadata.Summary);
	}

	/// <summary>
	/// Confirms line-scanner fallback can accumulate multi-line summary content without a closing Summary tag.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldParseUnclosedMultilineSummary_WithLineScannerFallback()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(
			xmlPath,
			"""
			<ComicInfo>
			  <Writer>Writer Name</Writer>
			  <Summary>Line 1
			  Line 2
			""");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.True(parsed);
		Assert.NotNull(metadata);
		Assert.Equal("Writer Name", metadata.Writer);
		Assert.Equal("Line 1\n  Line 2", metadata.Summary);
	}

	/// <summary>
	/// Confirms line-scanner fallback can recover scalar values when closing tags are missing.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldParseScalarWithoutClosingTag_WithLineScannerFallback()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(
			xmlPath,
			"""
			<ComicInfo>
			  <Writer>Writer Name
			""");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.True(parsed);
		Assert.NotNull(metadata);
		Assert.Equal("Writer Name", metadata.Writer);
	}

	/// <summary>
	/// Confirms malformed status input falls back to PublishingStatusTachiyomi extraction.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldUsePublishingStatusFallback_ForMalformedInput()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(
			xmlPath,
			"""
			<ComicInfo>
			  <PublishingStatusTachiyomi>licensed
			""");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.True(parsed);
		Assert.NotNull(metadata);
		Assert.Equal("licensed", metadata.Status);
	}

	/// <summary>
	/// Confirms malformed files without any supported tags fail parsing.
	/// </summary>
	[Fact]
	public void TryParse_Failure_ShouldReturnFalse_WhenNoSupportedTagsExist()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(xmlPath, "not xml");

		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.False(parsed);
		Assert.Null(metadata);
	}

	/// <summary>
	/// Confirms read/open races are treated as parse failures rather than thrown exceptions.
	/// </summary>
	[Fact]
	public void TryParse_Failure_ShouldReturnFalse_WhenComicInfoIsLocked()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string xmlPath = Path.Combine(temporaryDirectory.Path, "ComicInfo.xml");
		File.WriteAllText(xmlPath, "<ComicInfo><Writer>Writer Name</Writer></ComicInfo>");

		using FileStream lockStream = new(xmlPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
		ComicInfoMetadataParser parser = new();

		bool parsed = parser.TryParse(xmlPath, out ComicInfoMetadata? metadata);

		Assert.False(parsed);
		Assert.Null(metadata);
	}

	/// <summary>
	/// Confirms invalid parser argument values are rejected.
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void TryParse_Exception_ShouldThrow_WhenPathIsInvalid(string? invalidPath)
	{
		ComicInfoMetadataParser parser = new();

		Assert.ThrowsAny<ArgumentException>(() => parser.TryParse(invalidPath!, out _));
	}
}
