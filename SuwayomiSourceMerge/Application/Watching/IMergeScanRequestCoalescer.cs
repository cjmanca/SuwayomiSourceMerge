namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Coalesces merge-scan requests and dispatches them under retry/interval gates.
/// </summary>
internal interface IMergeScanRequestCoalescer
{
	/// <summary>
	/// Gets a value indicating whether one request is currently pending.
	/// </summary>
	bool HasPendingRequest
	{
		get;
	}

	/// <summary>
	/// Queues or replaces one pending merge-scan request.
	/// </summary>
	/// <param name="reason">Reason text used for diagnostics.</param>
	/// <param name="force">Whether pending request should be marked as force.</param>
	void RequestScan(string reason, bool force = false);

	/// <summary>
	/// Attempts to dispatch the pending request when gates permit execution.
	/// </summary>
	/// <param name="nowUtc">Current timestamp used for interval/retry gate decisions.</param>
	/// <param name="cancellationToken">Cancellation token used during dispatch.</param>
	/// <returns>Dispatch attempt classification.</returns>
	MergeScanDispatchOutcome DispatchPending(
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken = default);
}
