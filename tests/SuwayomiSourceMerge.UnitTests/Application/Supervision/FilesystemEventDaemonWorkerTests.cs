namespace SuwayomiSourceMerge.UnitTests.Application.Supervision;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Supervision;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FilesystemEventDaemonWorker"/>.
/// </summary>
public sealed class FilesystemEventDaemonWorkerTests
{
	/// <summary>
	/// Verifies startup and shutdown lifecycle hooks run exactly once for one canceled worker run.
	/// </summary>
	[Fact]
	public async Task RunAsync_Expected_ShouldInvokeLifecycleHooksOnce_WhenWorkerIsCancelled()
	{
		RecordingLifecycle lifecycle = new();
		FilesystemEventDaemonWorker worker = CreateWorker(lifecycle, new RecordingLogger());
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => worker.RunAsync(cancellationTokenSource.Token)).WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Equal(1, lifecycle.StartCalls);
		Assert.Equal(1, lifecycle.StopCalls);
		Assert.False(lifecycle.StopCancellationRequested);
	}

	/// <summary>
	/// Verifies worker forwards provided shutdown cancellation token into stop lifecycle hook.
	/// </summary>
	[Fact]
	public async Task RunAsync_Edge_ShouldForwardProvidedShutdownToken_WhenStoppingLifecycleRuns()
	{
		RecordingLifecycle lifecycle = new();
		FilesystemEventDaemonWorker worker = CreateWorker(lifecycle, new RecordingLogger());
		using CancellationTokenSource runCancellationTokenSource = new();
		using CancellationTokenSource shutdownCancellationTokenSource = new();
		shutdownCancellationTokenSource.Cancel();
		runCancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => worker.RunAsync(runCancellationTokenSource.Token, shutdownCancellationTokenSource.Token)).WaitAsync(TimeSpan.FromSeconds(5));

		Assert.True(lifecycle.StopCancellationRequested);
	}

	/// <summary>
	/// Verifies startup lifecycle exceptions fail worker startup.
	/// </summary>
	[Fact]
	public async Task RunAsync_Edge_ShouldThrow_WhenStartupLifecycleFails()
	{
		FilesystemEventDaemonWorker worker = CreateWorker(
			new ThrowingStartupLifecycle(),
			new RecordingLogger());

		await Assert.ThrowsAsync<InvalidOperationException>(() => worker.RunAsync(CancellationToken.None))
			.WaitAsync(TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// Verifies shutdown lifecycle exceptions are suppressed and logged.
	/// </summary>
	[Fact]
	public async Task RunAsync_Edge_ShouldSuppressShutdownLifecycleException_AndLogWarning()
	{
		RecordingLogger logger = new();
		FilesystemEventDaemonWorker worker = CreateWorker(new ThrowingShutdownLifecycle(), logger);
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => worker.RunAsync(cancellationTokenSource.Token)).WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Contains(logger.Events, static entry => entry.EventId == "supervisor.worker.lifecycle_warning");
	}

	/// <summary>
	/// Verifies cooperative shutdown cancellation is downgraded and does not emit lifecycle warnings.
	/// </summary>
	[Fact]
	public async Task RunAsync_Edge_ShouldDowngradeShutdownCancellation_WhenShutdownTokenIsCancelled()
	{
		RecordingLogger logger = new();
		FilesystemEventDaemonWorker worker = CreateWorker(new ThrowingShutdownCancellationLifecycle(), logger);
		using CancellationTokenSource runCancellationTokenSource = new();
		using CancellationTokenSource shutdownCancellationTokenSource = new();
		shutdownCancellationTokenSource.Cancel();
		runCancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => worker.RunAsync(runCancellationTokenSource.Token, shutdownCancellationTokenSource.Token)).WaitAsync(TimeSpan.FromSeconds(5));

		Assert.DoesNotContain(logger.Events, static entry => entry.EventId == "supervisor.worker.lifecycle_warning");
		Assert.Contains(
			logger.Events,
			static entry => entry.EventId == "supervisor.worker.stopped" &&
				entry.Message.Contains("cooperative cancellation", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies canceled shutdown token does not downgrade cancellation exceptions that carry a different token.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldLogWarning_WhenShutdownTokenIsCancelledButExceptionTokenDiffers()
	{
		RecordingLogger logger = new();
		FilesystemEventDaemonWorker worker = CreateWorker(new ThrowingMismatchedShutdownCancellationLifecycle(), logger);
		using CancellationTokenSource runCancellationTokenSource = new();
		using CancellationTokenSource shutdownCancellationTokenSource = new();
		shutdownCancellationTokenSource.Cancel();
		runCancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => worker.RunAsync(runCancellationTokenSource.Token, shutdownCancellationTokenSource.Token)).WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Contains(logger.Events, static entry => entry.EventId == "supervisor.worker.lifecycle_warning");
	}

	/// <summary>
	/// Verifies non-cooperative shutdown cancellation exceptions remain warning-level lifecycle diagnostics.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldLogWarning_WhenShutdownCancellationIsNonCooperative()
	{
		RecordingLogger logger = new();
		FilesystemEventDaemonWorker worker = CreateWorker(new ThrowingShutdownCancellationLifecycle(), logger);
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => worker.RunAsync(cancellationTokenSource.Token, CancellationToken.None)).WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Contains(logger.Events, static entry => entry.EventId == "supervisor.worker.lifecycle_warning");
	}

	/// <summary>
	/// Verifies fatal shutdown-lifecycle exceptions are rethrown.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldRethrow_WhenShutdownLifecycleThrowsFatalException()
	{
		RecordingLogger logger = new();
		FilesystemEventDaemonWorker worker = CreateWorker(new ThrowingFatalShutdownLifecycle(), logger);
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAsync<OutOfMemoryException>(
			() => worker.RunAsync(cancellationTokenSource.Token)).WaitAsync(TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// Verifies fatal pipeline-dispose exceptions are rethrown.
	/// </summary>
	[Fact]
	public async Task RunAsync_Failure_ShouldRethrow_WhenTriggerPipelineDisposeThrowsFatalException()
	{
		RecordingLogger logger = new();
		FilesystemEventDaemonWorker worker = CreateWorkerWithInotifyReader(new ThrowingFatalDisposeInotifyReader(), new RecordingLifecycle(), logger);
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(20));

		await Assert.ThrowsAsync<OutOfMemoryException>(
			() => worker.RunAsync(cancellationTokenSource.Token)).WaitAsync(TimeSpan.FromSeconds(5));
	}

	/// <summary>
	/// Creates a worker instance with deterministic pipeline fakes.
	/// </summary>
	/// <param name="lifecycle">Lifecycle hook dependency.</param>
	/// <param name="logger">Logger dependency.</param>
	/// <returns>Worker instance.</returns>
	private static FilesystemEventDaemonWorker CreateWorker(IMergeRuntimeLifecycle lifecycle, RecordingLogger logger)
	{
		return CreateWorkerWithInotifyReader(new EmptyInotifyReader(), lifecycle, logger);
	}

	/// <summary>
	/// Creates a worker instance with deterministic fakes and configurable inotify reader.
	/// </summary>
	/// <param name="inotifyEventReader">Inotify reader dependency.</param>
	/// <param name="lifecycle">Lifecycle hook dependency.</param>
	/// <param name="logger">Logger dependency.</param>
	/// <returns>Worker instance.</returns>
	private static FilesystemEventDaemonWorker CreateWorkerWithInotifyReader(
		IInotifyEventReader inotifyEventReader,
		IMergeRuntimeLifecycle lifecycle,
		RecordingLogger logger)
	{
		ChapterRenameOptions renameOptions = new(
			"/ssm/sources",
			renameDelaySeconds: 0,
			renameQuietSeconds: 0,
			renamePollSeconds: 1,
			renameRescanSeconds: 1,
			[]);
		FilesystemEventTriggerOptions triggerOptions = new(
			renameOptions,
			"/ssm/override",
			inotifyPollSeconds: 1,
			mergeIntervalSeconds: 1,
			mergeMinSecondsBetweenScans: 0,
			mergeLockRetrySeconds: 1,
			startupRenameRescanEnabled: false);
		FilesystemEventTriggerPipeline pipeline = new(
			triggerOptions,
			inotifyEventReader,
			new NoOpRenameProcessor(),
			new NoOpCoalescer(),
			logger);

		return new FilesystemEventDaemonWorker(pipeline, lifecycle, logger);
	}

	/// <summary>
	/// Lifecycle test double that records lifecycle call counts.
	/// </summary>
	private sealed class RecordingLifecycle : IMergeRuntimeLifecycle
	{
		/// <summary>
		/// Gets start call count.
		/// </summary>
		public int StartCalls
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets stop call count.
		/// </summary>
		public int StopCalls
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets a value indicating whether the stop-hook token was already cancellation requested.
		/// </summary>
		public bool StopCancellationRequested
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public void OnWorkerStarting(CancellationToken cancellationToken = default)
		{
			StartCalls++;
			cancellationToken.ThrowIfCancellationRequested();
		}

		/// <inheritdoc />
		public void OnWorkerStopping(CancellationToken cancellationToken = default)
		{
			StopCalls++;
			StopCancellationRequested = cancellationToken.IsCancellationRequested;
		}
	}

	/// <summary>
	/// Lifecycle test double that throws during startup.
	/// </summary>
	private sealed class ThrowingStartupLifecycle : IMergeRuntimeLifecycle
	{
		/// <inheritdoc />
		public void OnWorkerStarting(CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("startup failure");
		}

		/// <inheritdoc />
		public void OnWorkerStopping(CancellationToken cancellationToken = default)
		{
		}
	}

	/// <summary>
	/// Lifecycle test double that throws during shutdown.
	/// </summary>
	private sealed class ThrowingShutdownLifecycle : IMergeRuntimeLifecycle
	{
		/// <inheritdoc />
		public void OnWorkerStarting(CancellationToken cancellationToken = default)
		{
		}

		/// <inheritdoc />
		public void OnWorkerStopping(CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("shutdown failure");
		}
	}

	/// <summary>
	/// Lifecycle test double that throws cancellation during shutdown hook execution.
	/// </summary>
	private sealed class ThrowingShutdownCancellationLifecycle : IMergeRuntimeLifecycle
	{
		/// <inheritdoc />
		public void OnWorkerStarting(CancellationToken cancellationToken = default)
		{
		}

		/// <inheritdoc />
		public void OnWorkerStopping(CancellationToken cancellationToken = default)
		{
			throw new OperationCanceledException("shutdown canceled", cancellationToken);
		}
	}

	/// <summary>
	/// Lifecycle test double that throws cancellation with a different token during shutdown hook execution.
	/// </summary>
	private sealed class ThrowingMismatchedShutdownCancellationLifecycle : IMergeRuntimeLifecycle
	{
		/// <inheritdoc />
		public void OnWorkerStarting(CancellationToken cancellationToken = default)
		{
		}

		/// <inheritdoc />
		public void OnWorkerStopping(CancellationToken cancellationToken = default)
		{
			using CancellationTokenSource differentTokenSource = new();
			differentTokenSource.Cancel();
			throw new OperationCanceledException("mismatched shutdown token", differentTokenSource.Token);
		}
	}

	/// <summary>
	/// Lifecycle test double that throws a fatal exception during shutdown.
	/// </summary>
	private sealed class ThrowingFatalShutdownLifecycle : IMergeRuntimeLifecycle
	{
		/// <inheritdoc />
		public void OnWorkerStarting(CancellationToken cancellationToken = default)
		{
		}

		/// <inheritdoc />
		public void OnWorkerStopping(CancellationToken cancellationToken = default)
		{
			throw new OutOfMemoryException("fatal-shutdown-lifecycle");
		}
	}

	/// <summary>
	/// Inotify reader fake that returns an empty success result.
	/// </summary>
	private sealed class EmptyInotifyReader : IInotifyEventReader
	{
		/// <inheritdoc />
		public InotifyPollResult Poll(
			IReadOnlyList<string> watchRoots,
			TimeSpan timeout,
			CancellationToken cancellationToken = default)
		{
			return new InotifyPollResult(InotifyPollOutcome.Success, [], []);
		}
	}

	/// <summary>
	/// Inotify reader fake that throws a fatal exception during dispose.
	/// </summary>
	private sealed class ThrowingFatalDisposeInotifyReader : IInotifyEventReader, IDisposable
	{
		/// <inheritdoc />
		public InotifyPollResult Poll(
			IReadOnlyList<string> watchRoots,
			TimeSpan timeout,
			CancellationToken cancellationToken = default)
		{
			return new InotifyPollResult(InotifyPollOutcome.Success, [], []);
		}

		/// <inheritdoc />
		public void Dispose()
		{
			throw new OutOfMemoryException("fatal-pipeline-dispose");
		}
	}

	/// <summary>
	/// Rename processor fake that performs no-op behavior.
	/// </summary>
	private sealed class NoOpRenameProcessor : IChapterRenameQueueProcessor
	{
		/// <inheritdoc />
		public bool EnqueueChapterPath(string chapterPath)
		{
			return true;
		}

		/// <inheritdoc />
		public int EnqueueChaptersUnderSourcePath(string sourcePath)
		{
			return 0;
		}

		/// <inheritdoc />
		public int EnqueueChaptersUnderMangaPath(string mangaPath)
		{
			return 0;
		}

		/// <inheritdoc />
		public ChapterRenameProcessResult ProcessOnce()
		{
			return new ChapterRenameProcessResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
		}

		/// <inheritdoc />
		public ChapterRenameRescanResult RescanAndEnqueue()
		{
			return new ChapterRenameRescanResult(0, 0);
		}
	}

	/// <summary>
	/// Coalescer fake that always returns no pending request.
	/// </summary>
	private sealed class NoOpCoalescer : IMergeScanRequestCoalescer
	{
		/// <inheritdoc />
		public bool HasPendingRequest => false;

		/// <inheritdoc />
		public void RequestScan(string reason, bool force = false)
		{
		}

		/// <inheritdoc />
		public MergeScanDispatchOutcome DispatchPending(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
		{
			return MergeScanDispatchOutcome.NoPendingRequest;
		}
	}
}
