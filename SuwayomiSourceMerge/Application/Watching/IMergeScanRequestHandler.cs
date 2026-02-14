namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Dispatches merge-scan requests to a downstream orchestration target.
/// </summary>
internal interface IMergeScanRequestHandler
{
	/// <summary>
	/// Attempts to dispatch one merge scan request.
	/// </summary>
	/// <param name="reason">Reason text used for diagnostics.</param>
	/// <param name="force">Whether dispatch should be treated as a force/priority request.</param>
	/// <param name="cancellationToken">Cancellation token used to abort dispatch work.</param>
	/// <returns>Dispatch outcome classification.</returns>
	MergeScanDispatchOutcome DispatchMergeScan(
		string reason,
		bool force,
		CancellationToken cancellationToken = default);
}
