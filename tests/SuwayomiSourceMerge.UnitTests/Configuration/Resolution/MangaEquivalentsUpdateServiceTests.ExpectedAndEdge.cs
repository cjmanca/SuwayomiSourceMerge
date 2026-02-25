namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected and edge behavior for <see cref="MangaEquivalentsUpdateService"/>.
/// </summary>
public sealed partial class MangaEquivalentsUpdateServiceTests
{
	/// <summary>
	/// Verifies missing aliases are appended to one matched group without duplicate alias writes.
	/// </summary>
	[Fact]
	public void Update_Expected_ShouldAppendMissingAliases_WhenOneGroupMatches()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Manga Alpha", "Alpha One"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Manga Alpha",
				"en",
				("Alpha One", null),
				("Alpha Two", null)));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);

		Assert.Equal(MangaEquivalentsUpdateOutcome.UpdatedExistingGroup, result.Outcome);
		Assert.Equal(0, result.AffectedGroupIndex);
		Assert.Equal(1, result.AddedAliasCount);
		Assert.Equal(["Alpha One", "Alpha Two"], document.Groups![0].Aliases);
	}

	/// <summary>
	/// Verifies a new group is created when no existing mappings match incoming titles.
	/// </summary>
	[Fact]
	public void Update_Expected_ShouldCreateNewGroup_WhenNoMatchExists()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Other Manga", "Other Alias"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"ja",
				("Alt FR", "fr")));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);
		MangaEquivalentGroup createdGroup = document.Groups![1];

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Equal(1, result.AffectedGroupIndex);
		Assert.Equal(1, result.AddedAliasCount);
		Assert.Equal("Main Title", createdGroup.Canonical);
		Assert.Equal(["Alt FR"], createdGroup.Aliases);
	}

	/// <summary>
	/// Verifies canonical selection prefers exact preferred-language alternate titles.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldSelectCanonical_FromExactPreferredLanguageAlternate()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(temporaryDirectory, CreateSingleGroupYaml("Other Manga"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"ja",
				("Alt EN", "en"),
				("Alt JA", "ja")));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);
		MangaEquivalentGroup createdGroup = document.Groups![1];

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Equal("Alt JA", createdGroup.Canonical);
		Assert.Equal(["Main Title", "Alt EN"], createdGroup.Aliases);
	}

	/// <summary>
	/// Verifies preferred-language canonical selection still uses raw alternate order when normalized dedupe drops the matching alternate.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldSelectCanonical_FromExactPreferredLanguageAlternate_WhenPreferredAlternateCollidesWithMainKey()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(temporaryDirectory, CreateSingleGroupYaml("Other Manga"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"ja",
				("Main-Title", "ja"),
				("Alt EN", "en")));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);
		MangaEquivalentGroup createdGroup = document.Groups![1];

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Equal("Main-Title", createdGroup.Canonical);
		Assert.Equal(["Alt EN"], createdGroup.Aliases);
	}

	/// <summary>
	/// Verifies preferred-language two-character fallback selects compatible alternate-language titles.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldSelectCanonical_FromPreferredLanguagePrefixFallback()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(temporaryDirectory, CreateSingleGroupYaml("Other Manga"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"zh-CN",
				("Alt ZH HK", "zh-HK"),
				("Alt EN", "en")));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);
		MangaEquivalentGroup createdGroup = document.Groups![1];

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Equal("Alt ZH HK", createdGroup.Canonical);
		Assert.Equal(["Main Title", "Alt EN"], createdGroup.Aliases);
	}

	/// <summary>
	/// Verifies preferred-language prefix canonical selection still uses raw alternate order when normalized dedupe drops the matching alternate.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldSelectCanonical_FromPreferredLanguagePrefixFallback_WhenPrefixAlternateCollidesWithMainKey()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(temporaryDirectory, CreateSingleGroupYaml("Other Manga"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"zh-CN",
				("Main.Title", "zh-HK"),
				("Alt EN", "en")));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);
		MangaEquivalentGroup createdGroup = document.Groups![1];

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Equal("Main.Title", createdGroup.Canonical);
		Assert.Equal(["Alt EN"], createdGroup.Aliases);
	}

	/// <summary>
	/// Verifies canonical selection falls back to English alternate when preferred-language matches are unavailable.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldSelectCanonical_FromEnglishFallback()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(temporaryDirectory, CreateSingleGroupYaml("Other Manga"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"fr",
				("Alt DE", "de"),
				("Alt EN", "en")));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);
		MangaEquivalentGroup createdGroup = document.Groups![1];

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Equal("Alt EN", createdGroup.Canonical);
		Assert.Equal(["Main Title", "Alt DE"], createdGroup.Aliases);
	}

	/// <summary>
	/// Verifies English fallback canonical selection still uses raw alternate order when normalized dedupe drops all alternates.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldSelectCanonical_FromEnglishFallback_WhenEnglishAlternateCollidesWithMainKey()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(temporaryDirectory, CreateSingleGroupYaml("Other Manga"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"fr",
				("Main_Title", "de"),
				("Main-Title", "en")));
		MangaEquivalentsDocument document = ParseDocument(mangaYamlPath);
		MangaEquivalentGroup createdGroup = document.Groups![1];

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Equal("Main-Title", createdGroup.Canonical);
		Assert.NotNull(createdGroup.Aliases);
		Assert.Empty(createdGroup.Aliases);
	}

	/// <summary>
	/// Verifies no-op updates do not rewrite unchanged file content.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldReturnNoChanges_AndPreserveFileContent_WhenNoMutationNeeded()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Manga Title", "Alias One"));
		string beforeContent = File.ReadAllText(mangaYamlPath);
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Manga Title",
				"en",
				("Alias One", null),
				("Manga-Title", null)));
		string afterContent = File.ReadAllText(mangaYamlPath);

		Assert.Equal(MangaEquivalentsUpdateOutcome.NoChanges, result.Outcome);
		Assert.Equal(0, result.AddedAliasCount);
		Assert.Equal(beforeContent, afterContent);
	}

	/// <summary>
	/// Verifies no-op updates bypass persistence calls.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldBypassPersistence_WhenNoMutationNeeded()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Manga Title", "Alias One"));
		string beforeContent = File.ReadAllText(mangaYamlPath);
		MangaEquivalentsUpdateService service = CreateService(CreateThrowingAtomicPersistence());

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Manga Title",
				"en",
				("Alias One", null),
				("Manga-Title", null)));
		string afterContent = File.ReadAllText(mangaYamlPath);

		Assert.Equal(MangaEquivalentsUpdateOutcome.NoChanges, result.Outcome);
		Assert.Equal(0, result.AddedAliasCount);
		Assert.Equal(beforeContent, afterContent);
	}

	/// <summary>
	/// Verifies successful writes leave no orphan temporary files.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldLeaveNoOrphanTemporaryFiles_AfterSuccessfulWrite()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(temporaryDirectory, CreateSingleGroupYaml("Other Manga"));
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Main Title",
				"en",
				("Alt EN", "en")));
		string[] temporaryFiles = Directory
			.EnumerateFiles(temporaryDirectory.Path, "*.tmp", SearchOption.TopDirectoryOnly)
			.Where(path => path.StartsWith(mangaYamlPath + ".", StringComparison.Ordinal))
			.ToArray();

		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.Outcome);
		Assert.Empty(temporaryFiles);
	}
}
