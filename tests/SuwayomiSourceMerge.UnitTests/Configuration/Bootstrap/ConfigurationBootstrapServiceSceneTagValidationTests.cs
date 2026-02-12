namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.UnitTests.Configuration;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Tests scene-tag-aware cross-document manga-equivalents validation in bootstrap.
/// </summary>
public sealed class ConfigurationBootstrapServiceSceneTagValidationTests
{
	[Fact]
	public void Bootstrap_ShouldThrow_WhenTagAwareValidationFindsDuplicateCanonical()
	{
		using TemporaryDirectory tempDirectory = new();
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "scene_tags.yml"),
			"""
			tags:
			  - official
			""");
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "manga_equivalents.yml"),
			"""
			groups:
			  - canonical: Manga [Official]
			    aliases: []
			  - canonical: Manga
			    aliases: []
			""");

		ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

		ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(
			() => service.Bootstrap(tempDirectory.Path));

		Assert.Contains(exception.ValidationErrors, error => error.File == "manga_equivalents.yml" && error.Code == "CFG-MEQ-004");
	}

	[Fact]
	public void Bootstrap_ShouldSucceed_WhenSceneTagsDoNotMatchTrailingSuffixes()
	{
		using TemporaryDirectory tempDirectory = new();
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "scene_tags.yml"),
			"""
			tags:
			  - official
			""");
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "manga_equivalents.yml"),
			"""
			groups:
			  - canonical: Manga [Scanlation]
			    aliases: []
			  - canonical: Manga
			    aliases: []
			""");

		ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

		ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);

		Assert.Equal(2, result.Documents.MangaEquivalents.Groups!.Count);
	}

	[Fact]
	public void Bootstrap_ShouldSkipTagAwareValidation_WhenSceneTagsDocumentIsInvalid()
	{
		using TemporaryDirectory tempDirectory = new();
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "scene_tags.yml"),
			"""
			tags: []
			""");
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "manga_equivalents.yml"),
			"""
			groups:
			  - canonical: Manga [Official]
			    aliases: []
			  - canonical: Manga
			    aliases: []
			""");

		ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

		ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(
			() => service.Bootstrap(tempDirectory.Path));

		Assert.Contains(exception.ValidationErrors, error => error.File == "scene_tags.yml" && error.Code == "CFG-STG-001");
		Assert.DoesNotContain(exception.ValidationErrors, error => error.Code == "CFG-MEQ-004");
		Assert.DoesNotContain(exception.ValidationErrors, error => error.Code == "CFG-MEQ-005");
	}

	[Fact]
	public void Bootstrap_ShouldThrow_WhenTagAwareValidationFindsAliasConflict()
	{
		using TemporaryDirectory tempDirectory = new();
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "scene_tags.yml"),
			"""
			tags:
			  - official
			""");
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "manga_equivalents.yml"),
			"""
			groups:
			  - canonical: Manga One
			    aliases:
			      - Shared Alias [Official]
			  - canonical: Manga Two
			    aliases:
			      - Shared Alias
			""");

		ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

		ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(
			() => service.Bootstrap(tempDirectory.Path));

		Assert.Contains(exception.ValidationErrors, error => error.File == "manga_equivalents.yml" && error.Code == "CFG-MEQ-005");
	}

	[Fact]
	public void Bootstrap_ShouldSupportPunctuationOnlySceneTag_ForTagAwareComparison()
	{
		using TemporaryDirectory tempDirectory = new();
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "scene_tags.yml"),
			"""
			tags:
			  - "!!!"
			""");
		File.WriteAllText(
			Path.Combine(tempDirectory.Path, "manga_equivalents.yml"),
			"""
			groups:
			  - canonical: Manga - !!!
			    aliases: []
			  - canonical: Manga
			    aliases: []
			""");

		ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

		ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(
			() => service.Bootstrap(tempDirectory.Path));

		Assert.Contains(exception.ValidationErrors, error => error.File == "manga_equivalents.yml" && error.Code == "CFG-MEQ-004");
	}

	[Fact]
	public void Bootstrap_ShouldThrowIOException_WhenConfigRootPathIsAnExistingFile()
	{
		using TemporaryDirectory tempDirectory = new();
		string filePath = Path.Combine(tempDirectory.Path, "config-root-file");
		File.WriteAllText(filePath, "not-a-directory");

		ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

		Assert.ThrowsAny<IOException>(() => service.Bootstrap(filePath));
	}
}
