namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.UnitTests.Configuration;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Shared test helpers for <see cref="MangaEquivalentsUpdateService"/> behavior tests.
/// </summary>
public sealed partial class MangaEquivalentsUpdateServiceTests
{
	/// <summary>
	/// Default scene-tag YAML content used by updater tests.
	/// </summary>
	private const string DefaultSceneTagsYaml =
		"""
		tags:
		  - official
		""";

	/// <summary>
	/// Creates one update service instance.
	/// </summary>
	/// <returns>Update service.</returns>
	private static MangaEquivalentsUpdateService CreateService()
	{
		return new MangaEquivalentsUpdateService();
	}

	/// <summary>
	/// Creates one update service instance with injected atomic persistence behavior.
	/// </summary>
	/// <param name="atomicPersistence">Atomic persistence dependency.</param>
	/// <returns>Update service.</returns>
	private static MangaEquivalentsUpdateService CreateService(IMangaEquivalentsAtomicPersistence atomicPersistence)
	{
		ArgumentNullException.ThrowIfNull(atomicPersistence);
		return new MangaEquivalentsUpdateService(new YamlDocumentParser(), atomicPersistence);
	}

	/// <summary>
	/// Creates a deterministic write-failure persistence test double.
	/// </summary>
	/// <param name="diagnostic">Failure diagnostic returned by the double.</param>
	/// <returns>Persistence test double.</returns>
	private static IMangaEquivalentsAtomicPersistence CreateFailingAtomicPersistence(string diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
		return new FailingAtomicPersistence(diagnostic);
	}

	/// <summary>
	/// Creates a persistence test double that throws when called.
	/// </summary>
	/// <returns>Persistence test double.</returns>
	private static IMangaEquivalentsAtomicPersistence CreateThrowingAtomicPersistence()
	{
		return new ThrowingAtomicPersistence();
	}

	/// <summary>
	/// Creates one request from simplified test inputs.
	/// </summary>
	/// <param name="mangaEquivalentsYamlPath">Manga-equivalents file path.</param>
	/// <param name="mainTitle">Main title value.</param>
	/// <param name="preferredLanguage">Preferred language code.</param>
	/// <param name="alternateTitles">Alternate title values.</param>
	/// <returns>Update request.</returns>
	private static MangaEquivalentsUpdateRequest CreateRequest(
		string mangaEquivalentsYamlPath,
		string mainTitle,
		string preferredLanguage,
		params (string Title, string? Language)[] alternateTitles)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaEquivalentsYamlPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(mainTitle);
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);
		ArgumentNullException.ThrowIfNull(alternateTitles);

		MangaEquivalentAlternateTitle[] mappedAlternateTitles = alternateTitles
			.Select(static alternateTitle => new MangaEquivalentAlternateTitle(alternateTitle.Title, alternateTitle.Language))
			.ToArray();
		return new MangaEquivalentsUpdateRequest(
			mangaEquivalentsYamlPath,
			mainTitle,
			mappedAlternateTitles,
			preferredLanguage);
	}

	/// <summary>
	/// Creates one temporary configuration root with default scene tags and provided manga-equivalents YAML.
	/// </summary>
	/// <param name="temporaryDirectory">Temporary directory.</param>
	/// <param name="mangaEquivalentsYaml">Manga-equivalents YAML content.</param>
	/// <returns>Manga-equivalents file path.</returns>
	private static string WriteConfigFiles(TemporaryDirectory temporaryDirectory, string mangaEquivalentsYaml)
	{
		ArgumentNullException.ThrowIfNull(temporaryDirectory);
		ArgumentNullException.ThrowIfNull(mangaEquivalentsYaml);

		string mangaEquivalentsYamlPath = Path.Combine(temporaryDirectory.Path, "manga_equivalents.yml");
		string sceneTagsYamlPath = Path.Combine(temporaryDirectory.Path, "scene_tags.yml");

		File.WriteAllText(mangaEquivalentsYamlPath, mangaEquivalentsYaml.ReplaceLineEndings("\n"));
		File.WriteAllText(sceneTagsYamlPath, DefaultSceneTagsYaml.ReplaceLineEndings("\n"));
		return mangaEquivalentsYamlPath;
	}

	/// <summary>
	/// Parses and validates one manga-equivalents YAML file.
	/// </summary>
	/// <param name="mangaEquivalentsYamlPath">Manga-equivalents file path.</param>
	/// <returns>Parsed document.</returns>
	private static MangaEquivalentsDocument ParseDocument(string mangaEquivalentsYamlPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaEquivalentsYamlPath);

		string yaml = File.ReadAllText(mangaEquivalentsYamlPath);
		ConfigurationSchemaService schemaService = ConfigurationSchemaServiceFactory.CreateSchemaService();
		ParsedDocument<MangaEquivalentsDocument> parsed = schemaService.ParseMangaEquivalents(
			Path.GetFileName(mangaEquivalentsYamlPath),
			yaml);

		Assert.True(parsed.Validation.IsValid);
		Assert.NotNull(parsed.Document);
		return parsed.Document!;
	}

	/// <summary>
	/// Gets one deterministic YAML payload with one canonical group.
	/// </summary>
	/// <param name="canonical">Canonical title.</param>
	/// <param name="aliases">Alias titles.</param>
	/// <returns>YAML content.</returns>
	private static string CreateSingleGroupYaml(string canonical, params string[] aliases)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(canonical);
		ArgumentNullException.ThrowIfNull(aliases);

		string[] aliasLines = aliases
			.Select(static alias => $"      - {alias}")
			.ToArray();
		string aliasesBlock = aliasLines.Length == 0
			? "    aliases: []"
			: string.Join("\n", ["    aliases:", .. aliasLines]);

		return string.Join(
			"\n",
			[
				"groups:",
				$"  - canonical: {canonical}",
				aliasesBlock,
				string.Empty
			]);
	}

	/// <summary>
	/// Deterministic write-failure persistence test double.
	/// </summary>
	private sealed class FailingAtomicPersistence : IMangaEquivalentsAtomicPersistence
	{
		private readonly string _diagnostic;

		public FailingAtomicPersistence(string diagnostic)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);
			_diagnostic = diagnostic;
		}

		public bool TryPersistDocumentAtomically(string targetPath, MangaEquivalentsDocument document, out string? diagnostic)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
			ArgumentNullException.ThrowIfNull(document);

			diagnostic = _diagnostic;
			return false;
		}
	}

	/// <summary>
	/// Persistence test double that throws when persistence is attempted.
	/// </summary>
	private sealed class ThrowingAtomicPersistence : IMangaEquivalentsAtomicPersistence
	{
		public bool TryPersistDocumentAtomically(string targetPath, MangaEquivalentsDocument document, out string? diagnostic)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
			ArgumentNullException.ThrowIfNull(document);

			diagnostic = null;
			throw new InvalidOperationException("Persistence should not be called.");
		}
	}
}
