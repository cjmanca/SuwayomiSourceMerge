namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies mutable runtime manga-equivalence catalog behavior.
/// </summary>
public sealed partial class MangaEquivalenceCatalogTests
{
	/// <summary>
	/// Verifies successful persistence refreshes resolver behavior immediately in-process.
	/// </summary>
	[Fact]
	public void Update_Expected_ShouldApplyPersistedChangesImmediatelyWithoutRestart()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaEquivalentsYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Manga Alpha", "Manga Alpha"));
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		MangaEquivalenceCatalog catalog = new(CreateInitialDocument(), sceneTagMatcher);

		bool wasResolvedBefore = catalog.TryResolveCanonicalTitle("Alpha Prime", out _);

		MangaEquivalenceCatalogUpdateResult result = catalog.Update(
			CreateRequest(
				mangaEquivalentsYamlPath,
				"Manga Alpha",
				"en",
				("Alpha Prime", "en")));
		bool wasResolvedAfter = catalog.TryResolveCanonicalTitle("Alpha Prime", out string canonicalAfter);

		Assert.False(wasResolvedBefore);
		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.Applied, result.Outcome);
		Assert.Equal(MangaEquivalentsUpdateOutcome.UpdatedExistingGroup, result.UpdateResult.Outcome);
		Assert.True(wasResolvedAfter);
		Assert.Equal("Manga Alpha", canonicalAfter);
	}

	/// <summary>
	/// Verifies catalog equivalent-title lookups are resolved from the active runtime snapshot.
	/// </summary>
	[Fact]
	public void TryGetEquivalentTitles_Expected_ShouldReturnCanonicalAndAliases_WhenInputIsMapped()
	{
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		MangaEquivalenceCatalog catalog = new(CreateInitialDocument(), sceneTagMatcher);

		bool wasResolved = catalog.TryGetEquivalentTitles("Manga Alpha", out IReadOnlyList<string> equivalentTitles);

		Assert.True(wasResolved);
		Assert.Equal(["Manga Alpha", "Manga Alpha Variant"], equivalentTitles);
	}

	/// <summary>
	/// Verifies no-change updater outcomes do not replace the runtime resolver snapshot.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldKeepResolverUnchangedWhenUpdaterReportsNoChanges()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaEquivalentsYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Manga Alpha", "Manga Alpha"));
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		MangaEquivalenceCatalog catalog = new(CreateInitialDocument(), sceneTagMatcher);

		MangaEquivalenceCatalogUpdateResult result = catalog.Update(
			CreateRequest(
				mangaEquivalentsYamlPath,
				"Manga Alpha",
				"en",
				("Manga Alpha", "en")));
		bool wasResolved = catalog.TryResolveCanonicalTitle("Alpha Prime", out _);

		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.NoChanges, result.Outcome);
		Assert.Equal(MangaEquivalentsUpdateOutcome.NoChanges, result.UpdateResult.Outcome);
		Assert.False(wasResolved);
	}

	/// <summary>
	/// Verifies updater failures preserve the previous runtime resolver snapshot.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldKeepResolverUnchangedWhenUpdaterFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaEquivalentsYamlPath = Path.Combine(temporaryDirectory.Path, "manga_equivalents.yml");
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		IMangaEquivalentsUpdateService updater = new StubMangaEquivalentsUpdateService(
			_ => new MangaEquivalentsUpdateResult(
				MangaEquivalentsUpdateOutcome.WriteFailed,
				mangaEquivalentsYamlPath,
				MangaEquivalentsUpdateResult.NoAffectedGroupIndex,
				0,
				"simulated write failure"));
		MangaEquivalenceCatalog catalog = new(
			CreateInitialDocument(),
			sceneTagMatcher,
			updater,
			new YamlDocumentParser());

		MangaEquivalenceCatalogUpdateResult result = catalog.Update(
			CreateRequest(
				mangaEquivalentsYamlPath,
				"Manga Alpha",
				"en",
				("Alpha Prime", "en")));
		bool wasResolved = catalog.TryResolveCanonicalTitle("Alpha Prime", out _);

		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.UpdateFailed, result.Outcome);
		Assert.Equal(MangaEquivalentsUpdateOutcome.WriteFailed, result.UpdateResult.Outcome);
		Assert.Equal("simulated write failure", result.Diagnostic);
		Assert.False(wasResolved);
	}

	/// <summary>
	/// Verifies updater exceptions are mapped to deterministic update-failed outcomes with rollback.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldReturnUpdateFailed_WhenUpdaterThrowsUnexpectedException()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaEquivalentsYamlPath = Path.Combine(temporaryDirectory.Path, "manga_equivalents.yml");
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		IMangaEquivalentsUpdateService updater = new StubMangaEquivalentsUpdateService(
			_ => throw new InvalidOperationException("simulated updater exception"));
		MangaEquivalenceCatalog catalog = new(
			CreateInitialDocument(),
			sceneTagMatcher,
			updater,
			new YamlDocumentParser());

		MangaEquivalenceCatalogUpdateResult result = catalog.Update(
			CreateRequest(
				mangaEquivalentsYamlPath,
				"Manga Alpha",
				"en",
				("Alpha Prime", "en")));
		bool wasResolved = catalog.TryResolveCanonicalTitle("Alpha Prime", out _);

		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.UpdateFailed, result.Outcome);
		Assert.Equal(MangaEquivalentsUpdateOutcome.UnhandledException, result.UpdateResult.Outcome);
		Assert.Contains("InvalidOperationException", result.Diagnostic, StringComparison.Ordinal);
		Assert.False(wasResolved);
	}

	/// <summary>
	/// Verifies persisted-success followed by reload failure preserves the previous runtime resolver snapshot.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldRollbackRuntimeSnapshotWhenReloadFailsAfterPersistenceSuccess()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string missingMangaEquivalentsYamlPath = Path.Combine(temporaryDirectory.Path, "missing_manga_equivalents.yml");
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		IMangaEquivalentsUpdateService updater = new StubMangaEquivalentsUpdateService(
			_ => new MangaEquivalentsUpdateResult(
				MangaEquivalentsUpdateOutcome.CreatedNewGroup,
				missingMangaEquivalentsYamlPath,
				affectedGroupIndex: 1,
				addedAliasCount: 1,
				diagnostic: null));
		MangaEquivalenceCatalog catalog = new(
			CreateInitialDocument(),
			sceneTagMatcher,
			updater,
			new YamlDocumentParser());

		MangaEquivalenceCatalogUpdateResult result = catalog.Update(
			CreateRequest(
				missingMangaEquivalentsYamlPath,
				"Manga Alpha",
				"en",
				("Alpha Prime", "en")));
		bool wasResolved = catalog.TryResolveCanonicalTitle("Alpha Prime", out _);

		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.ReloadFailed, result.Outcome);
		Assert.Equal(MangaEquivalentsUpdateOutcome.CreatedNewGroup, result.UpdateResult.Outcome);
		Assert.False(string.IsNullOrWhiteSpace(result.Diagnostic));
		Assert.False(wasResolved);
	}

	/// <summary>
	/// Verifies runtime catalog updates keep working when scene-tags disk file is removed after startup.
	/// </summary>
	[Fact]
	public void Update_Edge_ShouldUseStartupSceneTagMatcher_WhenSceneTagsFileIsRemovedAfterStartup()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaEquivalentsYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Manga Alpha", "Manga Alpha"));
		File.Delete(Path.Combine(temporaryDirectory.Path, "scene_tags.yml"));
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		MangaEquivalenceCatalog catalog = new(CreateInitialDocument(), sceneTagMatcher);

		MangaEquivalenceCatalogUpdateResult result = catalog.Update(
			CreateRequest(
				mangaEquivalentsYamlPath,
				"Manga Alpha",
				"en",
				("Alpha Prime", "en")));
		bool wasResolvedAfter = catalog.TryResolveCanonicalTitle("Alpha Prime", out string canonicalAfter);

		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.Applied, result.Outcome);
		Assert.True(wasResolvedAfter);
		Assert.Equal("Manga Alpha", canonicalAfter);
	}

	/// <summary>
	/// Verifies constructor guard behavior for required dependencies.
	/// </summary>
	/// <param name="constructor">Constructor invocation under test.</param>
	/// <param name="expectedParamName">Expected failing parameter name.</param>
	[Theory]
	[MemberData(nameof(GetConstructorNullGuardCases))]
	public void Constructor_Failure_ShouldThrowWhenRequiredDependencyNull(Action constructor, string expectedParamName)
	{
		ArgumentNullException exception = Assert.Throws<ArgumentNullException>(constructor);
		Assert.Equal(expectedParamName, exception.ParamName);
	}

	/// <summary>
	/// Gets constructor null-guard cases and expected parameter names.
	/// </summary>
	/// <returns>Null-guard theory rows.</returns>
	public static IEnumerable<object[]> GetConstructorNullGuardCases()
	{
		yield return
		[
			(Action)(() => new MangaEquivalenceCatalog(null!, new SceneTagMatcher(["official"]))),
			"document"
		];
		yield return
		[
			(Action)(() => new MangaEquivalenceCatalog(CreateInitialDocument(), null!)),
			"sceneTagMatcher"
		];
		yield return
		[
			(Action)(() => new MangaEquivalenceCatalog(
				CreateInitialDocument(),
				new SceneTagMatcher(["official"]),
				null!,
				new YamlDocumentParser())),
			"mangaEquivalentsUpdateService"
		];
		yield return
		[
			(Action)(() => new MangaEquivalenceCatalog(
				CreateInitialDocument(),
				new SceneTagMatcher(["official"]),
				new StubMangaEquivalentsUpdateService(
					_ => throw new InvalidOperationException("not invoked")),
				null!)),
			"yamlDocumentParser"
		];
	}

	/// <summary>
	/// Creates one update request from simplified title inputs.
	/// </summary>
	/// <param name="mangaEquivalentsYamlPath">Manga-equivalents YAML path.</param>
	/// <param name="mainTitle">Main title.</param>
	/// <param name="preferredLanguage">Preferred language code.</param>
	/// <param name="alternateTitles">Alternate title tuples.</param>
	/// <returns>Mapped update request.</returns>
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
	/// Writes config files required by updater-backed catalog tests.
	/// </summary>
	/// <param name="temporaryDirectory">Temporary directory fixture.</param>
	/// <param name="mangaEquivalentsYaml">Manga-equivalents YAML content.</param>
	/// <returns>Created manga-equivalents YAML path.</returns>
	private static string WriteConfigFiles(TemporaryDirectory temporaryDirectory, string mangaEquivalentsYaml)
	{
		ArgumentNullException.ThrowIfNull(temporaryDirectory);
		ArgumentNullException.ThrowIfNull(mangaEquivalentsYaml);

		const string sceneTagsYaml =
			"""
			tags:
			  - official
			""";

		string mangaEquivalentsYamlPath = Path.Combine(temporaryDirectory.Path, "manga_equivalents.yml");
		string sceneTagsYamlPath = Path.Combine(temporaryDirectory.Path, "scene_tags.yml");
		File.WriteAllText(mangaEquivalentsYamlPath, mangaEquivalentsYaml.ReplaceLineEndings("\n"));
		File.WriteAllText(sceneTagsYamlPath, sceneTagsYaml.ReplaceLineEndings("\n"));
		return mangaEquivalentsYamlPath;
	}

	/// <summary>
	/// Creates one minimal YAML payload with one canonical group.
	/// </summary>
	/// <param name="canonical">Canonical title.</param>
	/// <param name="aliases">Alias list.</param>
	/// <returns>YAML content.</returns>
	private static string CreateSingleGroupYaml(string canonical, params string[] aliases)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(canonical);
		ArgumentNullException.ThrowIfNull(aliases);

		string[] aliasLines = aliases
			.Select(static alias => $"      - {alias}")
			.ToArray();

		return string.Join(
			"\n",
			[
				"groups:",
				$"  - canonical: {canonical}",
				"    aliases:",
				.. aliasLines,
				string.Empty
			]);
	}

	/// <summary>
	/// Creates one initial in-memory document used as the startup snapshot.
	/// </summary>
	/// <returns>Initial document.</returns>
	private static MangaEquivalentsDocument CreateInitialDocument()
	{
		return new MangaEquivalentsDocument
		{
			Groups =
			[
				new MangaEquivalentGroup
				{
					Canonical = "Manga Alpha",
					Aliases = ["Manga Alpha Variant"]
				}
			]
		};
	}

	/// <summary>
	/// Deterministic updater test double.
	/// </summary>
	private sealed class StubMangaEquivalentsUpdateService : IMangaEquivalentsUpdateService
	{
		private readonly Func<MangaEquivalentsUpdateRequest, MangaEquivalentsUpdateResult> _update;

		/// <summary>
		/// Initializes a new instance of the <see cref="StubMangaEquivalentsUpdateService"/> class.
		/// </summary>
		/// <param name="update">Update callback.</param>
		public StubMangaEquivalentsUpdateService(Func<MangaEquivalentsUpdateRequest, MangaEquivalentsUpdateResult> update)
		{
			ArgumentNullException.ThrowIfNull(update);
			_update = update;
		}

		/// <inheritdoc />
		public MangaEquivalentsUpdateResult Update(MangaEquivalentsUpdateRequest request)
		{
			return _update(request);
		}
	}
}
