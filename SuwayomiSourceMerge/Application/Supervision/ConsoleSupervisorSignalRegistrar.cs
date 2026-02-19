using System.Runtime.InteropServices;

namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Registers console and process-exit handlers that request supervisor stop.
/// </summary>
internal sealed class ConsoleSupervisorSignalRegistrar : ISupervisorSignalRegistrar
{
	/// <summary>
	/// POSIX signal registration factory.
	/// </summary>
	private readonly Func<PosixSignal, Action, IDisposable> _posixSignalRegistrar;

	/// <summary>
	/// Evaluates whether POSIX signal registration should be attempted.
	/// </summary>
	private readonly Func<bool> _isPosixRuntime;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsoleSupervisorSignalRegistrar"/> class.
	/// </summary>
	public ConsoleSupervisorSignalRegistrar()
		: this(RegisterPosixSignal, static () => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsoleSupervisorSignalRegistrar"/> class for testing.
	/// </summary>
	/// <param name="posixSignalRegistrar">POSIX signal registration factory.</param>
	/// <param name="isPosixRuntime">Runtime predicate controlling whether POSIX registration is attempted.</param>
	internal ConsoleSupervisorSignalRegistrar(
		Func<PosixSignal, Action, IDisposable> posixSignalRegistrar,
		Func<bool> isPosixRuntime)
	{
		_posixSignalRegistrar = posixSignalRegistrar ?? throw new ArgumentNullException(nameof(posixSignalRegistrar));
		_isPosixRuntime = isPosixRuntime ?? throw new ArgumentNullException(nameof(isPosixRuntime));
	}

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

		List<IDisposable> posixRegistrations = [];
		Console.CancelKeyPress += cancelHandler;
		AppDomain.CurrentDomain.ProcessExit += processExitHandler;

		try
		{
			TryRegisterPosixSignal(PosixSignal.SIGTERM, stopCallback, posixRegistrations);
			return new SignalRegistration(cancelHandler, processExitHandler, posixRegistrations);
		}
		catch
		{
			AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
			Console.CancelKeyPress -= cancelHandler;
			DisposeRegistrations(posixRegistrations);
			throw;
		}
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
	/// Tries to register one POSIX stop signal handler.
	/// </summary>
	/// <param name="signal">POSIX signal.</param>
	/// <param name="stopCallback">Stop callback.</param>
	/// <param name="registrations">Registration accumulator.</param>
	private void TryRegisterPosixSignal(
		PosixSignal signal,
		Action stopCallback,
		List<IDisposable> registrations)
	{
		ArgumentNullException.ThrowIfNull(stopCallback);
		ArgumentNullException.ThrowIfNull(registrations);
		if (!_isPosixRuntime())
		{
			return;
		}

		try
		{
			registrations.Add(
				_posixSignalRegistrar(
					signal,
					() => TryInvoke(stopCallback)));
		}
		catch (PlatformNotSupportedException)
		{
			// Best-effort registration; fallback process-exit handling remains active.
		}
		catch (NotSupportedException)
		{
			// Best-effort registration; fallback process-exit handling remains active.
		}
	}

	/// <summary>
	/// Registers one POSIX signal and converts it to cooperative stop semantics.
	/// </summary>
	/// <param name="signal">POSIX signal.</param>
	/// <param name="callback">Stop callback.</param>
	/// <returns>Signal registration handle.</returns>
	private static IDisposable RegisterPosixSignal(PosixSignal signal, Action callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		return PosixSignalRegistration.Create(
			signal,
			context =>
			{
				context.Cancel = true;
				callback();
			});
	}

	/// <summary>
	/// Disposes a registration set using best-effort semantics.
	/// </summary>
	/// <param name="registrations">Registration set.</param>
	private static void DisposeRegistrations(IReadOnlyList<IDisposable> registrations)
	{
		for (int index = 0; index < registrations.Count; index++)
		{
			try
			{
				registrations[index].Dispose();
			}
			catch
			{
				// Best-effort cleanup.
			}
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
		/// POSIX registration handles.
		/// </summary>
		private readonly IReadOnlyList<IDisposable> _posixRegistrations;

		/// <summary>
		/// Tracks whether the registration has already been disposed.
		/// </summary>
		private bool _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="SignalRegistration"/> class.
		/// </summary>
		/// <param name="cancelHandler">Console cancel handler.</param>
		/// <param name="processExitHandler">Process exit handler.</param>
		/// <param name="posixRegistrations">POSIX registration handles.</param>
		public SignalRegistration(
			ConsoleCancelEventHandler cancelHandler,
			EventHandler processExitHandler,
			IReadOnlyList<IDisposable> posixRegistrations)
		{
			_cancelHandler = cancelHandler ?? throw new ArgumentNullException(nameof(cancelHandler));
			_processExitHandler = processExitHandler ?? throw new ArgumentNullException(nameof(processExitHandler));
			_posixRegistrations = posixRegistrations ?? throw new ArgumentNullException(nameof(posixRegistrations));
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
			DisposeRegistrations(_posixRegistrations);
			_disposed = true;
		}
	}
}
