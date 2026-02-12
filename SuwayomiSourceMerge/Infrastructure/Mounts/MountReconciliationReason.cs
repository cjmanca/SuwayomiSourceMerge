namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Classifies why a reconciliation action was produced.
/// </summary>
internal enum MountReconciliationReason
{
	/// <summary>
	/// Desired mountpoint is missing from actual mount state.
	/// </summary>
	MissingMount,

	/// <summary>
	/// Mountpoint was explicitly flagged for remount.
	/// </summary>
	ForcedRemount,

	/// <summary>
	/// Mountpoint is occupied by a non-mergerfs filesystem.
	/// </summary>
	NonMergerfsAtTarget,

	/// <summary>
	/// Existing mount identity does not match the desired identity.
	/// </summary>
	DesiredIdentityMismatch,

	/// <summary>
	/// Existing mount is marked unhealthy when health-check reconciliation is enabled.
	/// </summary>
	UnhealthyMount,

	/// <summary>
	/// Existing mergerfs mount is no longer present in desired mount state.
	/// </summary>
	StaleMount
}
