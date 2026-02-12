namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.UnitTests.Configuration;

public sealed class SceneTagsDocumentValidatorTests
{
    [Fact]
    public void Validate_ShouldPass_ForValidDocument()
    {
        SceneTagsDocumentValidator validator = new();

        ValidationResult result = validator.Validate(ConfigurationTestData.CreateValidSceneTagsDocument(), "scene_tags.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldAllow_WhitespaceAroundTagValues()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = [" official ", "asura scan"]
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldReportDeterministicError_ForDuplicateTag()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = ["asura scan", "Asura-Scan"]
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("scene_tags.yml", error.File);
        Assert.Equal("$.tags[1]", error.Path);
        Assert.Equal("CFG-STG-003", error.Code);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenDocumentIsNull()
    {
        SceneTagsDocumentValidator validator = new();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, "scene_tags.yml"));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenFileIsWhitespace()
    {
        SceneTagsDocumentValidator validator = new();

        Assert.Throws<ArgumentException>(() => validator.Validate(ConfigurationTestData.CreateValidSceneTagsDocument(), " "));
    }

    [Fact]
    public void Validate_ShouldReportMissingTags_WhenTagsListIsNull()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = null
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.tags", error.Path);
        Assert.Equal("CFG-STG-001", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportMissingTags_WhenTagsListIsEmpty()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = []
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.tags", error.Path);
        Assert.Equal("CFG-STG-001", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportEmptyTag_WhenTagIsWhitespace()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = [" "]
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.tags[0]", error.Path);
        Assert.Equal("CFG-STG-002", error.Code);
    }

    [Fact]
    public void Validate_ShouldAllowPunctuationOnlyTag()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = ["!!!"]
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldReportDuplicateTag_WhenPunctuationOnlySequenceMatchesExactly()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = ["!!!", "  !!!  "]
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.tags[1]", error.Path);
        Assert.Equal("CFG-STG-003", error.Code);
    }

    [Fact]
    public void Validate_ShouldAllowDifferentPunctuationOnlySequences()
    {
        SceneTagsDocumentValidator validator = new();
        SceneTagsDocument document = new()
        {
            Tags = ["!!!", "??!"]
        };

        ValidationResult result = validator.Validate(document, "scene_tags.yml");

        Assert.True(result.IsValid);
    }
}
