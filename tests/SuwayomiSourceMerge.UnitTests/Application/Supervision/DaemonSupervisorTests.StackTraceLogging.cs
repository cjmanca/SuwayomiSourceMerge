namespace SuwayomiSourceMerge.UnitTests.Application.Supervision;

using SuwayomiSourceMerge.Application.Supervision;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies daemon-supervisor exception diagnostics include full exception payloads.
/// </summary>
public sealed class DaemonSupervisorStackTraceLoggingTests
{
	/// <summary>
	/// Verifies worker fault diagnostics capture full exception text including stack frames.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldCaptureWorkerFaultStackTraceInContext()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		DaemonSupervisor supervisor = CreateSupervisor(
			temporaryDirectory.Path,
			new ThrowingWorker(),
			logger);

		int exitCode = await supervisor.RunAsync();

		Assert.Equal(1, exitCode);
		RecordingLogger.CapturedLogEvent faultEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "supervisor.worker_fault");
		Assert.NotNull(faultEvent.Context);
		Assert.True(faultEvent.Context!.TryGetValue("exception", out string? exceptionText));
		Assert.Contains("System.InvalidOperationException: worker-failure", exceptionText, StringComparison.Ordinal);
		Assert.Contains(" at ", exceptionText, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies startup-failure diagnostics capture full exception text including stack frames.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldCaptureStartupFailureStackTraceInContext()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingLogger logger = new();
		DaemonSupervisor first = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			new RecordingLogger());
		DaemonSupervisor second = CreateSupervisor(
			temporaryDirectory.Path,
			new CooperativeWorker(),
			logger);

		await first.StartAsync();
		int exitCode = await second.RunAsync();
		await first.StopAsync();

		Assert.Equal(1, exitCode);
		RecordingLogger.CapturedLogEvent startupFailureEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "supervisor.startup_failure");
		Assert.NotNull(startupFailureEvent.Context);
		Assert.True(startupFailureEvent.Context!.TryGetValue("exception", out string? exceptionText));
		Assert.Contains("System.IO.IOException", exceptionText, StringComparison.Ordinal);
		Assert.Contains(" at ", exceptionText, StringComparison.Ordinal);
	}

	/// <summary>
	/// Creates one supervisor instance for stack-trace logging tests.
	/// </summary>
	/// <param name="stateRootPath">State-root path.</param>
	/// <param name="worker">Worker dependency.</param>
	/// <param name="logger">Recording logger.</param>
	/// <returns>Configured supervisor.</returns>
	private static DaemonSupervisor CreateSupervisor(
		string stateRootPath,
		IDaemonWorker worker,
		RecordingLogger logger)
	{
		return new DaemonSupervisor(
			worker,
			new DaemonSupervisorOptions(new SupervisorStatePaths(stateRootPath), TimeSpan.FromSeconds(1)),
			logger,
			new StubSignalRegistrar(),
			static (_, _) => { });
	}

	/// <summary>
	/// Signal-registrar stub that performs no action.
	/// </summary>
	private sealed class StubSignalRegistrar : ISupervisorSignalRegistrar
	{
		/// <inheritdoc />
		public IDisposable RegisterStopSignal(Action stopCallback)
		{
			ArgumentNullException.ThrowIfNull(stopCallback);
			return new EmptyRegistration();
		}
	}

	/// <summary>
	/// Disposable no-op registration.
	/// </summary>
	private sealed class EmptyRegistration : IDisposable
	{
		/// <inheritdoc />
		public void Dispose()
		{
		}
	}

	/// <summary>
	/// Worker that exits cooperatively on cancellation.
	/// </summary>
	private sealed class CooperativeWorker : IDaemonWorker
	{
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
						// Expected during cooperative stop.
					}
				},
				CancellationToken.None);
		}
	}

	/// <summary>
	/// Worker that fails immediately.
	/// </summary>
	private sealed class ThrowingWorker : IDaemonWorker
	{
		/// <inheritdoc />
		public Task RunAsync(CancellationToken cancellationToken, CancellationToken shutdownCancellationToken = default)
		{
			return Task.FromException(new InvalidOperationException("worker-failure"));
		}
	}
}
