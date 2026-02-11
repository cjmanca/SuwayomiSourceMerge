using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="SourcePriorityDocument"/>.
/// </summary>
/// <remarks>
/// Validation enforces that explicit source ordering is structurally valid and has no normalized
/// duplicates that could produce ambiguous precedence decisions.
/// </remarks>
public sealed class SourcePriorityDocumentValidator : IConfigValidator<SourcePriorityDocument>
{
	/// <summary>
	/// Error code emitted when <c>sources</c> is missing.
	/// </summary>
	private const string MissingSourcesCode = "CFG-SRC-001";

	/// <summary>
	/// Error code emitted when a source name entry is empty.
	/// </summary>
	private const string EmptySourceCode = "CFG-SRC-002";

	/// <summary>
	/// Error code emitted when two source names collide after normalization.
	/// </summary>
	private const string DuplicateSourceCode = "CFG-SRC-003";

	/// <summary>
	/// Validates the source-priority document.
	/// </summary>
	/// <param name="document">Parsed source-priority document to validate.</param>
	/// <param name="file">Logical file name used in emitted errors.</param>
	/// <returns>A validation result containing deterministic path-scoped errors.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="file"/> is empty or whitespace.</exception>
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
