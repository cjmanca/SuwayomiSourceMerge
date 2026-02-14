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
	/// Verifies run exits with failure and does not hang when stop timeout elapses.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldReturnFailureWithoutHanging_WhenStopTimeoutElapses()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		DaemonSupervisor supervisor = new(
			new IgnoringCancellationWorker(),
			new DaemonSupervisorOptions(
				new SupervisorStatePaths(temporaryDirectory.Path),
				TimeSpan.FromMilliseconds(100)),
			logger,
			new StubSignalRegistrar());
		using CancellationTokenSource cancellationTokenSource = new();

		Task<int> runTask = supervisor.RunAsync(cancellationTokenSource.Token);
		await Task.Delay(50);
		cancellationTokenSource.Cancel();

		Task completion = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(2)));
		Assert.Same(runTask, completion);

		int exitCode = await runTask;
		Assert.Equal(1, exitCode);
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "supervisor.stop_timeout" && entry.Level == LogLevel.Error);
	}

	/// <summary>
	/// Verifies stop timeout path still performs cleanup when worker ignores cancellation.
	/// </summary>
	[Fact]
	public async Task StopAsync_Failure_ShouldCleanup_WhenWorkerIgnoresCancellationAndTimeoutExpires()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		DaemonSupervisor supervisor = new(
			new IgnoringCancellationWorker(),
			new DaemonSupervisorOptions(
				new SupervisorStatePaths(temporaryDirectory.Path),
				TimeSpan.FromMilliseconds(100)),
			logger,
			new StubSignalRegistrar());

		await supervisor.StartAsync();
		await supervisor.StopAsync();

		Assert.False(supervisor.IsRunning);
		Assert.False(File.Exists(Path.Combine(temporaryDirectory.Path, "daemon.pid")));
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "supervisor.stop_timeout" && entry.Level == LogLevel.Error);
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
		TimeSpan stopTimeout)
	{
		return new DaemonSupervisor(
			worker,
			new DaemonSupervisorOptions(new SupervisorStatePaths(stateRootPath), stopTimeout),
			new RecordingLogger(),
			new StubSignalRegistrar());
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

		public Task RunAsync(CancellationToken cancellationToken)
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
		public Task RunAsync(CancellationToken cancellationToken)
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

		public Task RunAsync(CancellationToken cancellationToken)
		{
			return _neverCompletes.Task;
		}
	}

}
