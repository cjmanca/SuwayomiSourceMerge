namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Defines one long-running daemon worker loop.
/// </summary>
internal interface IDaemonWorker
{
	/// <summary>
	/// Runs the worker loop until cancellation is requested or a fatal exception occurs.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token used to request graceful stop.</param>
	/// <param name="shutdownCancellationToken">Cancellation token used to bound shutdown lifecycle cleanup work.</param>
	/// <returns>A task that completes when the worker loop exits.</returns>
	Task RunAsync(CancellationToken cancellationToken, CancellationToken shutdownCancellationToken = default);
}
