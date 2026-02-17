using System.Globalization;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;

namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Coordinates inotify event routing, rename polling/rescan fallback, and merge-trigger coalescing.
/// </summary>
internal sealed class FilesystemEventTriggerPipeline
{
	/// <summary>Event id emitted for malformed or failed inotify polling operations.</summary>
	private const string InotifyWarningEvent = "watcher.inotify.warning";

	/// <summary>Event id emitted when one merge request is queued.</summary>
	private const string MergeRequestEvent = "watcher.merge.requested";

	/// <summary>Event id emitted for one per-tick summary line.</summary>
	private const string TickSummaryEvent = "watcher.tick.summary";

	/// <summary>
	/// Path comparison mode used by root containment checks.
	/// </summary>
	private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	/// <summary>
	/// Watcher options.
	/// </summary>
	private readonly FilesystemEventTriggerOptions _options;

	/// <summary>
	/// Inotify event reader dependency.
	/// </summary>
	private readonly IInotifyEventReader _inotifyEventReader;

	/// <summary>
	/// Rename queue processor dependency.
	/// </summary>
	private readonly IChapterRenameQueueProcessor _chapterRenameQueueProcessor;

	/// <summary>
	/// Merge request coalescer dependency.
	/// </summary>
	private readonly IMergeScanRequestCoalescer _mergeScanRequestCoalescer;

	/// <summary>
	/// Logger dependency.
	/// </summary>
	private readonly ISsmLogger _logger;

	/// <summary>
	/// Tracks whether schedule state has been initialized.
	/// </summary>
	private bool _initialized;

	/// <summary>
	/// Tracks whether startup rename rescan has already run.
	/// </summary>
	private bool _startupRenameRescanCompleted;

	/// <summary>
	/// Next scheduled rename queue process time.
	/// </summary>
	private DateTimeOffset _nextRenameProcessUtc;

	/// <summary>
	/// Next scheduled rename rescan time.
	/// </summary>
	private DateTimeOffset _nextRenameRescanUtc;

	/// <summary>
	/// Next scheduled periodic merge-request time.
	/// </summary>
	private DateTimeOffset _nextMergeIntervalRequestUtc;

	/// <summary>
	/// Tracks seen source names for chapter-implied-new routing behavior.
	/// </summary>
	private readonly HashSet<string> _seenSources = new(StringComparer.Ordinal);

	/// <summary>
	/// Tracks seen source/manga pairs for chapter-implied-new routing behavior.
	/// </summary>
	private readonly HashSet<string> _seenSourceMangaKeys = new(StringComparer.Ordinal);

