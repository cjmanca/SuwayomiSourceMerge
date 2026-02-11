using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="MangaEquivalentsDocument"/>.
/// </summary>
/// <remarks>
/// Validation ensures group structure is complete and alias mappings are unambiguous after title
/// normalization so canonical grouping remains deterministic.
/// </remarks>
public sealed class MangaEquivalentsDocumentValidator : IConfigValidator<MangaEquivalentsDocument>
{
	/// <summary>
	/// Error code emitted when <c>groups</c> is missing.
	/// </summary>
	private const string MissingGroupsCode = "CFG-MEQ-001";

	/// <summary>
	/// Error code emitted when a group's canonical value is missing.
	/// </summary>
	private const string MissingCanonicalCode = "CFG-MEQ-002";

	/// <summary>
	/// Error code emitted when a group's aliases list is missing.
	/// </summary>
	private const string MissingAliasesCode = "CFG-MEQ-003";

	/// <summary>
	/// Error code emitted when canonical titles collide after normalization.
	/// </summary>
	private const string DuplicateCanonicalCode = "CFG-MEQ-004";

	/// <summary>
	/// Error code emitted when one normalized alias maps to different canonicals.
	/// </summary>
	private const string ConflictingAliasCode = "CFG-MEQ-005";

	/// <summary>
	/// Error code emitted when an alias is empty before or after normalization.
	/// </summary>
	private const string EmptyAliasCode = "CFG-MEQ-006";

	/// <summary>
	/// Validates manga equivalence groups and alias mapping integrity.
	/// </summary>
	/// <param name="document">Parsed manga equivalence document to validate.</param>
	/// <param name="file">Logical file name used in emitted errors.</param>
	/// <returns>A validation result containing deterministic path-scoped errors.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="file"/> is empty or whitespace.</exception>
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

	/// <summary>
	/// Registers one normalized alias key and reports conflicts against previously seen mappings.
	/// </summary>
	/// <param name="aliasKey">Normalized alias key.</param>
	/// <param name="canonical">Canonical title currently being processed.</param>
	/// <param name="file">Logical file name used in emitted errors.</param>
	/// <param name="path">Logical YAML/JSON path for diagnostics.</param>
	/// <param name="canonicalByAlias">Lookup storing the first canonical mapped for each alias key.</param>
	/// <param name="result">Validation collector that receives conflict/empty-alias errors.</param>
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
