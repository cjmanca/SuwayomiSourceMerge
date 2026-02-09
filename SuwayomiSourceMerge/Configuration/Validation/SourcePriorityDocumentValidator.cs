using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="SourcePriorityDocument"/>.
/// </summary>
public sealed class SourcePriorityDocumentValidator : IConfigValidator<SourcePriorityDocument>
{
    private const string MissingSourcesCode = "CFG-SRC-001";
    private const string EmptySourceCode = "CFG-SRC-002";
    private const string DuplicateSourceCode = "CFG-SRC-003";

    /// <inheritdoc />
    public ValidationResult Validate(SourcePriorityDocument document, string file)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        ValidationResult result = new();

        if (document.Sources is null)
        {
            result.Add(new ValidationError(file, "$.sources", MissingSourcesCode, "Sources list is required."));
            return result;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 0; i < document.Sources.Count; i++)
        {
            string? sourceName = document.Sources[i];
            string path = $"$.sources[{i}]";

            if (string.IsNullOrWhiteSpace(sourceName))
            {
                result.Add(new ValidationError(file, path, EmptySourceCode, "Source name must not be empty."));
                continue;
            }

            string key = ValidationKeyNormalizer.NormalizeTokenKey(sourceName);
            if (!seen.Add(key))
            {
                result.Add(new ValidationError(file, path, DuplicateSourceCode, "Duplicate source name after normalization."));
            }
        }

        return result;
    }
}
