namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FilesystemEventTriggerPipeline"/>.
/// </summary>
public sealed partial class FilesystemEventTriggerPipelineTests
{
	/// <summary>
	/// Verifies chapter directory events enqueue rename work and keep startup-first request ordering deterministic.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldEnqueueChapterAndQueueMergeRequestInStartupFirstOrder_WhenChapterEventArrives()
	{
		StubInotifyEventReader eventReader = new(
			new InotifyPollResult(
				InotifyPollOutcome.Success,
				[
					new InotifyEventRecord(
						"/ssm/sources/SourceA/MangaA/Chapter001",
						InotifyEventMask.Create | InotifyEventMask.IsDirectory,
						"CREATE,ISDIR")
				],
				[]));
		RecordingChapterRenameQueueProcessor renameProcessor = new();
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.Success);
		RecordingLogger logger = new();
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			renameProcessor,
			coalescer,
			logger);

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(1, result.EnqueuedChapterPaths);
		Assert.Equal(2, result.MergeRequestsQueued);
		Assert.Equal(1, result.RenameProcessRuns);
		Assert.Equal(MergeScanDispatchOutcome.Success, result.MergeDispatchOutcome);
		Assert.Single(renameProcessor.EnqueuedPaths);
		Assert.Equal(2, coalescer.Requests.Count);
		Assert.Equal("startup", coalescer.Requests[0].Reason);
		Assert.Equal("chapter-implied-new:SourceA/MangaA", coalescer.Requests[1].Reason);
	}

	/// <summary>
	/// Verifies source-directory events enqueue nested chapter paths immediately while preserving startup-first ordering.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldEnqueueNestedChaptersInStartupFirstOrder_WhenNewSourceDirectoryEventArrives()
	{
		StubInotifyEventReader eventReader = new(
			new InotifyPollResult(
				InotifyPollOutcome.Success,
				[
					new InotifyEventRecord(
						"/ssm/sources/SourceA",
						InotifyEventMask.Create | InotifyEventMask.IsDirectory,
						"CREATE,ISDIR")
				],
				[]));
		RecordingChapterRenameQueueProcessor renameProcessor = new()
		{
			EnqueueChaptersUnderSourcePathHandler = static _ => 3
		};
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.Success);
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			renameProcessor,
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(3, result.EnqueuedChapterPaths);
		Assert.Single(renameProcessor.EnqueuedSourcePaths);
		Assert.Equal("/ssm/sources/SourceA", renameProcessor.EnqueuedSourcePaths[0]);
		Assert.Equal(2, coalescer.Requests.Count);
		Assert.Equal("startup", coalescer.Requests[0].Reason);
		Assert.Equal("new-source:SourceA", coalescer.Requests[1].Reason);
	}

	/// <summary>
	/// Verifies manga-directory events enqueue nested chapter paths immediately while preserving startup-first ordering.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldEnqueueNestedChaptersInStartupFirstOrder_WhenNewMangaDirectoryEventArrives()
	{
		StubInotifyEventReader eventReader = new(
			new InotifyPollResult(
				InotifyPollOutcome.Success,
				[
					new InotifyEventRecord(
						"/ssm/sources/SourceA/MangaA",
						InotifyEventMask.Create | InotifyEventMask.IsDirectory,
						"CREATE,ISDIR")
				],
				[]));
		RecordingChapterRenameQueueProcessor renameProcessor = new()
		{
			EnqueueChaptersUnderMangaPathHandler = static _ => 2
		};
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.Success);
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			renameProcessor,
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(2, result.EnqueuedChapterPaths);
		Assert.Single(renameProcessor.EnqueuedMangaPaths);
		Assert.Equal("/ssm/sources/SourceA/MangaA", renameProcessor.EnqueuedMangaPaths[0]);
		Assert.Equal(2, coalescer.Requests.Count);
		Assert.Equal("startup", coalescer.Requests[0].Reason);
		Assert.Equal("new-manga:SourceA/MangaA", coalescer.Requests[1].Reason);
	}

	/// <summary>
	/// Verifies first tick queues one startup merge request when no event-driven requests are present.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldQueueStartupMergeRequest_OnFirstTickWithoutEvents()
	{
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.Success);
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			new StubInotifyEventReader(new InotifyPollResult(InotifyPollOutcome.Success, [], [])),
			new RecordingChapterRenameQueueProcessor(),
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(1, result.MergeRequestsQueued);
		Assert.Single(coalescer.Requests);
		Assert.Equal("startup", coalescer.Requests[0].Reason);
		Assert.False(coalescer.Requests[0].Force);
	}

	/// <summary>
	/// Verifies startup merge dispatch runs before the first inotify poll on the first tick.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldDispatchStartupBeforePolling_OnFirstTick()
	{
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.Success);
		StubInotifyEventReader eventReader = new(
			new InotifyPollResult(InotifyPollOutcome.Success, [], []),
			_ => Assert.Equal(1, coalescer.DispatchCalls));
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			new RecordingChapterRenameQueueProcessor(),
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(1, result.MergeRequestsQueued);
		Assert.Equal(2, coalescer.DispatchCalls);
		Assert.Single(coalescer.Requests);
		Assert.Equal("startup", coalescer.Requests[0].Reason);
	}

	/// <summary>
	/// Verifies startup evaluation does not queue a duplicate startup request when one is already pending.
	/// </summary>
	[Fact]
	public void Tick_Edge_ShouldEvaluateStartupWithoutQueueingDuplicateRequest_WhenPendingRequestAlreadyExists()
	{
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.Success)
		{
			HasPendingRequestOverride = true
		};
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			new StubInotifyEventReader(new InotifyPollResult(InotifyPollOutcome.Success, [], [])),
			new RecordingChapterRenameQueueProcessor(),
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(0, result.MergeRequestsQueued);
		Assert.Empty(coalescer.Requests);
		Assert.Equal(2, coalescer.DispatchCalls);
	}

	/// <summary>
	/// Verifies startup dispatch failures are non-fatal and polling still executes on first tick.
	/// </summary>
	[Fact]
	public void Tick_Failure_ShouldContinuePolling_WhenStartupDispatchFails()
	{
		bool pollInvoked = false;
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.Failure);
		StubInotifyEventReader eventReader = new(
			new InotifyPollResult(InotifyPollOutcome.Success, [], []),
			_ => pollInvoked = true);
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			new RecordingChapterRenameQueueProcessor(),
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.True(pollInvoked);
		Assert.Equal(MergeScanDispatchOutcome.Failure, result.MergeDispatchOutcome);
		Assert.Equal(1, result.MergeRequestsQueued);
	}

	/// <summary>
	/// Verifies startup merge scheduling only occurs once and is not repeated on subsequent ticks.
	/// </summary>
	[Fact]
	public void Tick_Edge_ShouldNotQueueStartupMergeRequestMoreThanOnce_WhenNoEventsArrive()
	{
		DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.NoPendingRequest);
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			new StubInotifyEventReader(new InotifyPollResult(InotifyPollOutcome.Success, [], [])),
			new RecordingChapterRenameQueueProcessor(),
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult first = pipeline.Tick(nowUtc);
		FilesystemEventTickResult second = pipeline.Tick(nowUtc.AddSeconds(1));

		Assert.Equal(1, first.MergeRequestsQueued);
		Assert.Equal(0, second.MergeRequestsQueued);
		Assert.Single(coalescer.Requests);
		Assert.Equal("startup", coalescer.Requests[0].Reason);
	}

	/// <summary>
	/// Verifies excluded sources are ignored for enqueue and merge-request routing.
	/// </summary>
	[Fact]
	public void Tick_Edge_ShouldIgnoreExcludedSource_WhenSourceIsExcluded()
	{
		StubInotifyEventReader eventReader = new(
			new InotifyPollResult(
				InotifyPollOutcome.Success,
				[
					new InotifyEventRecord(
						"/ssm/sources/Local source/MangaA/Chapter001",
						InotifyEventMask.Create | InotifyEventMask.IsDirectory,
						"CREATE,ISDIR")
				],
				[]));
		RecordingChapterRenameQueueProcessor renameProcessor = new();
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.NoPendingRequest);
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			renameProcessor,
			coalescer,
			new RecordingLogger());

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(0, result.EnqueuedChapterPaths);
		Assert.Equal(1, result.MergeRequestsQueued);
		Assert.Empty(renameProcessor.EnqueuedPaths);
		Assert.Empty(renameProcessor.EnqueuedSourcePaths);
		Assert.Empty(renameProcessor.EnqueuedMangaPaths);
		Assert.Single(coalescer.Requests);
		Assert.Equal("startup", coalescer.Requests[0].Reason);
	}

	/// <summary>
	/// Verifies poll warnings are logged and tick continues when polling fails.
	/// </summary>
	[Fact]
	public void Tick_Failure_ShouldLogWarningsAndContinue_WhenPollReturnsFailureOutcome()
	{
		StubInotifyEventReader eventReader = new(
			new InotifyPollResult(
				InotifyPollOutcome.CommandFailed,
				[],
				["inotifywait failed"]));
		RecordingChapterRenameQueueProcessor renameProcessor = new();
		RecordingMergeScanRequestCoalescer coalescer = new(MergeScanDispatchOutcome.NoPendingRequest);
		RecordingLogger logger = new();
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			renameProcessor,
			coalescer,
			logger);

		FilesystemEventTickResult result = pipeline.Tick(DateTimeOffset.UtcNow);

		Assert.Equal(InotifyPollOutcome.CommandFailed, result.PollOutcome);
		Assert.Equal(1, result.PollWarnings);
		Assert.Equal(1, result.RenameProcessRuns);
		Assert.Contains(logger.Events, static entry => entry.Level == LogLevel.Warning);
	}

	/// <summary>
	/// Verifies cancellation is honored before the tick begins.
	/// </summary>
	[Fact]
	public void Tick_Failure_ShouldThrow_WhenCancellationRequestedBeforeTick()
	{
		RecordingChapterRenameQueueProcessor renameProcessor = new();
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			new StubInotifyEventReader(new InotifyPollResult(InotifyPollOutcome.Success, [], [])),
			renameProcessor,
			new RecordingMergeScanRequestCoalescer(MergeScanDispatchOutcome.NoPendingRequest),
			new RecordingLogger());
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		Assert.Throws<OperationCanceledException>(
			() => pipeline.Tick(DateTimeOffset.UtcNow, cancellationTokenSource.Token));
		Assert.Equal(0, renameProcessor.ProcessCalls);
	}

	/// <summary>
	/// Verifies cancellation requested after poll aborts the remaining tick work.
	/// </summary>
	[Fact]
	public void Tick_Failure_ShouldThrow_WhenCancellationRequestedAfterPoll()
	{
		using CancellationTokenSource cancellationTokenSource = new();
		RecordingChapterRenameQueueProcessor renameProcessor = new();
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			new StubInotifyEventReader(
				new InotifyPollResult(InotifyPollOutcome.Success, [], []),
				_ => cancellationTokenSource.Cancel()),
			renameProcessor,
			new RecordingMergeScanRequestCoalescer(MergeScanDispatchOutcome.NoPendingRequest),
			new RecordingLogger());

		Assert.Throws<OperationCanceledException>(
			() => pipeline.Tick(DateTimeOffset.UtcNow, cancellationTokenSource.Token));
		Assert.Equal(0, renameProcessor.ProcessCalls);
	}

}
