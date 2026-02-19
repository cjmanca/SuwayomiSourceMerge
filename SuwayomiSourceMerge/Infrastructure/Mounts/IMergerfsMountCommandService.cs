namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Applies mount reconciliation actions using external mergerfs and unmount commands.
/// </summary>
internal interface IMergerfsMountCommandService
{
	/// <summary>
	/// Applies one reconciliation action.
	/// </summary>
	/// <param name="action">Reconciliation action to apply.</param>
	/// <param name="mergerfsOptionsBase">Base mergerfs options string.</param>
	/// <param name="commandTimeout">Per-command timeout.</param>
	/// <param name="pollInterval">Per-command process poll interval.</param>
	/// <param name="cleanupHighPriority">
	/// Whether cleanup priority wrappers should be attempted for unmount commands.
	/// </param>
	/// <param name="cleanupPriorityIoniceClass">Ionice class value used for cleanup wrapper execution.</param>
	/// <param name="cleanupPriorityNiceValue">Nice value used for cleanup wrapper execution.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Apply result classification and diagnostics.</returns>
	MountActionApplyResult ApplyAction(
		MountReconciliationAction action,
		string mergerfsOptionsBase,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		bool cleanupHighPriority,
		int cleanupPriorityIoniceClass,
		int cleanupPriorityNiceValue,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Attempts to unmount one mountpoint using canonical command fallbacks.
	/// </summary>
	/// <param name="mountPoint">Mountpoint to unmount.</param>
	/// <param name="commandTimeout">Per-command timeout.</param>
	/// <param name="pollInterval">Per-command process poll interval.</param>
	/// <param name="cleanupHighPriority">
	/// Whether cleanup priority wrappers should be attempted before plain commands.
	/// </param>
	/// <param name="cleanupPriorityIoniceClass">Ionice class value used for cleanup wrapper execution.</param>
	/// <param name="cleanupPriorityNiceValue">Nice value used for cleanup wrapper execution.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Apply result classification and diagnostics.</returns>
	MountActionApplyResult UnmountMountPoint(
		string mountPoint,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		bool cleanupHighPriority,
		int cleanupPriorityIoniceClass,
		int cleanupPriorityNiceValue,
		CancellationToken cancellationToken = default);

	/// <summary>
	/// Probes one mounted path for readiness using a timeout-bounded command execution path.
	/// </summary>
	/// <param name="mountPoint">Mounted path to probe.</param>
	/// <param name="commandTimeout">Per-command timeout.</param>
	/// <param name="pollInterval">Per-command process poll interval.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Readiness probe result with diagnostic details.</returns>
	MountReadinessProbeResult ProbeMountPointReadiness(
		string mountPoint,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		CancellationToken cancellationToken = default);
}
