namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;

public sealed partial class FilesystemEventTriggerPipelineTests
{
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

		/// <summary>
		/// Gets or sets optional pending-request override used by edge-case tests.
		/// </summary>
		public bool? HasPendingRequestOverride
		{
			get;
			set;
		}

		/// <summary>
		/// Gets dispatch call count.
		/// </summary>
		public int DispatchCalls
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public bool HasPendingRequest
		{
			get
			{
				return HasPendingRequestOverride ?? Requests.Count > 0;
			}
		}

		/// <inheritdoc />
		public MergeScanDispatchOutcome DispatchPending(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
		{
			DispatchCalls++;
			return DispatchOutcome;
		}

		/// <inheritdoc />
		public void RequestScan(string reason, bool force = false)
		{
			Requests.Add((reason, force));
		}
	}
}
