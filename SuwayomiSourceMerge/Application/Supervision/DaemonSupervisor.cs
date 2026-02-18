using System.Globalization;

using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Coordinates daemon startup, single-instance locking, signal-driven stop, and cleanup.
/// </summary>
internal sealed class DaemonSupervisor : IDaemonSupervisor
{
	/// <summary>Event id emitted when supervisor startup completes.</summary>
	private const string SupervisorStartedEvent = "supervisor.started";

	/// <summary>Event id emitted when supervisor stop is requested.</summary>
	private const string SupervisorStopRequestedEvent = "supervisor.stop_requested";

	/// <summary>Event id emitted when supervisor stop cleanup completes.</summary>
	private const string SupervisorStoppedEvent = "supervisor.stopped";

	/// <summary>Event id emitted when worker exits unexpectedly without a stop request.</summary>
	private const string SupervisorWorkerExitedEvent = "supervisor.worker_exited";

	/// <summary>Event id emitted when worker exits with an unhandled exception.</summary>
	private const string SupervisorWorkerFaultEvent = "supervisor.worker_fault";

	/// <summary>Event id emitted when startup fails before run-loop execution begins.</summary>
	private const string SupervisorStartupFailureEvent = "supervisor.startup_failure";

	/// <summary>Event id emitted when graceful stop timeout expires.</summary>
	private const string SupervisorStopTimeoutEvent = "supervisor.stop_timeout";

	/// <summary>
	/// Synchronization gate for start/stop state transitions.
	/// </summary>
	private readonly object _syncRoot = new();

	/// <summary>
	/// Worker loop dependency.
	/// </summary>
	private readonly IDaemonWorker _worker;

	/// <summary>
	/// Supervisor options.
	/// </summary>
	private readonly DaemonSupervisorOptions _options;

	/// <summary>
	/// Logger dependency.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Signal registrar dependency.
	/// </summary>
	private readonly ISupervisorSignalRegistrar _signalRegistrar;

	/// <summary>
	/// Process-termination callback used when stop timeout indicates unrecoverable runtime state.
	/// </summary>
	private readonly Action<string, Exception?> _terminateProcess;

	/// <summary>
	/// Active lock handle while running.
	/// </summary>
	private SupervisorFileLock? _lockHandle;

	/// <summary>
	/// Active worker cancellation source while running.
	/// </summary>
	private CancellationTokenSource? _workerCancellationSource;

	/// <summary>
	/// Active worker shutdown cancellation source while running.
	/// </summary>
	private CancellationTokenSource? _workerShutdownCancellationSource;

	/// <summary>
	/// Active worker task while running.
	/// </summary>
	private Task? _workerTask;

	/// <summary>
	/// Active start task when start is already in progress.
	/// </summary>
	private Task? _startTask;

	/// <summary>
	/// Active stop task when stop is already in progress.
	/// </summary>
	private Task? _stopTask;

	/// <summary>
	/// Tracks whether start has completed and the supervisor is considered running.
	/// </summary>
	private bool _isRunning;

	/// <summary>
	/// Tracks whether stop has been requested.
	/// </summary>
	private bool _stopRequested;

	/// <summary>
	/// Completion source for the active stop operation lifecycle.
	/// </summary>
	private TaskCompletionSource<bool>? _stopCompletionSource;

	/// <summary>
	/// Initializes a new instance of the <see cref="DaemonSupervisor"/> class.
	/// </summary>
	/// <param name="worker">Worker loop dependency.</param>
	/// <param name="options">Supervisor options.</param>
	/// <param name="logger">Logger dependency.</param>
	/// <param name="signalRegistrar">Signal registrar dependency.</param>
	/// <param name="terminateProcess">
	/// Optional process-termination callback used when stop timeout elapses.
	/// Defaults to <see cref="Environment.FailFast(string, Exception?)"/>.
	/// </param>
	public DaemonSupervisor(
		IDaemonWorker worker,
		DaemonSupervisorOptions options,
		ISsmLogger logger,
		ISupervisorSignalRegistrar signalRegistrar,
		Action<string, Exception?>? terminateProcess = null)
	{
		_worker = worker ?? throw new ArgumentNullException(nameof(worker));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_signalRegistrar = signalRegistrar ?? throw new ArgumentNullException(nameof(signalRegistrar));
		_terminateProcess = terminateProcess ?? DefaultTerminateProcess;
	}

