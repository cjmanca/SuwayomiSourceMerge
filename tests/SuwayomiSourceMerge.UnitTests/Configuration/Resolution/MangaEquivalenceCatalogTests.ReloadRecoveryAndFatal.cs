namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Additional reload-recovery and fatal-exception coverage for <see cref="MangaEquivalenceCatalog"/>.
/// </summary>
public sealed partial class MangaEquivalenceCatalogTests
{
	/// <summary>
	/// Verifies fatal updater exceptions are rethrown and not mapped to deterministic failure outcomes.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldRethrowFatalException_WhenUpdaterThrowsFatalException()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaEquivalentsYamlPath = Path.Combine(temporaryDirectory.Path, "manga_equivalents.yml");
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		IMangaEquivalentsUpdateService updater = new StubMangaEquivalentsUpdateService(
			_ => throw new OutOfMemoryException("simulated fatal updater exception"));
		MangaEquivalenceCatalog catalog = new(
			CreateInitialDocument(),
			sceneTagMatcher,
			updater,
			new YamlDocumentParser());

		Assert.Throws<OutOfMemoryException>(
			() => catalog.Update(
				CreateRequest(
					mangaEquivalentsYamlPath,
					"Manga Alpha",
					"en",
					("Alpha Prime", "en"))));
	}

	/// <summary>
	/// Verifies pending reload state is repaired on a later no-change update after transient reload failure.
	/// </summary>
	[Fact]
	public void Update_Expected_ShouldRepairPendingReloadOnLaterNoChangesUpdate()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string pendingReloadPath = Path.Combine(temporaryDirectory.Path, "manga_equivalents_pending.yml");
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		int invocation = 0;
		IMangaEquivalentsUpdateService updater = new StubMangaEquivalentsUpdateService(
			_ =>
			{
				invocation++;
				return invocation == 1
					? new MangaEquivalentsUpdateResult(
						MangaEquivalentsUpdateOutcome.CreatedNewGroup,
						pendingReloadPath,
						affectedGroupIndex: 1,
						addedAliasCount: 1,
						diagnostic: null)
					: new MangaEquivalentsUpdateResult(
						MangaEquivalentsUpdateOutcome.NoChanges,
						pendingReloadPath,
						affectedGroupIndex: 1,
						addedAliasCount: 0,
						diagnostic: null);
			});
		MangaEquivalenceCatalog catalog = new(
			CreateInitialDocument(),
			sceneTagMatcher,
			updater,
			new YamlDocumentParser());

		MangaEquivalenceCatalogUpdateResult firstResult = catalog.Update(
			CreateRequest(
				pendingReloadPath,
				"Manga Alpha",
				"en",
				("Alpha Prime", "en")));
		bool wasResolvedAfterFirstUpdate = catalog.TryResolveCanonicalTitle("Alpha Prime", out _);

		File.WriteAllText(
			pendingReloadPath,
			CreateSingleGroupYaml("Manga Alpha", "Manga Alpha", "Alpha Prime").ReplaceLineEndings("\n"));

		MangaEquivalenceCatalogUpdateResult secondResult = catalog.Update(
			CreateRequest(
				pendingReloadPath,
				"Manga Alpha",
				"en",
				("Manga Alpha", "en")));
		bool wasResolvedAfterSecondUpdate = catalog.TryResolveCanonicalTitle("Alpha Prime", out string canonicalAfter);

		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.ReloadFailed, firstResult.Outcome);
		Assert.False(wasResolvedAfterFirstUpdate);
		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.NoChanges, secondResult.Outcome);
		Assert.True(wasResolvedAfterSecondUpdate);
		Assert.Equal("Manga Alpha", canonicalAfter);
	}

	/// <summary>
	/// Verifies pending reload state remains pending when later no-change refresh also fails.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldReturnReloadFailedOnNoChangesWhenPendingReloadStillFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string pendingReloadPath = Path.Combine(temporaryDirectory.Path, "manga_equivalents_pending.yml");
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(["official"]);
		int invocation = 0;
		IMangaEquivalentsUpdateService updater = new StubMangaEquivalentsUpdateService(
			_ =>
			{
				invocation++;
				return invocation == 1
					? new MangaEquivalentsUpdateResult(
						MangaEquivalentsUpdateOutcome.CreatedNewGroup,
						pendingReloadPath,
						affectedGroupIndex: 1,
						addedAliasCount: 1,
						diagnostic: null)
					: new MangaEquivalentsUpdateResult(
						MangaEquivalentsUpdateOutcome.NoChanges,
						pendingReloadPath,
						affectedGroupIndex: 1,
						addedAliasCount: 0,
						diagnostic: null);
			});
		MangaEquivalenceCatalog catalog = new(
			CreateInitialDocument(),
			sceneTagMatcher,
			updater,
			new YamlDocumentParser());

		MangaEquivalenceCatalogUpdateResult firstResult = catalog.Update(
			CreateRequest(
				pendingReloadPath,
				"Manga Alpha",
				"en",
				("Alpha Prime", "en")));
		MangaEquivalenceCatalogUpdateResult secondResult = catalog.Update(
			CreateRequest(
				pendingReloadPath,
				"Manga Alpha",
				"en",
				("Manga Alpha", "en")));
		bool wasResolved = catalog.TryResolveCanonicalTitle("Alpha Prime", out _);

		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.ReloadFailed, firstResult.Outcome);
		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.ReloadFailed, secondResult.Outcome);
		Assert.False(wasResolved);
	}
}
