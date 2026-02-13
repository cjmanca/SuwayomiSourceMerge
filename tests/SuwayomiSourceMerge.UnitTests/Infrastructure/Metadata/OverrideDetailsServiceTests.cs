namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Text.Json;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies details.json seeding/generation outcomes and guard behavior.
/// </summary>
public sealed class OverrideDetailsServiceTests
{

	/// <summary>
	/// Confirms fast-path ComicInfo selection honors caller-provided source ordering.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Expected_ShouldHonorSourceOrder_ForFastPathCandidates()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string preferredSourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk-z", "Source Z", "Manga Title");
		string secondarySourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk-a", "Source A", "Manga Title");

		WriteComicInfoFile(
			Path.Combine(preferredSourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo><Writer>Preferred Writer</Writer></ComicInfo>
			""");
		WriteComicInfoFile(
			Path.Combine(secondarySourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo><Writer>Secondary Writer</Writer></ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[preferredSourcePath, secondarySourcePath]);
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComicInfo, result.Outcome);
		Assert.True(result.DetailsJsonExists);
		Assert.StartsWith(preferredSourcePath, result.ComicInfoXmlPath, StringComparison.Ordinal);
		Assert.Equal("Preferred Writer", document.RootElement.GetProperty("author").GetString());
	}

	/// <summary>
	/// Confirms fast-path generation continues to later sources when an earlier source candidate fails parsing.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldContinueToLaterSources_WhenEarlierFastPathCandidateFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string failingSourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		string validSourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk2", "Source Two", "Manga Title");

		WriteComicInfoFile(
			Path.Combine(failingSourcePath, "Chapter 1", "ComicInfo.xml"),
			"not xml");
		WriteComicInfoFile(
			Path.Combine(validSourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo><Writer>Recovered Writer</Writer></ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[failingSourcePath, validSourcePath]);
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComicInfo, result.Outcome);
		Assert.True(result.DetailsJsonExists);
		Assert.StartsWith(validSourcePath, result.ComicInfoXmlPath, StringComparison.Ordinal);
		Assert.Equal("Recovered Writer", document.RootElement.GetProperty("author").GetString());
	}

	/// <summary>
	/// Confirms constructor argument validation rejects null parser dependencies.
	/// </summary>
	[Fact]
	public void Constructor_Exception_ShouldThrow_WhenParserIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => new OverrideDetailsService(null!));
	}

	/// <summary>
	/// Confirms seeding copies the first source details.json when no override details file exists.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Expected_ShouldSeedFromFirstSourceDetails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourceOnePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		string sourceTwoPath = CreateDirectory(temporaryDirectory.Path, "sources", "disk2", "Source Two", "Manga Title");

		string sourceOneDetailsPath = Path.Combine(sourceOnePath, "details.json");
		File.WriteAllText(sourceOneDetailsPath, """{"seed":"first"}""");
		File.WriteAllText(Path.Combine(sourceTwoPath, "details.json"), """{"seed":"second"}""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourceOnePath, sourceTwoPath]);
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		string writtenDetailsPath = Path.Combine(preferredOverrideDirectoryPath, "details.json");