	/// <inheritdoc />
	public bool IsRunning
	{
		get
		{
			lock (_syncRoot)
			{
				return _isRunning;
			}
		}
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken = default)
	{
		// Startup cancellation is observed before startup work begins.
		cancellationToken.ThrowIfCancellationRequested();

		TaskCompletionSource<bool>? startCompletionSource = null;
		Task? startTask;

		lock (_syncRoot)
		{
			if (_isRunning)
			{
				return Task.CompletedTask;
			}

			if (_startTask is not null)
			{
				return _startTask;
			}

			startCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			startTask = startCompletionSource.Task;
			_startTask = startTask;
		}

		try
		{
			StartCore();
			startCompletionSource.SetResult(true);
			return startTask;
		}
		catch (Exception exception)
		{
			startCompletionSource.SetException(exception);
			throw;
		}
		finally
		{
			lock (_syncRoot)
			{
				_startTask = null;
			}
		}
	}

	/// <inheritdoc />
	public Task StopAsync()
	{
		lock (_syncRoot)
		{
			if (!_isRunning)
			{
				return Task.CompletedTask;
			}

			if (_stopTask is not null)
			{
				return _stopTask;
			}

			_stopRequested = true;
			_logger.Normal(SupervisorStopRequestedEvent, "Daemon supervisor stop requested.");
			_stopTask = StopCoreAsync();
			return _stopTask;
		}
	}

	/// <inheritdoc />
	public async Task<int> RunAsync(CancellationToken cancellationToken = default)
	{
		int exitCode = 1;
		try
		{
			try
			{
				await StartAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				return 0;
			}
			catch (Exception exception)
			{
				_logger.Error(
					SupervisorStartupFailureEvent,
					"Daemon supervisor startup failed.",
					BuildContext(
						("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
						("message", exception.Message),
						("exception", exception.ToString())));
				return 1;
			}

			Task workerTask;
			Task<bool> stopCompletionTask;
			try
			{
				// Capture run-state tasks before signal/cancellation registration; callbacks can
				// invoke StopAsync synchronously and clear active state on early-stop paths.
				workerTask = GetWorkerTaskOrThrow();
				stopCompletionTask = GetStopCompletionTaskOrThrow();
			}
			catch (InvalidOperationException) when (!IsRunning)
			{
				return 0;
			}

			using IDisposable signalRegistration = _signalRegistrar.RegisterStopSignal(
				() =>
				{
					_ = StopAsync();
				});
			using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
				() =>
				{
					_ = StopAsync();
				});

			Task completedTask = await Task.WhenAny(workerTask, stopCompletionTask).ConfigureAwait(false);
			if (ReferenceEquals(completedTask, stopCompletionTask))
			{
				bool stopTimedOut = await stopCompletionTask.ConfigureAwait(false);
				exitCode = stopTimedOut ? 1 : 0;
			}
			else
			{
				await workerTask.ConfigureAwait(false);
				exitCode = _stopRequested ? 0 : 1;
				if (exitCode != 0)
				{
					_logger.Error(
						SupervisorWorkerExitedEvent,
						"Daemon worker exited without a stop request.");
				}
			}
		}
		catch (OperationCanceledException) when (_stopRequested || cancellationToken.IsCancellationRequested)
		{
			exitCode = 0;
		}
		catch (Exception exception)
		{
			_logger.Error(
				SupervisorWorkerFaultEvent,
				"Daemon worker exited with an unhandled exception.",
				BuildContext(
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message),
					("exception", exception.ToString())));
			exitCode = 1;
		}
		finally
		{
			await StopAsync().ConfigureAwait(false);
		}

