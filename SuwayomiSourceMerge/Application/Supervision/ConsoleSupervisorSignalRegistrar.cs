namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Registers console and process-exit handlers that request supervisor stop.
/// </summary>
internal sealed class ConsoleSupervisorSignalRegistrar : ISupervisorSignalRegistrar
{
	/// <inheritdoc />
	public IDisposable RegisterStopSignal(Action stopCallback)
	{
		ArgumentNullException.ThrowIfNull(stopCallback);

		ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			TryInvoke(stopCallback);
		};

		EventHandler processExitHandler = (_, _) =>
		{
			TryInvoke(stopCallback);
		};

		Console.CancelKeyPress += cancelHandler;
		AppDomain.CurrentDomain.ProcessExit += processExitHandler;

		return new SignalRegistration(cancelHandler, processExitHandler);
	}

	/// <summary>
	/// Executes one signal callback using best-effort semantics.
	/// </summary>
	/// <param name="stopCallback">Stop callback.</param>
	private static void TryInvoke(Action stopCallback)
	{
		try
		{
			stopCallback();
		}
		catch
		{
			// Signal callbacks must never throw.
		}
	}

	/// <summary>
	/// Disposable registration handle used to unregister signal handlers.
	/// </summary>
	private sealed class SignalRegistration : IDisposable
	{
		/// <summary>
		/// Console cancel handler registration.
		/// </summary>
		private readonly ConsoleCancelEventHandler _cancelHandler;

		/// <summary>
		/// Process exit handler registration.
		/// </summary>
		private readonly EventHandler _processExitHandler;

		/// <summary>
		/// Tracks whether the registration has already been disposed.
		/// </summary>
		private bool _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="SignalRegistration"/> class.
		/// </summary>
		/// <param name="cancelHandler">Console cancel handler.</param>
		/// <param name="processExitHandler">Process exit handler.</param>
		public SignalRegistration(
			ConsoleCancelEventHandler cancelHandler,
			EventHandler processExitHandler)
		{
			_cancelHandler = cancelHandler ?? throw new ArgumentNullException(nameof(cancelHandler));
			_processExitHandler = processExitHandler ?? throw new ArgumentNullException(nameof(processExitHandler));
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			Console.CancelKeyPress -= _cancelHandler;
			AppDomain.CurrentDomain.ProcessExit -= _processExitHandler;
			_disposed = true;
		}
	}
}
