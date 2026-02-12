using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="SceneTagsDocument"/>.
/// </summary>
/// <remarks>
/// Validation enforces that tag lists are present, non-empty, and unique by matcher-equivalent key so
/// runtime matching behavior stays deterministic.
/// </remarks>
public sealed class SceneTagsDocumentValidator : IConfigValidator<SceneTagsDocument>
{
	/// <summary>
	/// Error code emitted when <c>tags</c> is missing or empty.
	/// </summary>
	private const string MissingTagsCode = "CFG-STG-001";

	/// <summary>
	/// Error code emitted when an individual tag is empty.
	/// </summary>
	private const string EmptyTagCode = "CFG-STG-002";

	/// <summary>
	/// Error code emitted when two tags collide under matcher-equivalent semantics.
	/// </summary>
	private const string DuplicateTagCode = "CFG-STG-003";

	/// <summary>
	/// Validates configured scene tags.
	/// </summary>
	/// <param name="document">Parsed scene-tags document to validate.</param>
	/// <param name="file">Logical file name used in emitted errors.</param>
	/// <returns>A validation result containing deterministic path-scoped errors.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="file"/> is empty or whitespace.</exception>
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

		HashSet<SceneTagMatchKey> seen = [];

		for (int i = 0; i < document.Tags.Count; i++)
		{
			string? tag = document.Tags[i];
			string path = $"$.tags[{i}]";

			if (string.IsNullOrWhiteSpace(tag))
			{
				result.Add(new ValidationError(file, path, EmptyTagCode, "Tag must not be empty."));
				continue;
			}

			if (!SceneTagMatchKeyBuilder.TryCreate(tag, out SceneTagMatchKey matchKey))
			{
				result.Add(new ValidationError(file, path, EmptyTagCode, "Tag must not be empty."));
				continue;
			}

			if (!seen.Add(matchKey))
			{
				result.Add(new ValidationError(file, path, DuplicateTagCode, "Duplicate scene tag after matcher-equivalent normalization."));
			}
		}

		return result;
	}
}
