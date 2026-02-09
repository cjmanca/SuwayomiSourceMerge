namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Tests source priority schema parsing and validation rules.
/// </summary>
public sealed class SourcePrioritySchemaTests
{
    [Fact]
    public void ParseSourcePriority_ShouldPassForValidDocument()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SourcePriorityDocument> parsed =
            service.ParseSourcePriority(
                "source_priority.yml",
                """
                sources:
                  - Source A
                  - Source B
                """);

        Assert.True(parsed.Validation.IsValid);
    }

    [Fact]
    public void ParseSourcePriority_ShouldAllowSingleSource()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SourcePriorityDocument> parsed =
            service.ParseSourcePriority(
                "source_priority.yml",
                """
                sources:
                  - Source A
                """);

        Assert.True(parsed.Validation.IsValid);
    }

    [Fact]
    public void ParseSourcePriority_ShouldFailForDuplicateSources()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SourcePriorityDocument> parsed =
            service.ParseSourcePriority(
                "source_priority.yml",
                """
                sources:
                  - Source A
                  - source a
                """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-SRC-003", error.Code);
    }
}
