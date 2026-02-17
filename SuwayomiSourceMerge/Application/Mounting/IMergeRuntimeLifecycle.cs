namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Defines startup and shutdown lifecycle hooks for merge runtime orchestration.
/// </summary>
internal interface IMergeRuntimeLifecycle
{
	/// <summary>
	/// Executes startup lifecycle behavior before the worker tick loop begins.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	void OnWorkerStarting(CancellationToken cancellationToken = default);

	/// <summary>
	/// Executes shutdown lifecycle behavior while the worker is stopping.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token.</param>
	void OnWorkerStopping(CancellationToken cancellationToken = default);
}
