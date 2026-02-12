namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Captures a point-in-time snapshot of mounted filesystems.
/// </summary>
internal interface IMountSnapshotService
{
	/// <summary>
	/// Captures the current mount snapshot.
	/// </summary>
	/// <returns>Captured mount entries and any non-fatal snapshot warnings.</returns>
	MountSnapshot Capture();
}