	/// <summary>
	/// Initializes a new instance of the <see cref="FilesystemEventTriggerPipeline"/> class.
	/// </summary>
	/// <param name="options">Trigger pipeline options.</param>
	/// <param name="inotifyEventReader">Inotify event reader.</param>
	/// <param name="chapterRenameQueueProcessor">Rename queue processor.</param>
	/// <param name="mergeScanRequestCoalescer">Merge request coalescer.</param>
	/// <param name="logger">Logger instance.</param>
	public FilesystemEventTriggerPipeline(
		FilesystemEventTriggerOptions options,
		IInotifyEventReader inotifyEventReader,
		IChapterRenameQueueProcessor chapterRenameQueueProcessor,
		IMergeScanRequestCoalescer mergeScanRequestCoalescer,
		ISsmLogger logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_inotifyEventReader = inotifyEventReader ?? throw new ArgumentNullException(nameof(inotifyEventReader));
		_chapterRenameQueueProcessor = chapterRenameQueueProcessor ?? throw new ArgumentNullException(nameof(chapterRenameQueueProcessor));
		_mergeScanRequestCoalescer = mergeScanRequestCoalescer ?? throw new ArgumentNullException(nameof(mergeScanRequestCoalescer));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Runs one full trigger-pipeline tick.
	/// </summary>
	/// <param name="nowUtc">Current UTC timestamp used by scheduling and coalescing gates.</param>
	/// <param name="cancellationToken">Cancellation token used by polling/dispatch operations.</param>
	/// <returns>Tick summary result.</returns>
	public FilesystemEventTickResult Tick(DateTimeOffset nowUtc, CancellationToken cancellationToken = default)
	{
		cancellationToken.ThrowIfCancellationRequested();
		EnsureInitialized(nowUtc);

		InotifyPollResult pollResult = _inotifyEventReader.Poll(
			[_options.SourcesRootPath, _options.OverrideRootPath],
			TimeSpan.FromSeconds(_options.InotifyPollSeconds),
			cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();

		LogPollWarnings(pollResult);

		int enqueuedChapterPaths = 0;
		int mergeRequestsQueued = 0;
		for (int index = 0; index < pollResult.Events.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			InotifyEventRecord eventRecord = pollResult.Events[index];
			(int enqueuedCount, int mergeCount) = RouteEvent(eventRecord);
			enqueuedChapterPaths += enqueuedCount;
			mergeRequestsQueued += mergeCount;
		}

		int renameProcessRuns = 0;
		int renameRescanRuns = 0;

		if (!_startupRenameRescanCompleted && _options.StartupRenameRescanEnabled)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_chapterRenameQueueProcessor.RescanAndEnqueue();
			_startupRenameRescanCompleted = true;
			renameRescanRuns++;
		}

		while (nowUtc >= _nextRenameProcessUtc)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_chapterRenameQueueProcessor.ProcessOnce();
			renameProcessRuns++;
			_nextRenameProcessUtc = AdvanceSchedule(_nextRenameProcessUtc, TimeSpan.FromSeconds(_options.RenameOptions.RenamePollSeconds), nowUtc);
		}

		while (nowUtc >= _nextRenameRescanUtc)
		{
			cancellationToken.ThrowIfCancellationRequested();
			_chapterRenameQueueProcessor.RescanAndEnqueue();
			renameRescanRuns++;
			_nextRenameRescanUtc = AdvanceSchedule(_nextRenameRescanUtc, TimeSpan.FromSeconds(_options.RenameOptions.RenameRescanSeconds), nowUtc);
		}

		if (nowUtc >= _nextMergeIntervalRequestUtc)
		{
			cancellationToken.ThrowIfCancellationRequested();
			mergeRequestsQueued += QueueMergeRequest("interval elapsed", force: false);
			_nextMergeIntervalRequestUtc = AdvanceSchedule(_nextMergeIntervalRequestUtc, TimeSpan.FromSeconds(_options.MergeIntervalSeconds), nowUtc);
		}

		cancellationToken.ThrowIfCancellationRequested();
		MergeScanDispatchOutcome dispatchOutcome = _mergeScanRequestCoalescer.DispatchPending(nowUtc, cancellationToken);
		FilesystemEventTickResult result = new(
			pollResult.Outcome,
			pollResult.Events.Count,
			pollResult.Warnings.Count,
			enqueuedChapterPaths,
			mergeRequestsQueued,
			renameProcessRuns,
			renameRescanRuns,
			dispatchOutcome);

		_logger.Debug(
			TickSummaryEvent,
			"Completed filesystem event trigger tick.",
			BuildContext(
				("poll_outcome", result.PollOutcome.ToString()),
				("events", result.PolledEvents.ToString()),
				("warnings", result.PollWarnings.ToString()),
				("enqueued", result.EnqueuedChapterPaths.ToString()),
				("merge_requests", result.MergeRequestsQueued.ToString()),
				("rename_process_runs", result.RenameProcessRuns.ToString()),
				("rename_rescan_runs", result.RenameRescanRuns.ToString()),
				("dispatch_outcome", result.MergeDispatchOutcome.ToString())));

		return result;
	}

	/// <summary>
	/// Initializes recurring schedule state at first tick.
	/// </summary>
	/// <param name="nowUtc">Current tick timestamp.</param>
	private void EnsureInitialized(DateTimeOffset nowUtc)
	{
		if (_initialized)
		{
			return;
		}

		_nextRenameProcessUtc = nowUtc;
		_nextRenameRescanUtc = nowUtc + TimeSpan.FromSeconds(_options.RenameOptions.RenameRescanSeconds);
		_nextMergeIntervalRequestUtc = nowUtc + TimeSpan.FromSeconds(_options.MergeIntervalSeconds);
		_initialized = true;
	}

	/// <summary>
	/// Logs warnings from one inotify poll result.
	/// </summary>
	/// <param name="pollResult">Poll result.</param>
	private void LogPollWarnings(InotifyPollResult pollResult)
	{
		for (int index = 0; index < pollResult.Warnings.Count; index++)
		{
			_logger.Warning(
				InotifyWarningEvent,
				pollResult.Warnings[index],
				BuildContext(("outcome", pollResult.Outcome.ToString())));
		}
	}

