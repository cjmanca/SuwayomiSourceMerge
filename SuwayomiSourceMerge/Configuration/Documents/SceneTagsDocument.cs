namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Represents the full contents of <c>scene_tags.yml</c>.
/// </summary>
/// <remarks>
/// These values are data-driven normalization hints. Matching logic is implemented elsewhere, but this
/// document is the canonical source of configured tags.
/// </remarks>
public sealed class SceneTagsDocument
{
	/// <summary>
	/// Gets or sets scene tag phrases that should be removed during title comparison.
	/// </summary>
	/// <remarks>
	/// Tags are validated for non-empty values and normalized uniqueness, so values that differ only by
	/// punctuation/casing can still be considered duplicates.
	/// </summary>
	public List<string>? Tags
	{
		get; init;
	}
}
