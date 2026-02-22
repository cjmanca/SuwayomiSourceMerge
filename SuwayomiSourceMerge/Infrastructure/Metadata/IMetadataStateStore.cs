namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Defines persisted metadata orchestration state storage behavior.
/// </summary>
internal interface IMetadataStateStore
{
	/// <summary>
	/// Reads the current metadata state snapshot.
	/// </summary>
	/// <returns>Current metadata state snapshot.</returns>
	MetadataStateSnapshot Read();

	/// <summary>
	/// Atomically transforms the persisted metadata state snapshot.
	/// </summary>
	/// <param name="transformer">Transformation callback that receives a snapshot and returns a replacement snapshot.</param>
	void Transform(Func<MetadataStateSnapshot, MetadataStateSnapshot> transformer);
}
