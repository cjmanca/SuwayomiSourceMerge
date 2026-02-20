namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Path-overlap and list-validation helpers for <see cref="SettingsDocumentValidator"/>.
/// </summary>
public sealed partial class SettingsDocumentValidator
{
	/// <summary>
	/// Validates the <c>runtime.excluded_sources</c> list for required values and normalized uniqueness.
	/// </summary>
	/// <param name="values">Source names excluded from processing.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the list in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateExcludedSources(List<string>? values, string file, string path, ValidationResult result)
	{
		if (values is null)
		{
			result.Add(new ValidationError(file, path, MissingFieldCode, "Required list is missing."));
			return;
		}

		HashSet<string> seen = new(StringComparer.Ordinal);

		for (int i = 0; i < values.Count; i++)
		{
			string? value = values[i];
			string itemPath = $"{path}[{i}]";

			if (string.IsNullOrWhiteSpace(value))
			{
				result.Add(new ValidationError(file, itemPath, MissingFieldCode, "List item must not be empty."));
				continue;
			}

			string key = ValidationKeyNormalizer.NormalizeTokenKey(value);
			if (!seen.Add(key))
			{
				result.Add(new ValidationError(file, itemPath, DuplicateListCode, "Duplicate excluded source value."));
			}
		}
	}

	/// <summary>
	/// Validates that config and merged root paths do not overlap each other.
	/// </summary>
	/// <param name="configRootPath">Configured config root path.</param>
	/// <param name="mergedRootPath">Configured merged root path.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateNonOverlappingConfigAndMergedPaths(
		string? configRootPath,
		string? mergedRootPath,
		string file,
		ValidationResult result)
	{
		if (!TryNormalizeAbsolutePath(configRootPath, out string normalizedConfigRootPath))
		{
			return;
		}

		if (!TryNormalizeAbsolutePath(mergedRootPath, out string normalizedMergedRootPath))
		{
			return;
		}

		StringComparison pathComparison = OperatingSystem.IsWindows()
			? StringComparison.OrdinalIgnoreCase
			: StringComparison.Ordinal;
		bool hasOverlap = string.Equals(normalizedConfigRootPath, normalizedMergedRootPath, pathComparison) ||
			IsStrictChildPath(normalizedConfigRootPath, normalizedMergedRootPath, pathComparison) ||
			IsStrictChildPath(normalizedMergedRootPath, normalizedConfigRootPath, pathComparison);
		if (!hasOverlap)
		{
			return;
		}

		const string message = "Path must not overlap with paths.merged_root_path.";
		result.Add(new ValidationError(file, "$.paths.config_root_path", OverlappingPathCode, message));
		result.Add(new ValidationError(file, "$.paths.merged_root_path", OverlappingPathCode, "Path must not overlap with paths.config_root_path."));
	}

	/// <summary>
	/// Attempts to normalize one absolute path for overlap comparisons.
	/// </summary>
	/// <param name="pathValue">Path value.</param>
	/// <param name="normalizedPathValue">Normalized full path when successful.</param>
	/// <returns><see langword="true"/> when path normalization succeeds.</returns>
	private static bool TryNormalizeAbsolutePath(string? pathValue, out string normalizedPathValue)
	{
		normalizedPathValue = string.Empty;
		if (string.IsNullOrWhiteSpace(pathValue) || !Path.IsPathRooted(pathValue))
		{
			return false;
		}

		try
		{
			normalizedPathValue = Path.GetFullPath(pathValue);
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	/// <summary>
	/// Returns whether one candidate path is a strict child of one root path.
	/// </summary>
	/// <param name="candidatePath">Candidate full path.</param>
	/// <param name="rootPath">Root full path.</param>
	/// <param name="comparison">Path comparison mode.</param>
	/// <returns><see langword="true"/> when <paramref name="candidatePath"/> is inside <paramref name="rootPath"/> but not equal to it.</returns>
	private static bool IsStrictChildPath(string candidatePath, string rootPath, StringComparison comparison)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

		if (string.Equals(candidatePath, rootPath, comparison))
		{
			return false;
		}

		string strictChildPrefix = rootPath + Path.DirectorySeparatorChar;
		return candidatePath.StartsWith(strictChildPrefix, comparison);
	}
}
