using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="SceneTagsDocument"/>.
/// </summary>
public sealed class SceneTagsDocumentValidator : IConfigValidator<SceneTagsDocument>
{
    private const string MissingTagsCode = "CFG-STG-001";
    private const string EmptyTagCode = "CFG-STG-002";
    private const string DuplicateTagCode = "CFG-STG-003";

    /// <inheritdoc />
    public ValidationResult Validate(SceneTagsDocument document, string file)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        ValidationResult result = new();

        if (document.Tags is null || document.Tags.Count == 0)
        {
            result.Add(new ValidationError(file, "$.tags", MissingTagsCode, "Tags list is required and must contain at least one item."));
            return result;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int i = 0; i < document.Tags.Count; i++)
        {
            string? tag = document.Tags[i];
            string path = $"$.tags[{i}]";

            if (string.IsNullOrWhiteSpace(tag))
            {
                result.Add(new ValidationError(file, path, EmptyTagCode, "Tag must not be empty."));
                continue;
            }

            string key = ValidationKeyNormalizer.NormalizeTokenKey(tag);
            if (!seen.Add(key))
            {
                result.Add(new ValidationError(file, path, DuplicateTagCode, "Duplicate scene tag after normalization."));
            }
        }

        return result;
    }
}
