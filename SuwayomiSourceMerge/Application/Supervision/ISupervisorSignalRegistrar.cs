namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Registers process signal callbacks used to trigger supervisor shutdown.
/// </summary>
internal interface ISupervisorSignalRegistrar
{
	/// <summary>
	/// Registers a callback invoked when the process receives stop-related signals.
	/// </summary>
	/// <param name="stopCallback">Callback to invoke on signal receipt.</param>
	/// <returns>A disposable registration that unregisters signal handlers.</returns>
	IDisposable RegisterStopSignal(Action stopCallback);
}