	/// <summary>
	/// Routes one parsed event into rename enqueue and merge request actions.
	/// </summary>
	/// <param name="eventRecord">Parsed event record.</param>
	/// <returns>Tuple with enqueue count and merge-request count.</returns>
	private (int EnqueuedCount, int MergeRequestCount) RouteEvent(InotifyEventRecord eventRecord)
	{
		if (TryGetRelativePath(_options.OverrideRootPath, eventRecord.Path, out string overrideRelativePath))
		{
			return HandleOverrideEvent(eventRecord, overrideRelativePath);
		}

		if (TryGetRelativePath(_options.SourcesRootPath, eventRecord.Path, out string sourceRelativePath))
		{
			return HandleSourceEvent(eventRecord, sourceRelativePath);
		}

		return (0, 0);
	}

	/// <summary>
	/// Handles one override-root event.
	/// </summary>
	/// <param name="eventRecord">Parsed event record.</param>
	/// <param name="relativePath">Path relative to override root.</param>
	/// <returns>Tuple with enqueue count and merge-request count.</returns>
	private (int EnqueuedCount, int MergeRequestCount) HandleOverrideEvent(InotifyEventRecord eventRecord, string relativePath)
	{
		string[] segments = SplitPathSegments(relativePath);
		if (segments.Length == 0)
		{
			return (0, 0);
		}

		string titleSegment = segments[0];
		if (string.IsNullOrWhiteSpace(titleSegment))
		{
			return (0, 0);
		}

		bool force = ShouldForceOverrideMergeRequest(eventRecord.EventMask);
		string reason = force
			? string.Create(CultureInfo.InvariantCulture, $"override-force:{titleSegment}")
			: string.Create(CultureInfo.InvariantCulture, $"override:{titleSegment}");

		return (0, QueueMergeRequest(reason, force));
	}

	/// <summary>
	/// Handles one source-root event.
	/// </summary>
	/// <param name="eventRecord">Parsed event record.</param>
	/// <param name="relativePath">Path relative to sources root.</param>
	/// <returns>Tuple with enqueue count and merge-request count.</returns>
	private (int EnqueuedCount, int MergeRequestCount) HandleSourceEvent(InotifyEventRecord eventRecord, string relativePath)
	{
		if (!eventRecord.IsDirectory)
		{
			return (0, 0);
		}

		if (HasAny(eventRecord.EventMask, InotifyEventMask.Delete | InotifyEventMask.MovedFrom))
		{
			return (0, 0);
		}

		string[] segments = SplitPathSegments(relativePath);
		if (segments.Length == 0 || segments.Length > 3)
		{
			return (0, 0);
		}

		string sourceName = segments[0];
		if (_options.RenameOptions.IsExcludedSource(sourceName))
		{
			return (0, 0);
		}

		if (segments.Length == 1)
		{
			_seenSources.Add(sourceName);
			int sourceEnqueuedCount = _chapterRenameQueueProcessor.EnqueueChaptersUnderSourcePath(eventRecord.Path);
			return (sourceEnqueuedCount, QueueMergeRequest($"new-source:{sourceName}", force: false));
		}

		string mangaName = segments[1];
		if (segments.Length == 2)
		{
			_seenSources.Add(sourceName);
			_seenSourceMangaKeys.Add(BuildSourceMangaKey(sourceName, mangaName));
			int mangaEnqueuedCount = _chapterRenameQueueProcessor.EnqueueChaptersUnderMangaPath(eventRecord.Path);
			return (mangaEnqueuedCount, QueueMergeRequest($"new-manga:{sourceName}/{mangaName}", force: false));
		}

		string sourceMangaKey = BuildSourceMangaKey(sourceName, mangaName);
		bool knownSource = _seenSources.Contains(sourceName);
		bool knownManga = _seenSourceMangaKeys.Contains(sourceMangaKey);
		_seenSources.Add(sourceName);
		_seenSourceMangaKeys.Add(sourceMangaKey);

		int enqueuedCount = _chapterRenameQueueProcessor.EnqueueChapterPath(eventRecord.Path) ? 1 : 0;
		if (!knownSource || !knownManga)
		{
			return (enqueuedCount, QueueMergeRequest($"chapter-implied-new:{sourceName}/{mangaName}", force: false));
		}

		if (HasAny(eventRecord.EventMask, InotifyEventMask.Create | InotifyEventMask.MovedTo))
		{
			return (enqueuedCount, QueueMergeRequest($"chapter-newdir:{sourceName}/{mangaName}", force: false));
		}

		return (enqueuedCount, 0);
	}

