namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Validation;

public sealed class ConfigurationBootstrapExceptionTests
{
    [Fact]
    public void Constructor_ShouldStoreValidationErrors()
    {
        ValidationError error = new("settings.yml", "$.runtime.details_description_mode", "CFG-SET-005", "Allowed values: text, br, html.");

        ConfigurationBootstrapException exception = new([error]);

        ValidationError stored = Assert.Single(exception.ValidationErrors);
        Assert.Equal(error, stored);
        Assert.Equal("Configuration bootstrap failed due to validation errors.", exception.Message);
    }

    [Fact]
    public void Constructor_ShouldCopyValidationErrors_WhenInputCollectionChanges()
    {
        List<ValidationError> errors = [new("scene_tags.yml", "$.tags[0]", "CFG-STG-003", "Duplicate scene tag after normalization.")];

        ConfigurationBootstrapException exception = new(errors);
        errors.Add(new ValidationError("scene_tags.yml", "$.tags[1]", "CFG-STG-003", "Duplicate scene tag after normalization."));

        Assert.Single(exception.ValidationErrors);
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenValidationErrorsAreNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ConfigurationBootstrapException(null!));
    }
}
