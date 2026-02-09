namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Tests raw YAML parser behavior.
/// </summary>
public sealed class YamlDocumentParserTests
{
    [Fact]
    public void Parse_ShouldPassForValidYamlWithUnknownFields()
    {
        YamlDocumentParser parser = new();

        ParsedDocument<SceneTagsDocument> parsed = parser.Parse<SceneTagsDocument>(
            "scene_tags.yml",
            """
            tags:
              - official
            unknown_field:
              nested: value
            """);

        Assert.True(parsed.Validation.IsValid);
        Assert.NotNull(parsed.Document);
        Assert.Equal(["official"], parsed.Document.Tags);
    }

    [Fact]
    public void Parse_ShouldFailForEmptyDocument()
    {
        YamlDocumentParser parser = new();

        ParsedDocument<SceneTagsDocument> parsed = parser.Parse<SceneTagsDocument>("scene_tags.yml", string.Empty);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-YAML-002", error.Code);
        Assert.Null(parsed.Document);
    }

    [Fact]
    public void Parse_ShouldFailForInvalidYamlShape()
    {
        YamlDocumentParser parser = new();

        ParsedDocument<SceneTagsDocument> parsed = parser.Parse<SceneTagsDocument>(
            "scene_tags.yml",
            """
            tags:
              key: value
            """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-YAML-001", error.Code);
        Assert.Null(parsed.Document);
    }

    [Fact]
    public void Parse_ShouldThrowForNullYamlContent()
    {
        YamlDocumentParser parser = new();

        Assert.Throws<ArgumentNullException>(() => parser.Parse<SceneTagsDocument>("scene_tags.yml", null!));
    }

    [Fact]
    public void Parse_ShouldThrowForWhitespaceFileName()
    {
        YamlDocumentParser parser = new();

        Assert.Throws<ArgumentException>(() => parser.Parse<SceneTagsDocument>(" ", "tags:\n  - official\n"));
    }
}
