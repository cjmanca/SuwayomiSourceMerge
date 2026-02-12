using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves configured source priority values from <see cref="SourcePriorityDocument"/>.
/// </summary>
internal interface ISourcePriorityService
{
	/// <summary>
	/// Attempts to get the configured priority index for a source name.
	/// </summary>
	/// <param name="sourceName">Source name to evaluate.</param>
	/// <param name="priority">
	/// Priority index when found; otherwise <see cref="int.MaxValue"/>.
	/// Lower values indicate higher priority.
	/// </param>
	/// <returns><see langword="true"/> when a configured priority exists; otherwise <see langword="false"/>.</returns>
	bool TryGetPriority(string sourceName, out int priority);

	/// <summary>
	/// Gets the configured priority index for a source name or a provided fallback value.
	/// </summary>
	/// <param name="sourceName">Source name to evaluate.</param>
	/// <param name="unknownPriority">Fallback value returned when the source is not configured.</param>
	/// <returns>Configured priority index when found; otherwise <paramref name="unknownPriority"/>.</returns>
	int GetPriorityOrDefault(string sourceName, int unknownPriority = int.MaxValue);
}
