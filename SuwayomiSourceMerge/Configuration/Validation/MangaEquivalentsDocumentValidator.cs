using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="MangaEquivalentsDocument"/>.
/// </summary>
public sealed class MangaEquivalentsDocumentValidator : IConfigValidator<MangaEquivalentsDocument>
{
    private const string MissingGroupsCode = "CFG-MEQ-001";
    private const string MissingCanonicalCode = "CFG-MEQ-002";
    private const string MissingAliasesCode = "CFG-MEQ-003";
    private const string DuplicateCanonicalCode = "CFG-MEQ-004";
    private const string ConflictingAliasCode = "CFG-MEQ-005";
    private const string EmptyAliasCode = "CFG-MEQ-006";

    /// <inheritdoc />
    public ValidationResult Validate(MangaEquivalentsDocument document, string file)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        ValidationResult result = new();

        if (document.Groups is null)
        {
            result.Add(new ValidationError(file, "$.groups", MissingGroupsCode, "Groups list is required."));
            return result;
        }

        Dictionary<string, string> canonicalByAlias = new(StringComparer.Ordinal);
        HashSet<string> canonicalKeys = new(StringComparer.Ordinal);

        for (int i = 0; i < document.Groups.Count; i++)
        {
            MangaEquivalentGroup group = document.Groups[i];
            string groupPath = $"$.groups[{i}]";

            if (string.IsNullOrWhiteSpace(group.Canonical))
            {
                result.Add(new ValidationError(file, $"{groupPath}.canonical", MissingCanonicalCode, "Canonical title is required."));
                continue;
            }

            if (group.Aliases is null)
            {
                result.Add(new ValidationError(file, $"{groupPath}.aliases", MissingAliasesCode, "Aliases list is required."));
                continue;
            }

            string canonical = group.Canonical.Trim();
            string canonicalKey = ValidationKeyNormalizer.NormalizeTitleKey(canonical);

            if (!canonicalKeys.Add(canonicalKey))
            {
                result.Add(new ValidationError(file, $"{groupPath}.canonical", DuplicateCanonicalCode, "Duplicate canonical entry after normalization."));
            }

            RegisterAlias(canonicalKey, canonical, file, $"{groupPath}.canonical", canonicalByAlias, result);

            for (int aliasIndex = 0; aliasIndex < group.Aliases.Count; aliasIndex++)
            {
                string? alias = group.Aliases[aliasIndex];
                string aliasPath = $"{groupPath}.aliases[{aliasIndex}]";

                if (string.IsNullOrWhiteSpace(alias))
                {
                    result.Add(new ValidationError(file, aliasPath, EmptyAliasCode, "Alias must not be empty."));
                    continue;
                }

                RegisterAlias(
                    ValidationKeyNormalizer.NormalizeTitleKey(alias),
                    canonical,
                    file,
                    aliasPath,
                    canonicalByAlias,
                    result);
            }
        }

        return result;
    }

    private static void RegisterAlias(
        string aliasKey,
        string canonical,
        string file,
        string path,
        IDictionary<string, string> canonicalByAlias,
        ValidationResult result)
    {
        if (string.IsNullOrEmpty(aliasKey))
        {
            result.Add(new ValidationError(file, path, EmptyAliasCode, "Alias becomes empty after normalization."));
            return;
        }

        if (!canonicalByAlias.TryGetValue(aliasKey, out string? existingCanonical))
        {
            canonicalByAlias[aliasKey] = canonical;
            return;
        }

        if (!string.Equals(existingCanonical, canonical, StringComparison.Ordinal))
        {
            result.Add(new ValidationError(file, path, ConflictingAliasCode, "Alias maps to conflicting canonical values."));
        }
    }
}