	/// <summary>
	/// Determines whether an override event should use force-request behavior.
	/// </summary>
	/// <param name="eventMask">Event mask.</param>
	/// <returns><see langword="true"/> when force request semantics should be used.</returns>
	private static bool ShouldForceOverrideMergeRequest(InotifyEventMask eventMask)
	{
		return HasAny(
			eventMask,
			InotifyEventMask.CloseWrite |
			InotifyEventMask.Attrib |
			InotifyEventMask.Create |
			InotifyEventMask.MovedTo);
	}

	/// <summary>
	/// Queues one merge scan request through the coalescer.
	/// </summary>
	/// <param name="reason">Reason text for diagnostics.</param>
	/// <param name="force">Force flag passed to coalescer.</param>
	/// <returns>Queued-request count.</returns>
	private int QueueMergeRequest(string reason, bool force)
	{
		_mergeScanRequestCoalescer.RequestScan(reason, force);
		_logger.Debug(
			MergeRequestEvent,
			"Queued merge scan request.",
			BuildContext(("reason", reason), ("force", force ? "true" : "false")));
		return 1;
	}

	/// <summary>
	/// Advances one scheduled timestamp forward until it exceeds the current time.
	/// </summary>
	/// <param name="scheduledUtc">Current scheduled timestamp.</param>
	/// <param name="interval">Scheduling interval.</param>
	/// <param name="nowUtc">Current time.</param>
	/// <returns>Next scheduled timestamp in the future.</returns>
	private static DateTimeOffset AdvanceSchedule(DateTimeOffset scheduledUtc, TimeSpan interval, DateTimeOffset nowUtc)
	{
		if (interval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(interval), "Schedule interval must be > 0.");
		}

		DateTimeOffset next = scheduledUtc;
		while (next <= nowUtc)
		{
			next += interval;
		}

		return next;
	}

	/// <summary>
	/// Builds a normalized source/manga composite key.
	/// </summary>
	/// <param name="sourceName">Source name.</param>
	/// <param name="mangaName">Manga name.</param>
	/// <returns>Composite key string.</returns>
	private static string BuildSourceMangaKey(string sourceName, string mangaName)
	{
		return string.Create(CultureInfo.InvariantCulture, $"{sourceName}/{mangaName}");
	}

	/// <summary>
	/// Returns whether one path is under one root and outputs root-relative path text.
	/// </summary>
	/// <param name="rootPath">Root path.</param>
	/// <param name="candidatePath">Candidate path.</param>
	/// <param name="relativePath">Relative path when containment is true.</param>
	/// <returns><see langword="true"/> when candidate is equal to or under root.</returns>
	private static bool TryGetRelativePath(string rootPath, string candidatePath, out string relativePath)
	{
		relativePath = string.Empty;
		string normalizedRoot = NormalizePath(rootPath);
		string normalizedCandidate = NormalizePath(candidatePath);
		if (string.Equals(normalizedRoot, normalizedCandidate, _pathComparison))
		{
			return true;
		}

		string prefix = normalizedRoot + Path.DirectorySeparatorChar;
		if (!normalizedCandidate.StartsWith(prefix, _pathComparison))
		{
			return false;
		}

		relativePath = normalizedCandidate[prefix.Length..];
		return true;
	}

	/// <summary>
	/// Normalizes one path for containment and equality checks.
	/// </summary>
	/// <param name="path">Input path.</param>
	/// <returns>Normalized full path.</returns>
	private static string NormalizePath(string path)
	{
		string normalized = Path.GetFullPath(Path.TrimEndingDirectorySeparator(path));
		return normalized.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
	}

	/// <summary>
	/// Splits one relative path into normalized segments.
	/// </summary>
	/// <param name="relativePath">Relative path text.</param>
	/// <returns>Segment array.</returns>
	private static string[] SplitPathSegments(string relativePath)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
		{
			return [];
		}

		return relativePath.Split(
			[Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}

	/// <summary>
	/// Returns whether one bit mask contains any of the specified flags.
	/// </summary>
	/// <param name="mask">Input mask.</param>
	/// <param name="values">Flags to probe.</param>
	/// <returns><see langword="true"/> when any flag is present.</returns>
	private static bool HasAny(InotifyEventMask mask, InotifyEventMask values)
	{
		return (mask & values) != 0;
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
