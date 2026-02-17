using SuwayomiSourceMerge.Application.Watching;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Defines merge-pass orchestration behavior used by merge scan dispatch.
/// </summary>
internal interface IMergeMountWorkflow
{
	/// <summary>
	/// Runs one merge pass that scans titles, plans mounts, reconciles state, and applies actions.
	/// </summary>
	/// <param name="reason">Merge dispatch reason text.</param>
	/// <param name="force">Whether force-remount semantics should be used.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Dispatch outcome classification.</returns>
	MergeScanDispatchOutcome RunMergePass(
		string reason,
		bool force,
		CancellationToken cancellationToken = default);
}
