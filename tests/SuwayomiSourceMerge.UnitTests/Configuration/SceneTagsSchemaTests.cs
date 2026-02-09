namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Tests scene tags schema parsing and validation rules.
/// </summary>
public sealed class SceneTagsSchemaTests
{
    [Fact]
    public void ParseSceneTags_ShouldPassForValidDocument()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SceneTagsDocument> parsed =
            service.ParseSceneTags(
                "scene_tags.yml",
                """
                tags:
                  - official
                  - color
                  - asura scan
                """);

        Assert.True(parsed.Validation.IsValid);
    }

    [Fact]
    public void ParseSceneTags_ShouldAllowWhitespaceAroundValues()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SceneTagsDocument> parsed =
            service.ParseSceneTags(
                "scene_tags.yml",
                """
                tags:
                  - " official "
                  - "colorized"
                """);

        Assert.True(parsed.Validation.IsValid);
    }

    [Fact]
    public void ParseSceneTags_ShouldFailForDuplicateNormalizedTags()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SceneTagsDocument> parsed =
            service.ParseSceneTags(
                "scene_tags.yml",
                """
                tags:
                  - asura scan
                  - Asura-Scan
                """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-STG-003", error.Code);
    }
}
