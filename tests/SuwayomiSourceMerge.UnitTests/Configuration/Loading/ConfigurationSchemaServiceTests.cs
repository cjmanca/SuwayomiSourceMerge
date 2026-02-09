namespace SuwayomiSourceMerge.UnitTests.Configuration.Loading;

using SuwayomiSourceMerge.Configuration.Loading;

public sealed class ConfigurationSchemaServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenPipelineIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationSchemaService(null!));
    }

    [Fact]
    public void ParseSettings_ShouldFail_WhenYamlShapeIsInvalid()
    {
        ConfigurationSchemaService service = new(new ConfigurationValidationPipeline(new YamlDocumentParser()));

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
            """
            paths:
              - invalid
            """);

        Assert.False(parsed.Validation.IsValid);
        Assert.Contains(parsed.Validation.Errors, error => error.Code == "CFG-YAML-001");
    }

    [Fact]
    public void ParseMangaEquivalents_ShouldFail_WhenGroupsShapeIsInvalid()
    {
        ConfigurationSchemaService service = new(new ConfigurationValidationPipeline(new YamlDocumentParser()));

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentsDocument> parsed = service.ParseMangaEquivalents(
            "manga_equivalents.yml",
            """
            groups:
              canonical: Manga
            """);

        Assert.False(parsed.Validation.IsValid);
        Assert.Contains(parsed.Validation.Errors, error => error.Code == "CFG-YAML-001");
    }

    [Fact]
    public void ParseSceneTags_ShouldFail_WhenTagsShapeIsInvalid()
    {
        ConfigurationSchemaService service = new(new ConfigurationValidationPipeline(new YamlDocumentParser()));

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SceneTagsDocument> parsed = service.ParseSceneTags(
            "scene_tags.yml",
            """
            tags:
              key: value
            """);

        Assert.False(parsed.Validation.IsValid);
        Assert.Contains(parsed.Validation.Errors, error => error.Code == "CFG-YAML-001");
    }

    [Fact]
    public void ParseSourcePriority_ShouldFail_WhenSourcesShapeIsInvalid()
    {
        ConfigurationSchemaService service = new(new ConfigurationValidationPipeline(new YamlDocumentParser()));

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SourcePriorityDocument> parsed = service.ParseSourcePriority(
            "source_priority.yml",
            """
            sources:
              key: value
            """);

        Assert.False(parsed.Validation.IsValid);
        Assert.Contains(parsed.Validation.Errors, error => error.Code == "CFG-YAML-001");
    }
}
