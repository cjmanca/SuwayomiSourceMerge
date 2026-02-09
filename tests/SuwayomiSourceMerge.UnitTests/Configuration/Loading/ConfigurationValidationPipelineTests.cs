namespace SuwayomiSourceMerge.UnitTests.Configuration.Loading;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Validation;

public sealed class ConfigurationValidationPipelineTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenParserIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationValidationPipeline(null!));
    }

    [Fact]
    public void ParseAndValidate_ShouldReturnDocument_WhenParserAndValidatorSucceed()
    {
        ConfigurationValidationPipeline pipeline = new(new YamlDocumentParser());
        RecordingValidator validator = new();

        ParsedDocument<SceneTagsDocument> parsed = pipeline.ParseAndValidate(
            "scene_tags.yml",
            """
            tags:
              - official
            """,
            validator);

        Assert.True(validator.WasCalled);
        Assert.True(parsed.Validation.IsValid);
        Assert.NotNull(parsed.Document);
    }

    [Fact]
    public void ParseAndValidate_ShouldSkipValidator_WhenParserFails()
    {
        ConfigurationValidationPipeline pipeline = new(new YamlDocumentParser());
        RecordingValidator validator = new();

        ParsedDocument<SceneTagsDocument> parsed = pipeline.ParseAndValidate(
            "scene_tags.yml",
            """
            tags:
              key: value
            """,
            validator);

        Assert.False(validator.WasCalled);
        Assert.False(parsed.Validation.IsValid);
        Assert.Contains(parsed.Validation.Errors, error => error.Code == "CFG-YAML-001");
    }

    [Fact]
    public void ParseAndValidate_ShouldThrow_WhenValidatorIsNull()
    {
        ConfigurationValidationPipeline pipeline = new(new YamlDocumentParser());

        Assert.Throws<ArgumentNullException>(
            () => pipeline.ParseAndValidate<SceneTagsDocument>(
                "scene_tags.yml",
                "tags:\n  - official\n",
                null!));
    }

    [Fact]
    public void ParseAndValidate_ShouldPropagateException_WhenValidatorThrows()
    {
        ConfigurationValidationPipeline pipeline = new(new YamlDocumentParser());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => pipeline.ParseAndValidate(
                "scene_tags.yml",
                """
                tags:
                  - official
                """,
                new ThrowingValidator()));

        Assert.Equal("validator failure", exception.Message);
    }

    [Fact]
    public void ParseAndValidate_ShouldThrow_WhenFileIsWhitespace()
    {
        ConfigurationValidationPipeline pipeline = new(new YamlDocumentParser());

        Assert.Throws<ArgumentException>(
            () => pipeline.ParseAndValidate(
                " ",
                "tags:\n  - official\n",
                new RecordingValidator()));
    }

    [Fact]
    public void ParseAndValidate_ShouldThrow_WhenYamlContentIsNull()
    {
        ConfigurationValidationPipeline pipeline = new(new YamlDocumentParser());

        Assert.Throws<ArgumentNullException>(
            () => pipeline.ParseAndValidate<SceneTagsDocument>(
                "scene_tags.yml",
                null!,
                new RecordingValidator()));
    }

    private sealed class RecordingValidator : IConfigValidator<SceneTagsDocument>
    {
        public bool WasCalled
        {
            get;
            private set;
        }

        public ValidationResult Validate(SceneTagsDocument document, string file)
        {
            WasCalled = true;
            return new ValidationResult();
        }
    }

    private sealed class ThrowingValidator : IConfigValidator<SceneTagsDocument>
    {
        public ValidationResult Validate(SceneTagsDocument document, string file)
        {
            throw new InvalidOperationException("validator failure");
        }
    }
}
