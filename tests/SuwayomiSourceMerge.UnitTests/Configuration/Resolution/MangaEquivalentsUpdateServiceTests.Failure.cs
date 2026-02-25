namespace SuwayomiSourceMerge.UnitTests.Configuration.Resolution;

using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies failure behavior for <see cref="MangaEquivalentsUpdateService"/>.
/// </summary>
public sealed partial class MangaEquivalentsUpdateServiceTests
{
	/// <summary>
	/// Verifies ambiguous multi-group matches are rejected without persistence.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldReturnConflict_WhenIncomingTitlesMatchMultipleGroups()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			"""
			groups:
			  - canonical: Manga One
			    aliases:
			      - Shared Alias
			  - canonical: Manga Two
			    aliases:
			      - Second Alias
			""");
		string beforeContent = File.ReadAllText(mangaYamlPath);
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Shared Alias",
				"en",
				("Manga Two", null)));
		string afterContent = File.ReadAllText(mangaYamlPath);

		Assert.Equal(MangaEquivalentsUpdateOutcome.Conflict, result.Outcome);
		Assert.Equal(beforeContent, afterContent);
	}

	/// <summary>
	/// Verifies strict scene-tag-aware validation failures block persistence.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldReturnValidationFailed_WhenSceneTagAwareValidationFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			"""
			groups:
			  - canonical: Manga [Official]
			    aliases: []
			  - canonical: Manga
			    aliases: []
			""");
		string beforeContent = File.ReadAllText(mangaYamlPath);
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Unrelated Title",
				"en"));
		string afterContent = File.ReadAllText(mangaYamlPath);

		Assert.Equal(MangaEquivalentsUpdateOutcome.ValidationFailed, result.Outcome);
		Assert.Contains("CFG-MEQ-004", result.Diagnostic, StringComparison.Ordinal);
		Assert.Equal(beforeContent, afterContent);
	}

	/// <summary>
	/// Verifies malformed source YAML returns deterministic validation-failure outcomes.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldReturnValidationFailed_WhenMangaYamlIsMalformed()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			"""
			groups:
			  - canonical: [
			""");
		MangaEquivalentsUpdateService service = CreateService();

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Any Title",
				"en"));

		Assert.Equal(MangaEquivalentsUpdateOutcome.ValidationFailed, result.Outcome);
		Assert.Contains("CFG-YAML-001", result.Diagnostic, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies write-stage filesystem failures return deterministic write-failure outcomes.
	/// </summary>
	[Fact]
	public void Update_Failure_ShouldReturnWriteFailed_WhenAtomicReplaceCannotWriteTarget()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mangaYamlPath = WriteConfigFiles(
			temporaryDirectory,
			CreateSingleGroupYaml("Manga Alpha", "Alias One"));
		string beforeContent = File.ReadAllText(mangaYamlPath);
		MangaEquivalentsUpdateService service = CreateService(
			CreateFailingAtomicPersistence("Injected persistence failure."));

		MangaEquivalentsUpdateResult result = service.Update(
			CreateRequest(
				mangaYamlPath,
				"Manga Alpha",
				"en",
				("Alias Two", null)));

		string afterContent = File.ReadAllText(mangaYamlPath);
		Assert.Equal(MangaEquivalentsUpdateOutcome.WriteFailed, result.Outcome);
		Assert.Equal("Injected persistence failure.", result.Diagnostic);
		Assert.Equal(beforeContent, afterContent);
	}
}