		Assert.Equal(OverrideDetailsOutcome.SeededFromSource, result.Outcome);
		Assert.Equal(writtenDetailsPath, result.DetailsJsonPath);
		Assert.True(result.DetailsJsonExists);
		Assert.Equal(sourceOneDetailsPath, result.SourceDetailsJsonPath);
		Assert.Equal("""{"seed":"first"}""", File.ReadAllText(writtenDetailsPath));
	}

	/// <summary>
	/// Confirms valid ComicInfo.xml generates shell-parity details.json and status mapping.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Expected_ShouldGenerateFromStrictComicInfo()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo>
			  <Series>Different Series Name</Series>
			  <Writer>Writer Name</Writer>
			  <Penciller>Artist Name</Penciller>
			  <Summary>Line 1&#10;Line 2</Summary>
			  <Genre>Action, Adventure ; Drama</Genre>
			  <Status>completed</Status>
			</ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			displayTitle: "Display Title");
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		string writtenDetailsPath = Path.Combine(preferredOverrideDirectoryPath, "details.json");
		using JsonDocument document = ParseJsonFile(writtenDetailsPath);

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComicInfo, result.Outcome);
		Assert.Equal(writtenDetailsPath, result.DetailsJsonPath);
		Assert.True(result.DetailsJsonExists);
		Assert.NotNull(result.ComicInfoXmlPath);
		Assert.Equal("Display Title", document.RootElement.GetProperty("title").GetString());
		Assert.Equal("Writer Name", document.RootElement.GetProperty("author").GetString());
		Assert.Equal("Artist Name", document.RootElement.GetProperty("artist").GetString());
		Assert.Equal("Line 1\nLine 2", document.RootElement.GetProperty("description").GetString());
		Assert.Equal("2", document.RootElement.GetProperty("status").GetString());
		Assert.Equal(
			["Action", "Adventure", "Drama"],
			document.RootElement.GetProperty("genre").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray());
		Assert.Equal(
			[
				"0 = Unknown",
				"1 = Ongoing",
				"2 = Completed",
				"3 = Licensed"
			],
			document.RootElement.GetProperty("_status values").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray());
	}

	/// <summary>
	/// Confirms details title always uses the supplied display title instead of ComicInfo Series.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Expected_ShouldUseDisplayTitle_WhenSeriesDiffers()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo>
			  <Series>Source Series Name</Series>
			  <Writer>Writer Name</Writer>
			</ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			displayTitle: "Canonical Display Title");
		OverrideDetailsService service = new();

		service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal("Canonical Display Title", document.RootElement.GetProperty("title").GetString());
	}

	/// <summary>
	/// Confirms ensure logic skips work when any override directory already contains details.json.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldReturnAlreadyExists_WhenAnyOverrideHasDetails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string secondaryOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "disk1", "Manga Title");
		string existingDetailsPath = Path.Combine(secondaryOverrideDirectoryPath, "details.json");
		File.WriteAllText(existingDetailsPath, """{"existing":"yes"}""");

		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo><Writer>Writer Name</Writer></ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath, secondaryOverrideDirectoryPath],
			[sourcePath]);
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);

		Assert.Equal(OverrideDetailsOutcome.AlreadyExists, result.Outcome);
		Assert.Equal(existingDetailsPath, result.DetailsJsonPath);
		Assert.True(result.DetailsJsonExists);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "details.json")));
	}

	/// <summary>
	/// Confirms text description mode preserves newline characters in details description.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldPreserveNewLines_ForTextMode()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo>
			  <Summary>Line1&lt;br /&gt;Line2&#10;Line3</Summary>
			</ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			descriptionMode: "text");
		OverrideDetailsService service = new();

		service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal("Line1\nLine2\nLine3", document.RootElement.GetProperty("description").GetString());
	}

	/// <summary>
	/// Confirms br and html description modes convert newlines to HTML break markers.
	/// </summary>
	[Theory]
	[InlineData("br")]
	[InlineData("html")]
	public void EnsureDetailsJson_Edge_ShouldConvertNewLinesToBr_ForBrAndHtmlModes(string descriptionMode)
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo>
			  <Summary>Line1&#10;Line2</Summary>
			</ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			descriptionMode: descriptionMode);
		OverrideDetailsService service = new();

		service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal("Line1<br />\nLine2", document.RootElement.GetProperty("description").GetString());
	}

	/// <summary>
	/// Confirms Genre values split by commas and semicolons and trim empty items.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldSplitGenreByCommaAndSemicolon()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo>
			  <Genre>Action, Adventure ; Drama;; , Slice of Life</Genre>
			</ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath]);
		OverrideDetailsService service = new();

		service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(
			["Action", "Adventure", "Drama", "Slice of Life"],
			document.RootElement.GetProperty("genre").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray());
	}

	/// <summary>
	/// Confirms PublishingStatusTachiyomi is used when Status is missing.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldUsePublishingStatusTachiyomiFallback_WhenStatusMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"""
			<ComicInfo>
			  <PublishingStatusTachiyomi>publishing</PublishingStatusTachiyomi>
			</ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath]);
		OverrideDetailsService service = new();

		service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal("1", document.RootElement.GetProperty("status").GetString());
	}

	/// <summary>
	/// Confirms malformed fast-path XML can still generate details via fallback parser and slow-path discovery.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Failure_ShouldRecoverWithFallbackAndSlowPathCandidate()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");

		WriteComicInfoFile(
			Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"),
			"not xml");
		WriteComicInfoFile(
			Path.Combine(sourcePath, "Bucket", "Volume", "Chapter 2", "ComicInfo.xml"),
			"""
			<ComicInfo><Writer>Fallback Writer</Writer></ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath]);
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComicInfo, result.Outcome);
		Assert.True(result.DetailsJsonExists);
		Assert.Contains("ComicInfo.xml", result.ComicInfoXmlPath, StringComparison.Ordinal);
		Assert.Equal("Fallback Writer", document.RootElement.GetProperty("author").GetString());
	}

	/// <summary>
	/// Confirms slow-path discovery does not retry a fast-path candidate that already failed parsing.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldNotRetryFastCandidate_DuringSlowPath()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");

		string fastPathCandidate = Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml");
		string deepPathCandidate = Path.Combine(sourcePath, "Bucket", "Volume", "Chapter 2", "ComicInfo.xml");

		WriteComicInfoFile(fastPathCandidate, "<ComicInfo>fast</ComicInfo>");
		WriteComicInfoFile(deepPathCandidate, "<ComicInfo>deep</ComicInfo>");

		FakeComicInfoMetadataParser parser = new(
			[
				(fastPathCandidate, null),
				(deepPathCandidate, new ComicInfoMetadata("Series", "Deep Writer", string.Empty, string.Empty, string.Empty, string.Empty))
			]);

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath]);
		OverrideDetailsService service = new(parser);

		OverrideDetailsResult result = service.EnsureDetailsJson(request);

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComicInfo, result.Outcome);
		Assert.Equal(1, parser.Attempts.Count(path => string.Equals(path, fastPathCandidate, StringComparison.Ordinal)));
		Assert.Equal(1, parser.Attempts.Count(path => string.Equals(path, deepPathCandidate, StringComparison.Ordinal)));
	}

	/// <summary>
	/// Confirms parse failure outcome is returned when ComicInfo candidates exist but all fail parsing.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Failure_ShouldReturnSkippedParseFailure_WhenAllCandidatesFail()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		WriteComicInfoFile(Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml"), "not xml");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath]);
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);

		Assert.Equal(OverrideDetailsOutcome.SkippedParseFailure, result.Outcome);
		Assert.False(result.DetailsJsonExists);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "details.json")));
	}

	/// <summary>
	/// Confirms no-candidate outcome is returned when neither source details.json nor ComicInfo.xml exists.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Failure_ShouldReturnSkippedNoComicInfo_WhenNoCandidatesExist()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath]);
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);

		Assert.Equal(OverrideDetailsOutcome.SkippedNoComicInfo, result.Outcome);
		Assert.False(result.DetailsJsonExists);
		Assert.False(File.Exists(Path.Combine(preferredOverrideDirectoryPath, "details.json")));
	}

	/// <summary>
	/// Confirms null ensure requests are rejected.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Exception_ShouldThrow_WhenRequestIsNull()
	{
		OverrideDetailsService service = new();

		Assert.Throws<ArgumentNullException>(() => service.EnsureDetailsJson(null!));
	}

	/// <summary>
	/// Confirms request construction rejects invalid values and unsupported description modes.
	/// </summary>
	[Fact]
	public void OverrideDetailsRequest_Exception_ShouldThrow_WhenArgumentsInvalid()
	{
		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideDetailsRequest(
				"",
				["/override"],
				["/source"],
				"Title",
				"text"));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideDetailsRequest(
				"/override",
				[],
				["/source"],
				"Title",
				"text"));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideDetailsRequest(
				"/override",
				["/override"],
				["/source"],
				"Title",
				"markdown"));
	}

	/// <summary>
	/// Creates a normalized request instance for tests.
	/// </summary>
	/// <param name="preferredOverrideDirectoryPath">Preferred override title directory.</param>
	/// <param name="allOverrideDirectoryPaths">All override title directories.</param>
	/// <param name="orderedSourceDirectoryPaths">Ordered source title directories.</param>
	/// <param name="displayTitle">Display title.</param>
	/// <param name="descriptionMode">Description mode.</param>
	/// <returns>Initialized request.</returns>
	private static OverrideDetailsRequest CreateRequest(
		string preferredOverrideDirectoryPath,
		IReadOnlyList<string> allOverrideDirectoryPaths,
		IReadOnlyList<string> orderedSourceDirectoryPaths,
		string displayTitle = "Manga Title",
		string descriptionMode = "text")
	{
		return new OverrideDetailsRequest(
			preferredOverrideDirectoryPath,
			allOverrideDirectoryPaths,
			orderedSourceDirectoryPaths,
			displayTitle,
			descriptionMode);
	}

	/// <summary>
	/// Creates a directory under one root using path segments.
	/// </summary>
	/// <param name="rootPath">Base directory path.</param>
	/// <param name="segments">Path segments appended under root.</param>
	/// <returns>Created directory path.</returns>
	private static string CreateDirectory(string rootPath, params string[] segments)
	{
		string path = rootPath;
		foreach (string segment in segments)
		{
			path = Path.Combine(path, segment);
		}

		return Directory.CreateDirectory(path).FullName;
	}

	/// <summary>
	/// Writes one ComicInfo.xml file and creates parent directories as needed.
	/// </summary>
	/// <param name="comicInfoXmlPath">ComicInfo.xml output path.</param>
	/// <param name="content">File content.</param>
	private static void WriteComicInfoFile(string comicInfoXmlPath, string content)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(comicInfoXmlPath);
		ArgumentNullException.ThrowIfNull(content);

		string parentPath = Path.GetDirectoryName(comicInfoXmlPath)
			?? throw new InvalidOperationException("Parent path could not be determined for ComicInfo.xml.");
		Directory.CreateDirectory(parentPath);
		File.WriteAllText(comicInfoXmlPath, content);
	}

	/// <summary>
	/// Parses one JSON file into a JSON document.
	/// </summary>
	/// <param name="jsonPath">JSON file path.</param>
	/// <returns>Parsed JSON document.</returns>
	private static JsonDocument ParseJsonFile(string jsonPath)
	{
		return JsonDocument.Parse(File.ReadAllText(jsonPath));
	}

	private sealed class FakeComicInfoMetadataParser : IComicInfoMetadataParser
	{
		private readonly IReadOnlyDictionary<string, ComicInfoMetadata?> _resultsByPath;

		public FakeComicInfoMetadataParser(IReadOnlyList<(string Path, ComicInfoMetadata? Metadata)> resultsByPath)
		{
			_resultsByPath = resultsByPath.ToDictionary(entry => entry.Path, entry => entry.Metadata, StringComparer.Ordinal);
		}

		public List<string> Attempts
		{
			get;
		} = [];

		public bool TryParse(string comicInfoXmlPath, out ComicInfoMetadata? metadata)
		{
			Attempts.Add(comicInfoXmlPath);
			if (!_resultsByPath.TryGetValue(comicInfoXmlPath, out metadata) || metadata is null)
			{
				metadata = null;
				return false;
			}

			return true;
		}
	}
}
