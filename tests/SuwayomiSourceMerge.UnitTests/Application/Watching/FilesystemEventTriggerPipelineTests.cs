namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FilesystemEventTriggerPipeline"/>.
/// </summary>
public sealed class FilesystemEventTriggerPipelineTests
{
	/// <summary>
	/// Verifies chapter directory events enqueue rename work and queue merge requests.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldEnqueueChapterAndQueueMergeRequest_WhenChapterEventArrives()
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
		Assert.Equal(1, result.MergeRequestsQueued);
		Assert.Equal(1, result.RenameProcessRuns);
		Assert.Equal(MergeScanDispatchOutcome.Success, result.MergeDispatchOutcome);
		Assert.Single(renameProcessor.EnqueuedPaths);
		Assert.Single(coalescer.Requests);
		Assert.Equal("chapter-implied-new:SourceA/MangaA", coalescer.Requests[0].Reason);
	}

	/// <summary>
	/// Verifies source-directory events enqueue nested chapter paths immediately.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldEnqueueNestedChapters_WhenNewSourceDirectoryEventArrives()
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
		Assert.Single(coalescer.Requests);
		Assert.Equal("new-source:SourceA", coalescer.Requests[0].Reason);
	}

	/// <summary>
	/// Verifies manga-directory events enqueue nested chapter paths immediately.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldEnqueueNestedChapters_WhenNewMangaDirectoryEventArrives()
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
		Assert.Single(coalescer.Requests);
		Assert.Equal("new-manga:SourceA/MangaA", coalescer.Requests[0].Reason);
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
		Assert.Equal(0, result.MergeRequestsQueued);
		Assert.Empty(renameProcessor.EnqueuedPaths);
		Assert.Empty(renameProcessor.EnqueuedSourcePaths);
		Assert.Empty(renameProcessor.EnqueuedMangaPaths);
		Assert.Empty(coalescer.Requests);
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

	/// <summary>
	/// Creates default trigger options for tests.
	/// </summary>
	/// <param name="startupRenameRescanEnabled">Startup rename rescan flag.</param>
	/// <returns>Constructed options instance.</returns>
	private static FilesystemEventTriggerOptions CreateOptions(bool startupRenameRescanEnabled)
	{
		ChapterRenameOptions renameOptions = new(
			"/ssm/sources",
			renameDelaySeconds: 300,
			renameQuietSeconds: 120,
			renamePollSeconds: 20,
			renameRescanSeconds: 172800,
			["Local source"]);
		return new FilesystemEventTriggerOptions(
			renameOptions,
			"/ssm/override",
			inotifyPollSeconds: 5,
			mergeIntervalSeconds: 3600,
			mergeMinSecondsBetweenScans: 15,
			mergeLockRetrySeconds: 30,
			startupRenameRescanEnabled);
	}

	/// <summary>
	/// Inotify reader stub returning one configured poll result.
	/// </summary>
	private sealed class StubInotifyEventReader : IInotifyEventReader
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="StubInotifyEventReader"/> class.
		/// </summary>
		/// <param name="result">Result returned by <see cref="Poll"/>.</param>
		public StubInotifyEventReader(InotifyPollResult result, Action<CancellationToken>? onPoll = null)
		{
			Result = result ?? throw new ArgumentNullException(nameof(result));
			OnPoll = onPoll;
		}

		/// <summary>
		/// Gets configured result.
		/// </summary>
		public InotifyPollResult Result
		{
			get;
		}

		/// <summary>
		/// Optional callback executed when poll is invoked.
		/// </summary>
		public Action<CancellationToken>? OnPoll
		{
			get;
		}

		/// <inheritdoc />
		public InotifyPollResult Poll(
			IReadOnlyList<string> watchRoots,
			TimeSpan timeout,
			CancellationToken cancellationToken = default)
		{
			OnPoll?.Invoke(cancellationToken);
			return Result;
		}
	}

	/// <summary>
	/// Rename queue processor fake recording method calls.
	/// </summary>
	private sealed class RecordingChapterRenameQueueProcessor : IChapterRenameQueueProcessor
	{
		/// <summary>
		/// Gets enqueued chapter paths.
		/// </summary>
		public List<string> EnqueuedPaths
		{
			get;
		} = [];

		/// <summary>
		/// Gets source paths passed to recursive enqueue.
		/// </summary>
		public List<string> EnqueuedSourcePaths
		{
			get;
		} = [];

		/// <summary>
		/// Gets manga paths passed to recursive enqueue.
		/// </summary>
		public List<string> EnqueuedMangaPaths
		{
			get;
		} = [];

		/// <summary>
		/// Delegate used by source recursive enqueue.
		/// </summary>
		public Func<string, int> EnqueueChaptersUnderSourcePathHandler
		{
			get;
			set;
		} = static _ => 0;

		/// <summary>
		/// Delegate used by manga recursive enqueue.
		/// </summary>
		public Func<string, int> EnqueueChaptersUnderMangaPathHandler
		{
			get;
			set;
		} = static _ => 0;

		/// <summary>
		/// Gets number of <see cref="ProcessOnce"/> calls.
		/// </summary>
		public int ProcessCalls
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets number of <see cref="RescanAndEnqueue"/> calls.
		/// </summary>
		public int RescanCalls
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public bool EnqueueChapterPath(string chapterPath)
		{
			EnqueuedPaths.Add(chapterPath);
			return true;
		}

		/// <inheritdoc />
		public int EnqueueChaptersUnderMangaPath(string mangaPath)
		{
			EnqueuedMangaPaths.Add(mangaPath);
			return EnqueueChaptersUnderMangaPathHandler(mangaPath);
		}

		/// <inheritdoc />
		public int EnqueueChaptersUnderSourcePath(string sourcePath)
		{
			EnqueuedSourcePaths.Add(sourcePath);
			return EnqueueChaptersUnderSourcePathHandler(sourcePath);
		}

		/// <inheritdoc />
		public ChapterRenameProcessResult ProcessOnce()
		{
			ProcessCalls++;
			return new ChapterRenameProcessResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
		}

		/// <inheritdoc />
		public ChapterRenameRescanResult RescanAndEnqueue()
		{
			RescanCalls++;
			return new ChapterRenameRescanResult(0, 0);
		}
	}

	/// <summary>
	/// Merge request coalescer fake recording queued requests and returning one configured dispatch outcome.
	/// </summary>
	private sealed class RecordingMergeScanRequestCoalescer : IMergeScanRequestCoalescer
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingMergeScanRequestCoalescer"/> class.
		/// </summary>
		/// <param name="dispatchOutcome">Outcome returned by <see cref="DispatchPending"/>.</param>
		public RecordingMergeScanRequestCoalescer(MergeScanDispatchOutcome dispatchOutcome)
		{
			DispatchOutcome = dispatchOutcome;
		}

		/// <summary>
		/// Gets queued request records.
		/// </summary>
		public List<(string Reason, bool Force)> Requests
		{
			get;
		} = [];

		/// <summary>
		/// Gets dispatch outcome returned by <see cref="DispatchPending"/>.
		/// </summary>
		public MergeScanDispatchOutcome DispatchOutcome
		{
			get;
		}

		/// <inheritdoc />
		public bool HasPendingRequest
		{
			get
			{
				return Requests.Count > 0;
			}
		}

		/// <inheritdoc />
		public MergeScanDispatchOutcome DispatchPending(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
		{
			return DispatchOutcome;
		}

		/// <inheritdoc />
		public void RequestScan(string reason, bool force = false)
		{
			Requests.Add((reason, force));
		}
	}

	/// <summary>
	/// Minimal logger implementation for pipeline tests.
	/// </summary>
	private sealed class RecordingLogger : ISsmLogger
	{
		/// <summary>
		/// Gets captured log events.
		/// </summary>
		public List<(LogLevel Level, string EventId, string Message)> Events
		{
			get;
		} = [];

		/// <inheritdoc />
		public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Debug, eventId, message));
		}

		/// <inheritdoc />
		public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Error, eventId, message));
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel level)
		{
			return true;
		}

		/// <inheritdoc />
		public void Log(LogLevel level, string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((level, eventId, message));
		}

		/// <inheritdoc />
		public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Trace, eventId, message));
		}

		/// <inheritdoc />
		public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Events.Add((LogLevel.Warning, eventId, message));
		}
	}
}
