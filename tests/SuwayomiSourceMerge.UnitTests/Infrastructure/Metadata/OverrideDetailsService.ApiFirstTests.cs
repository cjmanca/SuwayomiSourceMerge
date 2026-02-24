namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Text.Json;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies API-first details generation behavior for <see cref="OverrideDetailsService"/>.
/// </summary>
public sealed class OverrideDetailsServiceApiFirstTests
{
	/// <summary>
	/// Verifies Comick-first generation writes expected fields and appends language-coded title bullets.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Expected_ShouldGenerateFromComickAndAppendLanguageTitles_ForTextMode()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			orderedSourceDirectoryPaths: [],
			descriptionMode: "text",
			matchedComickComic: CreateComickPayload(
				description: "Main description",
				status: 1,
				mainTitle: "One Piece",
				mainLanguage: "ja",
				mdTitles:
				[
					new ComickTitleAlias
					{
						Title = "One Piece (English)",
						Language = "en"
					}
				],
				authors:
				[
					new ComickCreator
					{
						Name = "Author One"
					},
					new ComickCreator
					{
						Name = "Author Two"
					}
				],
				artists:
				[
					new ComickCreator
					{
						Name = "Artist One"
					}
				],
				genreMappings:
				[
					new ComickComicGenreMapping
					{
						Genre = new ComickGenreDescriptor
						{
							Name = "Action"
						}
					},
					new ComickComicGenreMapping
					{
						Genre = new ComickGenreDescriptor
						{
							Name = "Adventure"
						}
					}
				],
				muCategoryVotes:
				[
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Pirate/s"
						},
						PositiveVote = 12,
						NegativeVote = 3
					},
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Ignored Genre"
						},
						PositiveVote = 1,
						NegativeVote = 2
					}
				]));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.True(result.DetailsJsonExists);
		Assert.Equal("Author One, Author Two", document.RootElement.GetProperty("author").GetString());
		Assert.Equal("Artist One", document.RootElement.GetProperty("artist").GetString());
		Assert.Equal(
			"Main description\n\nTitles:\n- [ja] One Piece\n- [en] One Piece (English)",
			document.RootElement.GetProperty("description").GetString());
		Assert.Equal(
			["Action", "Adventure", "Pirate/s"],
			document.RootElement.GetProperty("genre").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray());
		Assert.Equal("1", document.RootElement.GetProperty("status").GetString());
	}

	/// <summary>
	/// Verifies br/html modes convert appended title-block newlines to HTML break markers.
	/// </summary>
	[Theory]
	[InlineData("br")]
	[InlineData("html")]
	public void EnsureDetailsJson_Expected_ShouldApplyDescriptionModeConversion_ForComickTitleBlock(string descriptionMode)
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			orderedSourceDirectoryPaths: [],
			descriptionMode: descriptionMode,
			matchedComickComic: CreateComickPayload(
				description: "Line1\nLine2",
				status: 1,
				mainTitle: "Main Title",
				mainLanguage: "ja",
				mdTitles:
				[
					new ComickTitleAlias
					{
						Title = "Alt Title",
						Language = "en"
					}
				]));
		OverrideDetailsService service = new();

		_ = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(
			"Line1<br />\nLine2<br />\n<br />\nTitles:<br />\n- [ja] Main Title<br />\n- [en] Alt Title",
			document.RootElement.GetProperty("description").GetString());
	}

	/// <summary>
	/// Verifies missing Comick fields fallback to ComicInfo values while still appending Comick title bullets.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldFallbackMissingFieldsToComicInfo_WhenComickFieldsAreMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		string comicInfoPath = Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml");
		WriteComicInfoFile(
			comicInfoPath,
			"""
			<ComicInfo>
			  <Writer>Fallback Writer</Writer>
			  <Penciller>Fallback Artist</Penciller>
			  <Summary>Fallback summary</Summary>
			  <Genre>Action, Drama</Genre>
			  <Status>completed</Status>
			</ComicInfo>
			""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			descriptionMode: "text",
			matchedComickComic: CreateComickPayload(
				description: null,
				parsedDescription: null,
				status: null,
				mainTitle: "Main Title",
				mainLanguage: "ja",
				mdTitles:
				[
					new ComickTitleAlias
					{
						Title = "Alt Title",
						Language = "en"
					}
				],
				authors: [],
				artists: [],
				genreMappings: [],
				muCategoryVotes: []));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Equal(comicInfoPath, result.ComicInfoXmlPath);
		Assert.Equal("Fallback Writer", document.RootElement.GetProperty("author").GetString());
		Assert.Equal("Fallback Artist", document.RootElement.GetProperty("artist").GetString());
		Assert.Equal(
			"Fallback summary\n\nTitles:\n- [ja] Main Title\n- [en] Alt Title",
			document.RootElement.GetProperty("description").GetString());
		Assert.Equal(
			["Action", "Drama"],
			document.RootElement.GetProperty("genre").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray());
		Assert.Equal("2", document.RootElement.GetProperty("status").GetString());
	}

	/// <summary>
	/// Verifies malformed and partial MU category payload entries are tolerated deterministically.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldIgnoreInvalidMuCategoryEntries_WithoutThrowing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			orderedSourceDirectoryPaths: [],
			descriptionMode: "text",
			matchedComickComic: CreateComickPayload(
				description: "Description",
				status: 1,
				mainTitle: "Main Title",
				mainLanguage: "ja",
				mdTitles: [],
				genreMappings:
				[
					new ComickComicGenreMapping
					{
						Genre = new ComickGenreDescriptor
						{
							Name = "Action"
						}
					}
				],
				muCategoryVotes:
				[
					new ComickMuComicCategoryVote
					{
						Category = null,
						PositiveVote = 10,
						NegativeVote = 1
					},
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "   "
						},
						PositiveVote = 10,
						NegativeVote = 1
					},
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Filtered Out"
						},
						PositiveVote = 2,
						NegativeVote = 2
					}
				]));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Equal(
			["Action"],
			document.RootElement.GetProperty("genre").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray());
	}

	/// <summary>
	/// Verifies existing override details short-circuit generation even when Comick payload is present.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldPreserveExistingOverrideDetails_WhenComickPayloadIsProvided()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string detailsPath = Path.Combine(preferredOverrideDirectoryPath, "details.json");
		File.WriteAllText(detailsPath, """{"existing":"yes"}""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			orderedSourceDirectoryPaths: [],
			descriptionMode: "text",
			matchedComickComic: CreateComickPayload(description: "ignored"));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);

		Assert.Equal(OverrideDetailsOutcome.AlreadyExists, result.Outcome);
		Assert.Equal("""{"existing":"yes"}""", File.ReadAllText(detailsPath));
	}

	/// <summary>
	/// Verifies source seeding remains higher priority than Comick generation.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldSeedFromSourceBeforeComickGeneration()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		string sourceDetailsPath = Path.Combine(sourcePath, "details.json");
		File.WriteAllText(sourceDetailsPath, """{"seed":"source"}""");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			descriptionMode: "text",
			matchedComickComic: CreateComickPayload(description: "ignored"));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		string writtenDetailsPath = Path.Combine(preferredOverrideDirectoryPath, "details.json");

		Assert.Equal(OverrideDetailsOutcome.SeededFromSource, result.Outcome);
		Assert.Equal(sourceDetailsPath, result.SourceDetailsJsonPath);
		Assert.Equal("""{"seed":"source"}""", File.ReadAllText(writtenDetailsPath));
	}

	/// <summary>
	/// Creates one request model for API-first tests.
	/// </summary>
	/// <param name="preferredOverrideDirectoryPath">Preferred override directory.</param>
	/// <param name="allOverrideDirectoryPaths">All override directories.</param>
	/// <param name="orderedSourceDirectoryPaths">Ordered source directories.</param>
	/// <param name="descriptionMode">Description mode.</param>
	/// <param name="matchedComickComic">Optional matched Comick payload.</param>
	/// <returns>Initialized request.</returns>
	private static OverrideDetailsRequest CreateRequest(
		string preferredOverrideDirectoryPath,
		IReadOnlyList<string> allOverrideDirectoryPaths,
		IReadOnlyList<string> orderedSourceDirectoryPaths,
		string descriptionMode,
		ComickComicResponse? matchedComickComic)
	{
		return new OverrideDetailsRequest(
			preferredOverrideDirectoryPath,
			allOverrideDirectoryPaths,
			orderedSourceDirectoryPaths,
			displayTitle: "Manga Title",
			descriptionMode,
			CreateMetadataOrchestrationOptions(),
			matchedComickComic);
	}

	/// <summary>
	/// Creates default metadata orchestration options for tests.
	/// </summary>
	/// <returns>Metadata orchestration options.</returns>
	private static MetadataOrchestrationOptions CreateMetadataOrchestrationOptions()
	{
		return new MetadataOrchestrationOptions(
			TimeSpan.FromHours(24),
			null,
			TimeSpan.FromMinutes(60),
			"en");
	}

	/// <summary>
	/// Creates one Comick payload for API-first tests.
	/// </summary>
	/// <param name="description">Optional markdown description text.</param>
	/// <param name="parsedDescription">Optional parsed HTML description text.</param>
	/// <param name="status">Optional status code.</param>
	/// <param name="mainTitle">Main title text.</param>
	/// <param name="mainLanguage">Main language code.</param>
	/// <param name="mdTitles">Alias list.</param>
	/// <param name="authors">Author list.</param>
	/// <param name="artists">Artist list.</param>
	/// <param name="genreMappings">Genre mappings.</param>
	/// <param name="muCategoryVotes">MU category votes.</param>
	/// <returns>Comick payload.</returns>
	private static ComickComicResponse CreateComickPayload(
		string? description,
		string? parsedDescription = "<p>Parsed</p>",
		int? status = 1,
		string mainTitle = "Main Title",
		string? mainLanguage = "ja",
		IReadOnlyList<ComickTitleAlias>? mdTitles = null,
		IReadOnlyList<ComickCreator>? authors = null,
		IReadOnlyList<ComickCreator>? artists = null,
		IReadOnlyList<ComickComicGenreMapping>? genreMappings = null,
		IReadOnlyList<ComickMuComicCategoryVote>? muCategoryVotes = null)
	{
		return new ComickComicResponse
		{
			Comic = new ComickComicDetails
			{
				Hid = "hid-1",
				Slug = "slug-1",
				Title = mainTitle,
				Description = description,
				ParsedDescription = parsedDescription,
				Status = status,
				Iso6391 = mainLanguage,
				Links = new ComickComicLinks(),
				Statistics = [],
				Recommendations = [],
				RelateFrom = [],
				MdTitles = mdTitles ?? [],
				MdCovers = [new ComickCover { B2Key = "cover.jpg" }],
				GenreMappings = genreMappings ?? [],
				MuComics = new ComickMuComics
				{
					MuComicCategories = muCategoryVotes
				}
			},
			Authors = authors ?? [],
			Artists = artists ?? []
		};
	}

	/// <summary>
	/// Creates one directory under one root using path segments.
	/// </summary>
	/// <param name="rootPath">Root path.</param>
	/// <param name="segments">Path segments.</param>
	/// <returns>Created directory path.</returns>
	private static string CreateDirectory(string rootPath, params string[] segments)
	{
		string path = rootPath;
		for (int index = 0; index < segments.Length; index++)
		{
			path = Path.Combine(path, segments[index]);
		}

		return Directory.CreateDirectory(path).FullName;
	}

	/// <summary>
	/// Writes one ComicInfo.xml file.
	/// </summary>
	/// <param name="comicInfoXmlPath">ComicInfo path.</param>
	/// <param name="content">XML content.</param>
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
	/// Parses one JSON file into a document.
	/// </summary>
	/// <param name="jsonPath">JSON file path.</param>
	/// <returns>JSON document.</returns>
	private static JsonDocument ParseJsonFile(string jsonPath)
	{
		return JsonDocument.Parse(File.ReadAllText(jsonPath));
	}
}
