using System.Globalization;

using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Coordinates daemon startup, single-instance locking, signal-driven stop, and cleanup.
/// </summary>
internal sealed class DaemonSupervisor : IDaemonSupervisor
{
	/// <summary>Event id emitted when supervisor startup completes.</summary>
	private const string SUPERVISOR_STARTED_EVENT = "supervisor.started";

	/// <summary>Event id emitted when supervisor stop is requested.</summary>
	private const string SUPERVISOR_STOP_REQUESTED_EVENT = "supervisor.stop_requested";

	/// <summary>Event id emitted when supervisor stop cleanup completes.</summary>
	private const string SUPERVISOR_STOPPED_EVENT = "supervisor.stopped";

	/// <summary>Event id emitted when worker exits unexpectedly without a stop request.</summary>
	private const string SUPERVISOR_WORKER_EXITED_EVENT = "supervisor.worker_exited";

	/// <summary>Event id emitted when worker exits with an unhandled exception.</summary>
	private const string SUPERVISOR_WORKER_FAULT_EVENT = "supervisor.worker_fault";

	/// <summary>Event id emitted when startup fails before run-loop execution begins.</summary>
	private const string SUPERVISOR_STARTUP_FAILURE_EVENT = "supervisor.startup_failure";

	/// <summary>Event id emitted when graceful stop timeout expires.</summary>
	private const string SUPERVISOR_STOP_TIMEOUT_EVENT = "supervisor.stop_timeout";

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
	/// Active lock handle while running.
	/// </summary>
	private SupervisorFileLock? _lockHandle;

	/// <summary>
	/// Active worker cancellation source while running.
	/// </summary>
	private CancellationTokenSource? _workerCancellationSource;

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
	public DaemonSupervisor(
		IDaemonWorker worker,
		DaemonSupervisorOptions options,
		ISsmLogger logger,
		ISupervisorSignalRegistrar signalRegistrar)
	{
		_worker = worker ?? throw new ArgumentNullException(nameof(worker));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_signalRegistrar = signalRegistrar ?? throw new ArgumentNullException(nameof(signalRegistrar));
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
			_logger.Debug(SUPERVISOR_STOP_REQUESTED_EVENT, "Daemon supervisor stop requested.");
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
					SUPERVISOR_STARTUP_FAILURE_EVENT,
					"Daemon supervisor startup failed.",
					BuildContext(
						("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
						("message", exception.Message)));
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
						SUPERVISOR_WORKER_EXITED_EVENT,
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
				SUPERVISOR_WORKER_FAULT_EVENT,
				"Daemon worker exited with an unhandled exception.",
				BuildContext(
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
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
		Task? workerTask;
		bool stopTimedOut = false;

		lock (_syncRoot)
		{
			workerCancellationSource = _workerCancellationSource;
			workerTask = _workerTask;
		}

		workerCancellationSource?.Cancel();

		try
		{
			if (workerTask is not null)
			{
				Task timeoutTask = Task.Delay(_options.StopTimeout);
				Task completedTask = await Task.WhenAny(workerTask, timeoutTask).ConfigureAwait(false);
				if (!ReferenceEquals(completedTask, workerTask))
				{
					stopTimedOut = true;
					_logger.Error(
						SUPERVISOR_STOP_TIMEOUT_EVENT,
						"Daemon worker did not stop before timeout elapsed.",
						BuildContext(("timeout_seconds", _options.StopTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture))));
				}
				else
				{
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
				_workerTask = null;
				_stopTask = null;
				stopCompletionSource = _stopCompletionSource;
				_stopCompletionSource = null;
				_isRunning = false;
			}

			stopCompletionSource?.TrySetResult(stopTimedOut);
			_logger.Debug(
				SUPERVISOR_STOPPED_EVENT,
				"Daemon supervisor stopped.",
				BuildContext(("state_root", _options.StatePaths.StateRootPath)));
		}
	}

	/// <summary>
	/// Executes startup lock/pid setup and launches the daemon worker.
	/// </summary>
	private void StartCore()
	{
		SupervisorFileLock? acquiredLock = null;
		CancellationTokenSource? workerCancellationSource = null;
		Task? workerTask = null;

		try
		{
			Directory.CreateDirectory(_options.StatePaths.StateRootPath);
			acquiredLock = SupervisorFileLock.Acquire(_options.StatePaths.SupervisorLockFilePath);
			workerCancellationSource = new CancellationTokenSource();
			workerTask = _worker.RunAsync(workerCancellationSource.Token);
			File.WriteAllText(
				_options.StatePaths.DaemonPidFilePath,
				Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
		}
		catch
		{
			workerCancellationSource?.Cancel();
			workerCancellationSource?.Dispose();
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
				acquiredLock.Dispose();
				TryDeleteFile(_options.StatePaths.DaemonPidFilePath);
				return;
			}

			_lockHandle = acquiredLock;
			_workerCancellationSource = workerCancellationSource;
			_workerTask = workerTask;
			_stopTask = null;
			_stopRequested = false;
			_stopCompletionSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			_isRunning = true;
		}

		_logger.Debug(
			SUPERVISOR_STARTED_EVENT,
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
}
