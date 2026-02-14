namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Integration-style tests covering burst event handling and merge-request coalescing behavior.
/// </summary>
public sealed class FilesystemEventTriggerPipelineIntegrationTests
{
	/// <summary>
	/// Verifies burst chapter events are coalesced to one merge dispatch per gating window.
	/// </summary>
	[Fact]
	public void Tick_Expected_ShouldCoalesceBurstMergeRequests_UnderLoad()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		SequenceInotifyEventReader eventReader = new(
			new InotifyPollResult(
				InotifyPollOutcome.Success,
				BuildBurstChapterEvents(250),
				[]),
			new InotifyPollResult(
				InotifyPollOutcome.Success,
				BuildBurstChapterEvents(250),
				[]),
			new InotifyPollResult(
				InotifyPollOutcome.Success,
				[],
				[]));
		RecordingMergeScanRequestHandler handler = new();
		MergeScanRequestCoalescer coalescer = new(handler, minSecondsBetweenScans: 15, retryDelaySeconds: 30);
		FilesystemEventTriggerPipeline pipeline = new(
			CreateOptions(startupRenameRescanEnabled: false),
			eventReader,
			new AcceptingChapterRenameQueueProcessor(),
			coalescer,
			new NullLogger());

		FilesystemEventTickResult firstTick = pipeline.Tick(now);
		FilesystemEventTickResult secondTick = pipeline.Tick(now.AddSeconds(5));
		FilesystemEventTickResult thirdTick = pipeline.Tick(now.AddSeconds(20));

		Assert.Equal(MergeScanDispatchOutcome.Success, firstTick.MergeDispatchOutcome);
		Assert.Equal(MergeScanDispatchOutcome.SkippedDueToMinInterval, secondTick.MergeDispatchOutcome);
		Assert.Equal(MergeScanDispatchOutcome.Success, thirdTick.MergeDispatchOutcome);
		Assert.Equal(2, handler.DispatchCalls);
	}

	/// <summary>
	/// Builds chapter-create burst events for one source/manga pair.
	/// </summary>
	/// <param name="count">Event count.</param>
	/// <returns>Event list.</returns>
	private static IReadOnlyList<InotifyEventRecord> BuildBurstChapterEvents(int count)
	{
		List<InotifyEventRecord> events = new(count);
		for (int index = 0; index < count; index++)
		{
			events.Add(
				new InotifyEventRecord(
					$"/ssm/sources/SourceA/MangaA/Ch-{index:D4}",
					InotifyEventMask.Create | InotifyEventMask.IsDirectory,
					"CREATE,ISDIR"));
		}

		return events;
	}

	/// <summary>
	/// Creates trigger options used by integration-style tests.
	/// </summary>
	/// <param name="startupRenameRescanEnabled">Startup rename rescan flag.</param>
	/// <returns>Options instance.</returns>
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
	/// Inotify reader that returns configured results in sequence.
	/// </summary>
	private sealed class SequenceInotifyEventReader : IInotifyEventReader
	{
		/// <summary>
		/// Result queue.
		/// </summary>
		private readonly Queue<InotifyPollResult> _results;

		/// <summary>
		/// Initializes a new instance of the <see cref="SequenceInotifyEventReader"/> class.
		/// </summary>
		/// <param name="results">Result sequence.</param>
		public SequenceInotifyEventReader(params InotifyPollResult[] results)
		{
			ArgumentNullException.ThrowIfNull(results);
			_results = new Queue<InotifyPollResult>(results);
		}

		/// <inheritdoc />
		public InotifyPollResult Poll(
			IReadOnlyList<string> watchRoots,
			TimeSpan timeout,
			CancellationToken cancellationToken = default)
		{
			if (_results.Count == 0)
			{
				return new InotifyPollResult(InotifyPollOutcome.Success, [], []);
			}

			return _results.Dequeue();
		}
	}

	/// <summary>
	/// Rename queue processor fake that accepts enqueue calls.
	/// </summary>
	private sealed class AcceptingChapterRenameQueueProcessor : IChapterRenameQueueProcessor
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
	/// Merge handler that records dispatch calls and always succeeds.
	/// </summary>
	private sealed class RecordingMergeScanRequestHandler : IMergeScanRequestHandler
	{
		/// <summary>
		/// Gets dispatch call count.
		/// </summary>
		public int DispatchCalls
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public MergeScanDispatchOutcome DispatchMergeScan(string reason, bool force, CancellationToken cancellationToken = default)
		{
			DispatchCalls++;
			return MergeScanDispatchOutcome.Success;
		}
	}

	/// <summary>
	/// Minimal logger that discards all events.
	/// </summary>
	private sealed class NullLogger : ISsmLogger
	{
		/// <inheritdoc />
		public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public bool IsEnabled(LogLevel level)
		{
			return false;
		}

		/// <inheritdoc />
		public void Log(LogLevel level, string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}

		/// <inheritdoc />
		public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
		}
	}
}
