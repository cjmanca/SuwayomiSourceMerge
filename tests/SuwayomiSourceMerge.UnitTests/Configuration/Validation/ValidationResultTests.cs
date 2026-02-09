namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Validation;

public sealed class ValidationResultTests
{
    [Fact]
    public void Add_ShouldAppendError_AndSetIsValidToFalse()
    {
        ValidationResult result = new();

        result.Add(new ValidationError("settings.yml", "$.runtime.low_priority", "CFG-SET-002", "Required field is missing."));

        ValidationError error = Assert.Single(result.Errors);
        Assert.False(result.IsValid);
        Assert.Equal("CFG-SET-002", error.Code);
    }

    [Fact]
    public void AddRange_ShouldRemainValid_WhenOtherResultHasNoErrors()
    {
        ValidationResult target = new();
        ValidationResult source = new();

        target.AddRange(source);

        Assert.True(target.IsValid);
        Assert.Empty(target.Errors);
    }

    [Fact]
    public void Add_ShouldThrow_WhenErrorIsNull()
    {
        ValidationResult result = new();

        Assert.Throws<ArgumentNullException>(() => result.Add(null!));
    }

    [Fact]
    public void AddRange_ShouldThrow_WhenOtherResultIsNull()
    {
        ValidationResult result = new();

        Assert.Throws<ArgumentNullException>(() => result.AddRange(null!));
    }
}
