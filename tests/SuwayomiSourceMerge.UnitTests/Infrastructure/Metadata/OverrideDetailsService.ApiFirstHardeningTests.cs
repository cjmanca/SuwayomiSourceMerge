namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Text.Json;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies API-first hardening behavior for <see cref="OverrideDetailsService"/>.
/// </summary>
public sealed class OverrideDetailsServiceApiFirstHardeningTests
{
	/// <summary>
	/// Verifies null MU category entries are ignored while valid genre entries are still mapped.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldIgnoreNullMuCategoryEntries()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			orderedSourceDirectoryPaths: [],
			matchedComickComic: CreateComickPayload(
				description: "Description",
				status: 1,
				authors: [new ComickCreator { Name = "Author Name" }],
				artists: [new ComickCreator { Name = "Artist Name" }],
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
					// Intentionally simulate malformed API payload entries that deserialize as null list items.
					null!,
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Pirate/s"
						},
						PositiveVote = 8,
						NegativeVote = 1
					}
				]));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Equal(
			["Action", "Pirate/s"],
			document.RootElement.GetProperty("genre").EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToArray());
	}

	/// <summary>
	/// Verifies MU category entries with missing vote fields are skipped while valid vote entries still map.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldSkipMuCategoryEntries_WhenVotesAreMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			orderedSourceDirectoryPaths: [],
			matchedComickComic: CreateComickPayload(
				description: "Description",
				status: 1,
				authors: [new ComickCreator { Name = "Author Name" }],
				artists: [new ComickCreator { Name = "Artist Name" }],
				genreMappings: [],
				muCategoryVotes:
				[
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Missing Positive Vote"
						},
						PositiveVote = null,
						NegativeVote = 1
					},
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Pirate/s"
						},
						PositiveVote = 8,
						NegativeVote = 1
					}
				]));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Equal(
			["Pirate/s"],
			document.RootElement.GetProperty("genre").EnumerateArray().Select(static value => value.GetString() ?? string.Empty).ToArray());
	}

	/// <summary>
	/// Verifies all invalid MU category vote rows are tolerated deterministically without throwing.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Failure_ShouldRemainDeterministic_WhenAllMuVotesAreInvalid()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");

		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			orderedSourceDirectoryPaths: [],
			matchedComickComic: CreateComickPayload(
				description: "Description",
				status: 1,
				authors: [new ComickCreator { Name = "Author Name" }],
				artists: [new ComickCreator { Name = "Artist Name" }],
				genreMappings: [],
				muCategoryVotes:
				[
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Missing Positive Vote"
						},
						PositiveVote = null,
						NegativeVote = 1
					},
					new ComickMuComicCategoryVote
					{
						Category = new ComickMuCategoryDescriptor
						{
							Title = "Missing Negative Vote"
						},
						PositiveVote = 3,
						NegativeVote = null
					}
				]));
		OverrideDetailsService service = new();

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Empty(document.RootElement.GetProperty("genre").EnumerateArray().ToArray());
	}

	/// <summary>
	/// Verifies fallback parsing is skipped entirely when Comick fields are complete.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Expected_ShouldSkipComicInfoFallbackParsing_WhenComickFieldsAreComplete()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		string comicInfoPath = Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml");
		WriteComicInfoFile(comicInfoPath, "<ComicInfo><Writer>Unused</Writer></ComicInfo>");

		RecordingComicInfoMetadataParser parser = new(
			[
				(comicInfoPath, new ComicInfoMetadata("Series", "Unused", "Unused", "Unused", "Action", "completed"))
			]);
		OverrideDetailsService service = new(parser);
		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			matchedComickComic: CreateComickPayload(
				description: "Comick description",
				status: 1,
				authors: [new ComickCreator { Name = "Comick Author" }],
				artists: [new ComickCreator { Name = "Comick Artist" }],
				genreMappings:
				[
					new ComickComicGenreMapping
					{
						Genre = new ComickGenreDescriptor
						{
							Name = "Adventure"
						}
					}
				]));

		OverrideDetailsResult result = service.EnsureDetailsJson(request);

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Null(result.ComicInfoXmlPath);
		Assert.Empty(parser.Attempts);
	}

	/// <summary>
	/// Verifies parsed ComicInfo path stays null when fallback parsing occurs but does not contribute values.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldKeepComicInfoPathNull_WhenFallbackDoesNotContribute()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		string comicInfoPath = Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml");
		WriteComicInfoFile(comicInfoPath, "<ComicInfo><Writer /></ComicInfo>");

		RecordingComicInfoMetadataParser parser = new(
			[
				(comicInfoPath, new ComicInfoMetadata("Series", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty))
			]);
		OverrideDetailsService service = new(parser);
		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			matchedComickComic: CreateComickPayload(
				description: "Comick description",
				status: 1,
				authors: [],
				artists: [new ComickCreator { Name = "Comick Artist" }],
				genreMappings:
				[
					new ComickComicGenreMapping
					{
						Genre = new ComickGenreDescriptor
						{
							Name = "Adventure"
						}
					}
				]));

		OverrideDetailsResult result = service.EnsureDetailsJson(request);

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Null(result.ComicInfoXmlPath);
		Assert.Single(parser.Attempts);
	}

	/// <summary>
	/// Verifies one fallback parse is reused across multiple missing fields and ComicInfo path is reported.
	/// </summary>
	[Fact]
	public void EnsureDetailsJson_Edge_ShouldReuseSingleFallbackParseAndSetComicInfoPath_WhenFallbackContributes()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverrideDirectoryPath = CreateDirectory(temporaryDirectory.Path, "override", "priority", "Manga Title");
		string sourcePath = CreateDirectory(temporaryDirectory.Path, "sources", "disk1", "Source One", "Manga Title");
		string comicInfoPath = Path.Combine(sourcePath, "Chapter 1", "ComicInfo.xml");
		WriteComicInfoFile(comicInfoPath, "<ComicInfo><Writer>Fallback Writer</Writer></ComicInfo>");

		RecordingComicInfoMetadataParser parser = new(
			[
				(comicInfoPath, new ComicInfoMetadata("Series", "Fallback Writer", "Fallback Artist", "Fallback Summary", "Drama", "completed"))
			]);
		OverrideDetailsService service = new(parser);
		OverrideDetailsRequest request = CreateRequest(
			preferredOverrideDirectoryPath,
			[preferredOverrideDirectoryPath],
			[sourcePath],
			matchedComickComic: CreateComickPayload(
				description: null,
				parsedDescription: null,
				status: null,
				authors: [],
				artists: [],
				genreMappings: []));

		OverrideDetailsResult result = service.EnsureDetailsJson(request);
		using JsonDocument document = ParseJsonFile(Path.Combine(preferredOverrideDirectoryPath, "details.json"));

		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick, result.Outcome);
		Assert.Equal(comicInfoPath, result.ComicInfoXmlPath);
		Assert.Equal("Fallback Writer", document.RootElement.GetProperty("author").GetString());
		Assert.Equal("Fallback Artist", document.RootElement.GetProperty("artist").GetString());
		Assert.Single(parser.Attempts);
	}

	/// <summary>
	/// Creates one request model for hardening tests.
	/// </summary>
	private static OverrideDetailsRequest CreateRequest(
		string preferredOverrideDirectoryPath,
		IReadOnlyList<string> allOverrideDirectoryPaths,
		IReadOnlyList<string> orderedSourceDirectoryPaths,
		ComickComicResponse matchedComickComic)
	{
		return new OverrideDetailsRequest(
			preferredOverrideDirectoryPath,
			allOverrideDirectoryPaths,
			orderedSourceDirectoryPaths,
			displayTitle: "Manga Title",
			detailsDescriptionMode: "text",
			metadataOrchestration: new MetadataOrchestrationOptions(
				TimeSpan.FromHours(24),
				null,
				TimeSpan.FromMinutes(60),
				"en",
				TimeSpan.FromMilliseconds(1000),
				TimeSpan.FromHours(24)),
			matchedComickComic);
	}

	/// <summary>
	/// Creates one Comick payload for hardening tests.
	/// </summary>
	private static ComickComicResponse CreateComickPayload(
		string? description,
		string? parsedDescription = "<p>Parsed</p>",
		int? status = 1,
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
				Title = "Main Title",
				Description = description,
				ParsedDescription = parsedDescription,
				Status = status,
				Iso6391 = "ja",
				Links = new ComickComicLinks(),
				Statistics = [],
				Recommendations = [],
				RelateFrom = [],
				MdTitles = [new ComickTitleAlias { Title = "Alt Title", Language = "en" }],
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
	private static JsonDocument ParseJsonFile(string jsonPath)
	{
		return JsonDocument.Parse(File.ReadAllText(jsonPath));
	}

	private sealed class RecordingComicInfoMetadataParser : IComicInfoMetadataParser
	{
		private readonly IReadOnlyDictionary<string, ComicInfoMetadata?> _resultsByPath;

		public RecordingComicInfoMetadataParser(IReadOnlyList<(string Path, ComicInfoMetadata? Metadata)> resultsByPath)
		{
			_resultsByPath = resultsByPath.ToDictionary(static entry => entry.Path, static entry => entry.Metadata, StringComparer.Ordinal);
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
