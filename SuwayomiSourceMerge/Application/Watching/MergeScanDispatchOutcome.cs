namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Classifies merge-scan dispatch outcomes used by request coalescing.
/// </summary>
internal enum MergeScanDispatchOutcome
{
	/// <summary>
	/// No pending request was available for dispatch.
	/// </summary>
	NoPendingRequest,

	/// <summary>
	/// Dispatch was skipped due to minimum interval gating.
	/// </summary>
	SkippedDueToMinInterval,

	/// <summary>
	/// Dispatch was skipped due to retry-delay gating after a prior busy/failure outcome.
	/// </summary>
	SkippedDueToRetryDelay,

	/// <summary>
	/// Request dispatch succeeded.
	/// </summary>
	Success,

	/// <summary>
	/// Dispatch target reported a busy condition.
	/// </summary>
	Busy,

	/// <summary>
	/// Dispatch target reported both busy and non-busy failure conditions.
	/// </summary>
	Mixed,

	/// <summary>
	/// Dispatch target reported a non-busy failure.
	/// </summary>
	Failure
}
