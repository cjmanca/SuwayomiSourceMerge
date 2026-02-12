namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Represents the full contents of <c>source_priority.yml</c>.
/// </summary>
/// <remarks>
/// Source names are ordered from highest to lowest priority and are used when duplicate chapter/content
/// candidates must be resolved deterministically.
/// </remarks>
public sealed class SourcePriorityDocument
{
	/// <summary>
	/// Gets or sets source names in priority order (top to bottom).
	/// </summary>
	/// <remarks>
	/// Values are validated for normalized uniqueness. Reordering this list changes precedence behavior
	/// without changing any runtime code.
	/// </summary>
	public List<string>? Sources
	{
		get; init;
	}
}
