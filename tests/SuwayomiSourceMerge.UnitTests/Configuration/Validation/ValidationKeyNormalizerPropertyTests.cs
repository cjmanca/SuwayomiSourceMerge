namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using FsCheck;
using FsCheck.Xunit;
using SuwayomiSourceMerge.Configuration.Validation;

public sealed class ValidationKeyNormalizerPropertyTests
{
    [Property(MaxTest = 250)]
    public bool NormalizeTitleKey_ShouldOnlyContainLettersOrDigits(NonNull<string> input)
    {
        string normalized = ValidationKeyNormalizer.NormalizeTitleKey(input.Get);
        return normalized.All(character => char.IsLetterOrDigit(character));
    }

    [Property(MaxTest = 250)]
    public bool NormalizeTokenKey_ShouldBeIdempotent(NonNull<string> input)
    {
        string once = ValidationKeyNormalizer.NormalizeTokenKey(input.Get);
        string twice = ValidationKeyNormalizer.NormalizeTokenKey(once);
        return once == twice;
    }

    [Property(MaxTest = 250)]
    public bool NormalizeTokenKey_ShouldOnlyContainLettersDigitsOrSpaces(NonNull<string> input)
    {
        string normalized = ValidationKeyNormalizer.NormalizeTokenKey(input.Get);
        return normalized.All(character => char.IsLetterOrDigit(character) || character == ' ');
    }
}