		return exitCode;
	}

	/// <summary>
	/// Performs stop cancellation, timeout waiting, and resource cleanup.
	/// </summary>
	/// <returns>A task that completes when stop cleanup has finished.</returns>
	private async Task StopCoreAsync()
	{
		CancellationTokenSource? workerCancellationSource;
		CancellationTokenSource? workerShutdownCancellationSource;
		Task? workerTask;
		bool stopTimedOut = false;

		lock (_syncRoot)
		{
			workerCancellationSource = _workerCancellationSource;
			workerShutdownCancellationSource = _workerShutdownCancellationSource;
			workerTask = _workerTask;
		}

		workerShutdownCancellationSource?.CancelAfter(_options.StopTimeout);
		workerCancellationSource?.Cancel();

		try
		{
			if (workerTask is not null)
			{
				Task timeoutTask = Task.Delay(_options.StopTimeout);
				Task completedTask = await Task.WhenAny(workerTask, timeoutTask).ConfigureAwait(false);
				if (ShouldTreatStopAsTimedOut(workerTask, completedTask))
				{
					stopTimedOut = true;
					workerShutdownCancellationSource?.Cancel();
					const string timeoutMessage = "Daemon worker did not stop before timeout elapsed.";
					_logger.Error(
						SupervisorStopTimeoutEvent,
						timeoutMessage,
						BuildContext(("timeout_seconds", _options.StopTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture))));
					_terminateProcess(timeoutMessage, null);
				}
				else
				{
					await ObserveWorkerCompletionDuringStop(workerTask).ConfigureAwait(false);
				}
			}
		}
		finally
		{
			TaskCompletionSource<bool>? stopCompletionSource;
			lock (_syncRoot)
			{
				TryDeleteFile(_options.StatePaths.DaemonPidFilePath);
				_lockHandle?.Dispose();
				_lockHandle = null;
				_workerCancellationSource?.Dispose();
				_workerCancellationSource = null;
				_workerShutdownCancellationSource?.Dispose();
				_workerShutdownCancellationSource = null;
				_workerTask = null;
				_stopTask = null;
				stopCompletionSource = _stopCompletionSource;
				_stopCompletionSource = null;
				_isRunning = false;
			}

			stopCompletionSource?.TrySetResult(stopTimedOut);
			_logger.Normal(
				SupervisorStoppedEvent,
				"Daemon supervisor stopped.",
				BuildContext(("state_root", _options.StatePaths.StateRootPath)));
		}
	}

	/// <summary>
	/// Returns whether stop should be treated as timed out after a worker/timeout race.
	/// </summary>
	/// <param name="workerTask">Active worker task.</param>
	/// <param name="completedTask">Task returned by <see cref="Task.WhenAny(Task[])"/>.</param>
	/// <returns>
	/// <see langword="true"/> when timeout handling should continue because the worker is still incomplete;
	/// otherwise <see langword="false"/>.
	/// </returns>
	internal static bool ShouldTreatStopAsTimedOut(Task workerTask, Task completedTask)
	{
		ArgumentNullException.ThrowIfNull(workerTask);
		ArgumentNullException.ThrowIfNull(completedTask);

		if (ReferenceEquals(completedTask, workerTask))
		{
			return false;
		}

		// Boundary race guard: if both timeout and worker complete at the same instant,
		// Task.WhenAny may return either task. Treat completed worker as non-timeout.
		return !workerTask.IsCompleted;
	}

	/// <summary>
	/// Awaits worker completion during stop and swallows cooperative/faulted outcomes.
	/// </summary>
	/// <param name="workerTask">Worker task to observe.</param>
	/// <returns>A completion task.</returns>
	private static async Task ObserveWorkerCompletionDuringStop(Task workerTask)
	{
		ArgumentNullException.ThrowIfNull(workerTask);

		try
		{
			await workerTask.ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected on cooperative stop.
		}
		catch
		{
			// Worker faults are handled by RunAsync and do not block cleanup.
		}
	}

	/// <summary>
	/// Executes startup lock/pid setup and launches the daemon worker.
	/// </summary>
	private void StartCore()
	{
		SupervisorFileLock? acquiredLock = null;
		CancellationTokenSource? workerCancellationSource = null;
		CancellationTokenSource? workerShutdownCancellationSource = null;
		Task? workerTask = null;

		try
		{
			Directory.CreateDirectory(_options.StatePaths.StateRootPath);
			acquiredLock = SupervisorFileLock.Acquire(_options.StatePaths.SupervisorLockFilePath);
			workerCancellationSource = new CancellationTokenSource();
			workerShutdownCancellationSource = new CancellationTokenSource();
			workerTask = _worker.RunAsync(workerCancellationSource.Token, workerShutdownCancellationSource.Token);
			File.WriteAllText(
				_options.StatePaths.DaemonPidFilePath,
				Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
		}
		catch
		{
			workerCancellationSource?.Cancel();
			workerCancellationSource?.Dispose();
			workerShutdownCancellationSource?.Cancel();
			workerShutdownCancellationSource?.Dispose();
			acquiredLock?.Dispose();
			TryDeleteFile(_options.StatePaths.DaemonPidFilePath);
			throw;
		}

		lock (_syncRoot)
		{
			if (_isRunning)
			{
				workerCancellationSource.Cancel();
				workerCancellationSource.Dispose();
				workerShutdownCancellationSource?.Cancel();
				workerShutdownCancellationSource?.Dispose();
				acquiredLock.Dispose();
				TryDeleteFile(_options.StatePaths.DaemonPidFilePath);
				return;
			}

			_lockHandle = acquiredLock;
			_workerCancellationSource = workerCancellationSource;
			_workerShutdownCancellationSource = workerShutdownCancellationSource;
			_workerTask = workerTask;
			_stopTask = null;
			_stopRequested = false;
			_stopCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_isRunning = true;
		}

		_logger.Normal(
			SupervisorStartedEvent,
			"Daemon supervisor started.",
			BuildContext(("state_root", _options.StatePaths.StateRootPath)));
	}

	/// <summary>
	/// Returns the currently running worker task.
	/// </summary>
	/// <returns>Active worker task.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no active worker task exists.</exception>
	private Task GetWorkerTaskOrThrow()
	{
		lock (_syncRoot)
		{
			if (_workerTask is null)
			{
				throw new InvalidOperationException("No active worker task exists.");
			}

			return _workerTask;
		}
	}

	/// <summary>
	/// Returns the active stop completion task.
	/// </summary>
	/// <returns>Stop completion task for the active run lifecycle.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no active stop completion task exists.</exception>
	private Task<bool> GetStopCompletionTaskOrThrow()
	{
		lock (_syncRoot)
		{
			if (_stopCompletionSource is null)
			{
				throw new InvalidOperationException("No active stop completion task exists.");
			}

			return _stopCompletionSource.Task;
		}
	}

	/// <summary>
	/// Deletes one file path using best-effort semantics.
	/// </summary>
	/// <param name="filePath">File path to delete.</param>
	private static void TryDeleteFile(string filePath)
	{
		try
		{
			if (File.Exists(filePath))
			{
				File.Delete(filePath);
			}
		}
		catch
		{
			// Best-effort cleanup.
		}
	}

	/// <summary>
	/// Builds one immutable logging context dictionary.
	/// </summary>
	/// <param name="pairs">Context key/value pairs.</param>
	/// <returns>Context dictionary.</returns>
	private static IReadOnlyDictionary<string, string> BuildContext(params (string Key, string Value)[] pairs)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		for (int index = 0; index < pairs.Length; index++)
		{
			(string key, string value) = pairs[index];
			if (!string.IsNullOrWhiteSpace(key))
			{
				context[key] = value;
			}
		}

		return context;
	}

	/// <summary>
	/// Terminates the current process immediately.
	/// </summary>
	/// <param name="message">Failure message.</param>
	/// <param name="exception">Optional exception context.</param>
	private static void DefaultTerminateProcess(string message, Exception? exception)
	{
		Environment.FailFast(message, exception);
	}
}
