namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.UnitTests.Configuration;

public sealed class SourcePriorityDocumentValidatorTests
{
    [Fact]
    public void Validate_ShouldPass_ForValidDocument()
    {
        SourcePriorityDocumentValidator validator = new();

        ValidationResult result = validator.Validate(ConfigurationTestData.CreateValidSourcePriorityDocument(), "source_priority.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldAllow_SingleSource()
    {
        SourcePriorityDocumentValidator validator = new();
        SourcePriorityDocument document = new()
        {
            Sources = ["Source A"]
        };

        ValidationResult result = validator.Validate(document, "source_priority.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldReportDeterministicError_ForDuplicateNormalizedSource()
    {
        SourcePriorityDocumentValidator validator = new();
        SourcePriorityDocument document = new()
        {
            Sources = ["Source A", "source-a"]
        };

        ValidationResult result = validator.Validate(document, "source_priority.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("source_priority.yml", error.File);
        Assert.Equal("$.sources[1]", error.Path);
        Assert.Equal("CFG-SRC-003", error.Code);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenDocumentIsNull()
    {
        SourcePriorityDocumentValidator validator = new();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, "source_priority.yml"));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenFileIsWhitespace()
    {
        SourcePriorityDocumentValidator validator = new();

        Assert.Throws<ArgumentException>(() => validator.Validate(ConfigurationTestData.CreateValidSourcePriorityDocument(), " "));
    }

    [Fact]
    public void Validate_ShouldReportMissingSources_WhenSourcesListIsNull()
    {
        SourcePriorityDocumentValidator validator = new();
        SourcePriorityDocument document = new()
        {
            Sources = null
        };

        ValidationResult result = validator.Validate(document, "source_priority.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.sources", error.Path);
        Assert.Equal("CFG-SRC-001", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportEmptySource_WhenSourceNameIsWhitespace()
    {
        SourcePriorityDocumentValidator validator = new();
        SourcePriorityDocument document = new()
        {
            Sources = [" "]
        };

        ValidationResult result = validator.Validate(document, "source_priority.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.sources[0]", error.Path);
        Assert.Equal("CFG-SRC-002", error.Code);
    }
}
