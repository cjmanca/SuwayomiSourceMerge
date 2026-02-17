namespace SuwayomiSourceMerge.UnitTests.Application.Supervision;

using SuwayomiSourceMerge.Application.Supervision;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="DaemonSupervisor"/>.
/// </summary>
public sealed class DaemonSupervisorTests
{
	/// <summary>
	/// Verifies start creates pid/lock state and stop removes state files.
	/// </summary>
	[Fact]
	public async Task StartStop_Expected_ShouldCreateAndCleanupStateFiles()
	{
		using TemporaryDirectory temporaryDirectory = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1));

		await supervisor.StartAsync();

		string pidPath = Path.Combine(temporaryDirectory.Path, "daemon.pid");
		string lockPath = Path.Combine(temporaryDirectory.Path, "supervisor.lock");
		Assert.True(File.Exists(pidPath));
		Assert.True(File.Exists(lockPath));

		await supervisor.StopAsync();

		Assert.False(File.Exists(pidPath));
	}

	/// <summary>
	/// Verifies run loop returns success when cancellation is requested before startup begins.
	/// </summary>
	[Fact]
	public async Task RunAsync_Edge_ShouldReturnSuccess_WhenCancellationRequestedBeforeStartup()
	{
		using TemporaryDirectory temporaryDirectory = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1));
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		int exitCode = await supervisor.RunAsync(cancellationTokenSource.Token);

		Assert.Equal(0, exitCode);
		Assert.False(supervisor.IsRunning);
	}

	/// <summary>
	/// Verifies run loop returns success when cancellation requests stop.
	/// </summary>
	[Fact]
	public async Task RunAsync_Expected_ShouldReturnSuccess_WhenCancellationRequested()
	{
		using TemporaryDirectory temporaryDirectory = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1));
		using CancellationTokenSource cancellationTokenSource = new();

		Task<int> runTask = supervisor.RunAsync(cancellationTokenSource.Token);
		await Task.Delay(50);
		cancellationTokenSource.Cancel();

		int exitCode = await runTask;

		Assert.Equal(0, exitCode);
		Assert.False(supervisor.IsRunning);
	}

	/// <summary>
	/// Verifies repeated start calls are idempotent.
	/// </summary>
	[Fact]
	public async Task StartAsync_Edge_ShouldBeIdempotent_WhenCalledMultipleTimes()
	{
		using TemporaryDirectory temporaryDirectory = new();
		CooperativeWorker worker = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			worker,
			TimeSpan.FromSeconds(1));

		await supervisor.StartAsync();
		await supervisor.StartAsync();

		Assert.Equal(1, worker.StartCount);

		await supervisor.StopAsync();
	}

	/// <summary>
	/// Verifies parallel start calls coalesce to a single startup operation.
	/// </summary>
	[Fact]
	public async Task StartAsync_Edge_ShouldCoalesceConcurrentCalls_WhenCalledInParallel()
	{
		using TemporaryDirectory temporaryDirectory = new();
		CooperativeWorker worker = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			worker,
			TimeSpan.FromSeconds(1));

		using Barrier barrier = new(9);
		Task[] startTasks = Enumerable.Range(0, 8)
			.Select(
				_ => Task.Run(
					async () =>
					{
						barrier.SignalAndWait();
						await supervisor.StartAsync();
					}))
			.ToArray();
		barrier.SignalAndWait();

		await Task.WhenAll(startTasks);

		Assert.Equal(1, worker.StartCount);
		await supervisor.StopAsync();
	}

	/// <summary>
	/// Verifies repeated stop calls are idempotent.
	/// </summary>
	[Fact]
	public async Task StopAsync_Edge_ShouldBeIdempotent_WhenCalledMultipleTimes()
	{
		using TemporaryDirectory temporaryDirectory = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1));

		await supervisor.StartAsync();
		await supervisor.StopAsync();
		await supervisor.StopAsync();

		Assert.False(supervisor.IsRunning);
	}

	/// <summary>
	/// Verifies a second supervisor cannot start while the first holds the lock.
	/// </summary>
	[Fact]
	public async Task StartAsync_Failure_ShouldThrow_WhenLockAlreadyHeldByAnotherSupervisor()
	{
		using TemporaryDirectory temporaryDirectory = new();
		DaemonSupervisor first = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1));
		DaemonSupervisor second = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1));

		await first.StartAsync();

		await Assert.ThrowsAsync<IOException>(() => second.StartAsync());
		await first.StopAsync();
	}

	/// <summary>
	/// Verifies startup lock failures return failure and emit startup-failure diagnostics.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldReturnFailureAndLogStartupFailure_WhenStartupLockFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		DaemonSupervisor first = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1));
		DaemonSupervisor second = new(
			new CooperativeWorker(),
			new DaemonSupervisorOptions(
				new SupervisorStatePaths(temporaryDirectory.Path),
				TimeSpan.FromSeconds(1)),
			logger,
			new StubSignalRegistrar());

		await first.StartAsync();
		int exitCode = await second.RunAsync();

		Assert.Equal(1, exitCode);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "supervisor.startup_failure" && entry.Level == LogLevel.Error);
		await first.StopAsync();
	}

	/// <summary>
	/// Verifies worker faults produce a failure exit and cleanup.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldReturnFailureAndCleanup_WhenWorkerThrows()
	{
		using TemporaryDirectory temporaryDirectory = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new ThrowingWorker(),
			TimeSpan.FromSeconds(1));

		int exitCode = await supervisor.RunAsync();

		Assert.Equal(1, exitCode);
		Assert.False(supervisor.IsRunning);
		Assert.False(File.Exists(Path.Combine(temporaryDirectory.Path, "daemon.pid")));
	}

	/// <summary>
	/// Verifies RunAsync succeeds when signal registration triggers immediate stop.
	/// </summary>
	[Fact]
	public async Task RunAsync_Edge_ShouldReturnSuccess_WhenSignalStopsDuringRegistration()
	{
		using TemporaryDirectory temporaryDirectory = new();
		DaemonSupervisor supervisor = new(
			new CooperativeWorker(),
			new DaemonSupervisorOptions(
				new SupervisorStatePaths(temporaryDirectory.Path),
				TimeSpan.FromSeconds(1)),
			new RecordingLogger(),
			new ImmediateStopSignalRegistrar());

		int exitCode = await supervisor.RunAsync();

		Assert.Equal(0, exitCode);
		Assert.False(supervisor.IsRunning);
	}

	/// <summary>
	/// Verifies RunAsync succeeds when cancellation registration callback fires immediately.
	/// </summary>
	[Fact]
	public async Task RunAsync_Edge_ShouldReturnSuccess_WhenCancellationCallbackFiresImmediately()
	{
		using TemporaryDirectory temporaryDirectory = new();
		using CancellationTokenSource cancellationTokenSource = new();
		DaemonSupervisor supervisor = new(
			new CooperativeWorker(),
			new DaemonSupervisorOptions(
				new SupervisorStatePaths(temporaryDirectory.Path),
				TimeSpan.FromSeconds(1)),
			new RecordingLogger(),
			new ImmediateCancellationSignalRegistrar(cancellationTokenSource));

		int exitCode = await supervisor.RunAsync(cancellationTokenSource.Token);

		Assert.Equal(0, exitCode);
		Assert.False(supervisor.IsRunning);
	}

	/// <summary>
	/// Verifies graceful stop paths do not invoke process termination when worker exits before timeout.
	/// </summary>
	[Fact]
	public async Task StopAsync_Expected_ShouldNotInvokeProcessTermination_WhenWorkerStopsBeforeTimeout()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		RecordingProcessTerminator processTerminator = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			TimeSpan.FromSeconds(1),
			new StubSignalRegistrar(),
			processTerminator.Terminate,
			logger);

		await supervisor.StartAsync();
		await supervisor.StopAsync();

		Assert.Equal(0, processTerminator.CallCount);
		Assert.DoesNotContain(
			logger.Events,
			static entry => entry.EventId == "supervisor.stop_timeout" && entry.Level == LogLevel.Error);
	}

	/// <summary>
	/// Verifies timeout classification treats boundary races as non-timeout when worker has already completed.
	/// </summary>
	[Fact]
	public void ShouldTreatStopAsTimedOut_Edge_ShouldReturnFalse_WhenCompletedTaskIsNotWorkerButWorkerAlreadyCompleted()
	{
		Task workerTask = Task.CompletedTask;
		Task completedTask = Task.FromResult(0);

		bool shouldTimeout = DaemonSupervisor.ShouldTreatStopAsTimedOut(workerTask, completedTask);

		Assert.False(shouldTimeout);
	}

	/// <summary>
	/// Verifies timeout classification treats incomplete workers as true timeout conditions.
	/// </summary>
	[Fact]
	public void ShouldTreatStopAsTimedOut_Failure_ShouldReturnTrue_WhenWorkerIsStillIncomplete()
	{
		TaskCompletionSource<bool> workerCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
		Task completedTask = Task.CompletedTask;

		bool shouldTimeout = DaemonSupervisor.ShouldTreatStopAsTimedOut(workerCompletion.Task, completedTask);

		Assert.True(shouldTimeout);
	}

	/// <summary>
	/// Verifies run exits with failure and does not hang when stop timeout elapses.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldReturnFailureWithoutHanging_WhenStopTimeoutElapses()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		RecordingProcessTerminator processTerminator = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new IgnoringCancellationWorker(),
			TimeSpan.FromMilliseconds(100),
			new StubSignalRegistrar(),
			processTerminator.Terminate,
			logger);
		using CancellationTokenSource cancellationTokenSource = new();

		Task<int> runTask = supervisor.RunAsync(cancellationTokenSource.Token);
		await Task.Delay(50);
		cancellationTokenSource.Cancel();

		Task completion = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(runTask, completion);

		int exitCode = await runTask;
		Assert.Equal(1, exitCode);
		Assert.Equal(1, processTerminator.CallCount);
		Assert.Equal("Daemon worker did not stop before timeout elapsed.", processTerminator.LastMessage);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "supervisor.stop_timeout" && entry.Level == LogLevel.Error);
	}

	/// <summary>
	/// Verifies stop timeout path invokes process termination when worker ignores cancellation.
	/// </summary>
	[Fact]
	public async Task StopAsync_Failure_ShouldInvokeProcessTermination_WhenWorkerIgnoresCancellationAndTimeoutExpires()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		RecordingProcessTerminator processTerminator = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new IgnoringCancellationWorker(),
			TimeSpan.FromMilliseconds(100),
			new StubSignalRegistrar(),
			processTerminator.Terminate,
			logger);

		await supervisor.StartAsync();
		await supervisor.StopAsync();

		Assert.Equal(1, processTerminator.CallCount);
		Assert.Equal("Daemon worker did not stop before timeout elapsed.", processTerminator.LastMessage);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "supervisor.stop_timeout" && entry.Level == LogLevel.Error);
	}

	/// <summary>
	/// Verifies stop uses a fresh shutdown token and cancels it when stop timeout elapses.
	/// </summary>
	[Fact]
	public async Task StopAsync_Edge_ShouldCancelShutdownTokenAfterTimeout_WhenWorkerCleanupBlocks()
	{
		using TemporaryDirectory temporaryDirectory = new();
		ShutdownTokenObservingWorker worker = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			worker,
			TimeSpan.FromMilliseconds(100));

		await supervisor.StartAsync();
		Task stopTask = supervisor.StopAsync();

		await worker.CleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
		await stopTask.WaitAsync(TimeSpan.FromSeconds(2));

		Assert.False(worker.ShutdownTokenInitiallyCanceled);
		Assert.True(worker.ShutdownTokenCanceledDuringCleanup);
	}

	/// <summary>
	/// Creates a supervisor for tests.
	/// </summary>
	/// <param name="stateRootPath">State root path.</param>
	/// <param name="worker">Worker implementation.</param>
	/// <param name="stopTimeout">Stop timeout.</param>
	/// <returns>Configured supervisor instance.</returns>
	private static DaemonSupervisor CreateSupervisor(
		string stateRootPath,
		IDaemonWorker worker,
		TimeSpan stopTimeout,
		ISupervisorSignalRegistrar? signalRegistrar = null,
		Action<string, Exception?>? terminateProcess = null,
		RecordingLogger? logger = null)
	{
		Action<string, Exception?> processTerminator = terminateProcess ?? ((_, _) => { });

		return new DaemonSupervisor(
			worker,
			new DaemonSupervisorOptions(new SupervisorStatePaths(stateRootPath), stopTimeout),
			logger ?? new RecordingLogger(),
			signalRegistrar ?? new StubSignalRegistrar(),
			processTerminator);
	}

	/// <summary>
	/// Process terminator fake that records hard-stop requests.
	/// </summary>
	private sealed class RecordingProcessTerminator
	{
		/// <summary>
		/// Gets termination request call count.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets last termination message.
		/// </summary>
		public string? LastMessage
		{
			get;
			private set;
		}

		/// <summary>
		/// Records one termination request.
		/// </summary>
		/// <param name="message">Termination message.</param>
		/// <param name="exception">Optional exception.</param>
		public void Terminate(string message, Exception? exception)
		{
			_ = exception;
			CallCount++;
			LastMessage = message;
		}
	}

	/// <summary>
	/// Signal registrar stub used by tests.
	/// </summary>
	private sealed class StubSignalRegistrar : ISupervisorSignalRegistrar
	{
		/// <inheritdoc />
		public IDisposable RegisterStopSignal(Action stopCallback)
		{
			return new EmptyRegistration();
		}
	}

	/// <summary>
	/// Signal registrar that invokes stop callback synchronously during registration.
	/// </summary>
	private sealed class ImmediateStopSignalRegistrar : ISupervisorSignalRegistrar
	{
		/// <inheritdoc />
		public IDisposable RegisterStopSignal(Action stopCallback)
		{
			ArgumentNullException.ThrowIfNull(stopCallback);
			stopCallback();
			return new EmptyRegistration();
		}
	}

	/// <summary>
	/// Signal registrar that cancels a token before cancellation registration occurs.
	/// </summary>
	private sealed class ImmediateCancellationSignalRegistrar : ISupervisorSignalRegistrar
	{
		private readonly CancellationTokenSource _cancellationTokenSource;

		public ImmediateCancellationSignalRegistrar(CancellationTokenSource cancellationTokenSource)
		{
			_cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
		}

		/// <inheritdoc />
		public IDisposable RegisterStopSignal(Action stopCallback)
		{
			_cancellationTokenSource.Cancel();
			return new EmptyRegistration();
		}
	}

	/// <summary>
	/// Empty signal registration disposable.
	/// </summary>
	private sealed class EmptyRegistration : IDisposable
	{
		public void Dispose()
		{
		}
	}

	/// <summary>
	/// Worker that blocks until cancellation and exits cooperatively.
	/// </summary>
	private sealed class CooperativeWorker : IDaemonWorker
	{
		private int _startCount;

		public int StartCount
		{
			get
			{
				return _startCount;
			}
		}

		public Task RunAsync(CancellationToken cancellationToken, CancellationToken shutdownCancellationToken = default)
		{
			Interlocked.Increment(ref _startCount);
			return Task.Run(
				() =>
				{
					try
					{
						Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).GetAwaiter().GetResult();
					}
					catch (OperationCanceledException)
					{
						// Expected cooperative stop.
					}
				},
				CancellationToken.None);
		}
	}

	/// <summary>
	/// Worker that throws immediately.
	/// </summary>
	private sealed class ThrowingWorker : IDaemonWorker
	{
		public Task RunAsync(CancellationToken cancellationToken, CancellationToken shutdownCancellationToken = default)
		{
			return Task.FromException(new InvalidOperationException("worker-failure"));
		}
	}

	/// <summary>
	/// Worker that never completes and ignores cancellation.
	/// </summary>
	private sealed class IgnoringCancellationWorker : IDaemonWorker
	{
		private readonly TaskCompletionSource<bool> _neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public Task RunAsync(CancellationToken cancellationToken, CancellationToken shutdownCancellationToken = default)
		{
			return _neverCompletes.Task;
		}
	}

	/// <summary>
	/// Worker that observes supervisor shutdown-token cancellation during blocked cleanup.
	/// </summary>
	private sealed class ShutdownTokenObservingWorker : IDaemonWorker
	{
		/// <summary>
		/// Gets a completion source that is signaled when cleanup starts.
		/// </summary>
		public TaskCompletionSource<bool> CleanupStarted
		{
			get;
		} = new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// Gets a value indicating whether shutdown token was initially canceled at cleanup start.
		/// </summary>
		public bool ShutdownTokenInitiallyCanceled
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets a value indicating whether shutdown token cancellation was observed during cleanup.
		/// </summary>
		public bool ShutdownTokenCanceledDuringCleanup
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task RunAsync(CancellationToken cancellationToken, CancellationToken shutdownCancellationToken = default)
		{
			return Task.Run(
				() =>
				{
					try
					{
						Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).GetAwaiter().GetResult();
					}
					catch (OperationCanceledException)
					{
						// Expected cooperative stop signal.
					}

					ShutdownTokenInitiallyCanceled = shutdownCancellationToken.IsCancellationRequested;
					CleanupStarted.TrySetResult(true);

					try
					{
						Task.Delay(Timeout.InfiniteTimeSpan, shutdownCancellationToken).GetAwaiter().GetResult();
					}
					catch (OperationCanceledException)
					{
						ShutdownTokenCanceledDuringCleanup = true;
					}
				},
				CancellationToken.None);
		}
	}

}
