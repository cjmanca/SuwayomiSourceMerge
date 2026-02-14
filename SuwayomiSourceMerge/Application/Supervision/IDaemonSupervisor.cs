namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Defines lifecycle control operations for the runtime daemon supervisor.
/// </summary>
internal interface IDaemonSupervisor
{
	/// <summary>
	/// Gets a value indicating whether the supervisor is currently running.
	/// </summary>
	bool IsRunning
	{
		get;
	}

	/// <summary>
	/// Starts the supervisor if it is not already running.
	/// </summary>
	/// <param name="cancellationToken">
	/// Cancellation token checked before startup begins. Once startup work has started, cancellation is best-effort.
	/// </param>
	/// <returns>A task that completes when startup has finished.</returns>
	Task StartAsync(CancellationToken cancellationToken = default);

	/// <summary>
	/// Requests supervisor stop and waits for cleanup.
	/// </summary>
	/// <returns>A task that completes when stop cleanup has finished.</returns>
	Task StopAsync();

	/// <summary>
	/// Runs the supervisor until signal/cancellation/worker exit and returns an exit code.
	/// </summary>
	/// <param name="cancellationToken">Cancellation token used to request stop.</param>
	/// <returns>Zero for successful stop, non-zero for fatal runtime failures.</returns>
	Task<int> RunAsync(CancellationToken cancellationToken = default);
}
