namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Defines mergerfs branch access modes used in generated branch specifications.
/// </summary>
internal enum MergerfsBranchAccessMode
{

	/// <summary>
	/// Branch is writable.
	/// </summary>
	ReadWrite,

	/// <summary>
	/// Branch is read-only.
	/// </summary>
	ReadOnly
}
