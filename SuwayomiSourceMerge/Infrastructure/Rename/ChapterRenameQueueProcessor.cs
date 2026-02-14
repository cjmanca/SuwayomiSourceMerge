using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Processes chapter rename queue entries using shell-parity sanitization rules.
/// </summary>
internal sealed partial class ChapterRenameQueueProcessor : IChapterRenameQueueProcessor
{
	/// <summary>Event id used when one chapter path is enqueued.</summary>
	private const string ENQUEUE_EVENT = "rename.queue.enqueued";

	/// <summary>Event id used for per-pass queue processing summaries.</summary>
	private const string PROCESS_SUMMARY_EVENT = "rename.queue.processed";

	/// <summary>Event id used for per-pass rescan summaries.</summary>
	private const string RESCAN_SUMMARY_EVENT = "rename.queue.rescan";

	/// <summary>Event id used when queue entries are dropped after missing-path grace windows expire.</summary>
	private const string MISSING_PATH_DROPPED_EVENT = "rename.queue.missing_dropped";

	/// <summary>Event id used when collision suffix options are exhausted.</summary>
	private const string COLLISION_EXHAUSTED_EVENT = "rename.collision_exhausted";

	/// <summary>Event id used when directory moves fail.</summary>
	private const string MOVE_FAILED_EVENT = "rename.move_failed";

	/// <summary>Event id used when a filesystem enumeration operation fails.</summary>
	private const string ENUMERATION_FAILED_EVENT = "rename.enumeration_failed";

	/// <summary>Event id used when one rename operation succeeds.</summary>
	private const string RENAMED_EVENT = "rename.directory.renamed";

	/// <summary>Collision suffix alphabet used by shell parity behavior.</summary>
	private const string COLLISION_SUFFIX_ALPHABET = "abcdefghijklmnopqrstuvwxyz";

	/// <summary>Filesystem adapter dependency.</summary>
	private readonly IChapterRenameFileSystem _fileSystem;

	/// <summary>Logger dependency for diagnostics.</summary>
	private readonly ISsmLogger _logger;

	/// <summary>Rename options.</summary>
	private readonly ChapterRenameOptions _options;

	/// <summary>Queue storage dependency.</summary>
	private readonly IChapterRenameQueueStore _queueStore;

	/// <summary>Sanitization dependency.</summary>
	private readonly IChapterRenameSanitizer _sanitizer;

	/// <summary>
	/// Initializes a new instance of the <see cref="ChapterRenameQueueProcessor"/> class.
	/// </summary>
	/// <param name="options">Chapter rename options.</param>
	/// <param name="sanitizer">Chapter-name sanitizer.</param>
	/// <param name="queueStore">Queue store implementation.</param>
	/// <param name="fileSystem">Filesystem adapter.</param>
	/// <param name="logger">Logger for diagnostics.</param>
	public ChapterRenameQueueProcessor(
		ChapterRenameOptions options,
		IChapterRenameSanitizer sanitizer,
		IChapterRenameQueueStore queueStore,
		IChapterRenameFileSystem fileSystem,
		ISsmLogger logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
		_queueStore = queueStore ?? throw new ArgumentNullException(nameof(queueStore));
		_fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public bool EnqueueChapterPath(string chapterPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(chapterPath);

		string normalizedChapterPath = _fileSystem.GetFullPath(Path.TrimEndingDirectorySeparator(chapterPath));
		if (!TryParseSourcePath(normalizedChapterPath, out string sourceName))
		{
			return false;
		}

		if (_options.IsExcludedSource(sourceName))
		{
			return false;
		}

		long allowAt = GetCurrentUnixSeconds() + _options.RenameDelaySeconds;
		bool queued = _queueStore.TryEnqueue(new ChapterRenameQueueEntry(allowAt, normalizedChapterPath));
		if (queued)
		{
			_logger.Debug(
				ENQUEUE_EVENT,
				"Queued chapter path for rename processing.",
				BuildContext(
					("path", normalizedChapterPath),
					("allow_at", allowAt.ToString())));
		}

		return queued;
	}

	/// <inheritdoc />
	public ChapterRenameProcessResult ProcessOnce()
	{
		long now = GetCurrentUnixSeconds();
		ChapterRenameProcessResult? processResult = null;
		_queueStore.Transform(entries =>
		{
			ProcessPassComputation computation = ComputeProcessPass(entries, now);
			processResult = computation.Result;
			return computation.RemainingEntries;
		});

		ChapterRenameProcessResult finalizedResult = processResult ?? throw new InvalidOperationException("Queue transform did not produce a process result.");

		_logger.Debug(
			PROCESS_SUMMARY_EVENT,
			"Processed chapter rename queue.",
			BuildContext(
				("processed", finalizedResult.ProcessedEntries.ToString()),
				("renamed", finalizedResult.RenamedEntries.ToString()),
				("unchanged", finalizedResult.UnchangedEntries.ToString()),
				("deferred_missing", finalizedResult.DeferredMissingEntries.ToString()),
				("dropped_missing", finalizedResult.DroppedMissingEntries.ToString()),
				("deferred_not_ready", finalizedResult.DeferredNotReadyEntries.ToString()),
				("deferred_not_quiet", finalizedResult.DeferredNotQuietEntries.ToString()),
				("collision_skipped", finalizedResult.CollisionSkippedEntries.ToString()),
				("move_failed", finalizedResult.MoveFailedEntries.ToString()),
				("remaining", finalizedResult.RemainingQueuedEntries.ToString())));

		return finalizedResult;
	}

	/// <inheritdoc />
	public ChapterRenameRescanResult RescanAndEnqueue()
	{
		long now = GetCurrentUnixSeconds();
		int candidates = 0;
		int enqueued = 0;
		HashSet<string> queuedPaths = SnapshotQueuedPaths();

		foreach (string sourcePath in EnumerateDirectoriesSafe(_options.SourcesRootPath))
		{
			string sourceName = Path.GetFileName(sourcePath);
			if (_options.IsExcludedSource(sourceName))
			{
				continue;
			}

			foreach (string mangaPath in EnumerateDirectoriesSafe(sourcePath))
			{
				foreach (string chapterPath in EnumerateDirectoriesSafe(mangaPath))
				{
					string chapterName = Path.GetFileName(chapterPath);
					string sanitizedName = _sanitizer.Sanitize(chapterName);
					if (string.Equals(chapterName, sanitizedName, StringComparison.Ordinal))
					{
						continue;
					}

					candidates++;
					string normalizedChapterPath = _fileSystem.GetFullPath(Path.TrimEndingDirectorySeparator(chapterPath));
					if (queuedPaths.Contains(normalizedChapterPath))
					{
						continue;
					}

					long allowAt = BuildAllowAtFromDirectoryTimestamp(normalizedChapterPath, now);
					if (_queueStore.TryEnqueue(new ChapterRenameQueueEntry(allowAt, normalizedChapterPath)))
					{
						enqueued++;
						queuedPaths.Add(normalizedChapterPath);
					}
				}
			}
		}

		ChapterRenameRescanResult result = new(candidates, enqueued);
		_logger.Debug(
			RESCAN_SUMMARY_EVENT,
			"Rescanned chapter directories for rename candidates.",
			BuildContext(
				("candidates", result.CandidateEntries.ToString()),
				("enqueued", result.EnqueuedEntries.ToString())));

		return result;
	}

	/// <summary>
	/// Builds an enqueue timestamp from one chapter directory timestamp.
	/// </summary>
	/// <param name="chapterPath">Chapter directory path.</param>
	/// <param name="nowUnixSeconds">Current timestamp in Unix seconds.</param>
	/// <returns>Computed allow-at timestamp.</returns>
	private long BuildAllowAtFromDirectoryTimestamp(string chapterPath, long nowUnixSeconds)
	{
		if (_fileSystem.TryGetLastWriteTimeUtc(chapterPath, out DateTimeOffset lastWrite))
		{
			long lastWriteUnixSeconds = lastWrite.ToUnixTimeSeconds();
			if (lastWriteUnixSeconds > 0)
			{
				long delayedFromWrite = lastWriteUnixSeconds + _options.RenameDelaySeconds;
				return Math.Max(nowUnixSeconds, delayedFromWrite);
			}
		}

		return nowUnixSeconds + _options.RenameDelaySeconds;
	}

	/// <summary>
	/// Returns whether one chapter directory has been quiet long enough to rename.
	/// </summary>
	/// <param name="chapterPath">Chapter directory path.</param>
	/// <param name="nowUnixSeconds">Current timestamp in Unix seconds.</param>
	/// <returns><see langword="true"/> when quiet requirements are met; otherwise <see langword="false"/>.</returns>
	private bool IsQuietEnough(string chapterPath, long nowUnixSeconds)
	{
		long latestWriteUnixSeconds = 0;

		foreach (string childPath in EnumerateFileSystemEntriesSafe(chapterPath))
		{
			if (_fileSystem.TryGetLastWriteTimeUtc(childPath, out DateTimeOffset childWriteTime))
			{
				latestWriteUnixSeconds = Math.Max(latestWriteUnixSeconds, childWriteTime.ToUnixTimeSeconds());
			}
		}

		if (latestWriteUnixSeconds <= 0)
		{
			if (!_fileSystem.TryGetLastWriteTimeUtc(chapterPath, out DateTimeOffset directoryWriteTime))
			{
				return false;
			}

			latestWriteUnixSeconds = directoryWriteTime.ToUnixTimeSeconds();
			if (latestWriteUnixSeconds <= 0)
			{
				return false;
			}
		}

		return nowUnixSeconds - latestWriteUnixSeconds >= _options.RenameQuietSeconds;
	}

	/// <summary>
	/// Resolves the destination name applying shell-parity collision suffix behavior.
	/// </summary>
	/// <param name="parentPath">Parent directory path.</param>
	/// <param name="sanitizedName">Sanitized base destination name.</param>
	/// <returns>Resolved destination name or empty string when collisions are exhausted.</returns>
	private string ResolveDestinationName(string parentPath, string sanitizedName)
	{
		string baseDestinationPath = Path.Combine(parentPath, sanitizedName);
		if (!_fileSystem.PathExists(baseDestinationPath))
		{
			return sanitizedName;
		}

		for (int index = 0; index < COLLISION_SUFFIX_ALPHABET.Length; index++)
		{
			char suffix = COLLISION_SUFFIX_ALPHABET[index];
			string candidateName = $"{sanitizedName}_alt-{suffix}";
			string candidatePath = Path.Combine(parentPath, candidateName);
			if (!_fileSystem.PathExists(candidatePath))
			{
				return candidateName;
			}
		}

		return string.Empty;
	}

	/// <summary>
	/// Safely enumerates direct child directories and converts any exception into a warning.
	/// </summary>
	/// <param name="path">Directory path to enumerate.</param>
	/// <returns>Array of direct child directory paths.</returns>
	private string[] EnumerateDirectoriesSafe(string path)
	{
		try
		{
			return _fileSystem.EnumerateDirectories(path).ToArray();
		}
		catch (Exception exception)
		{
			_logger.Warning(
				ENUMERATION_FAILED_EVENT,
				"Directory enumeration failed.",
				BuildContext(
					("path", path),
					("exception", exception.GetType().Name)));
			return [];
		}
	}

	/// <summary>
	/// Safely enumerates nested filesystem entries and converts any exception into a warning.
	/// </summary>
	/// <param name="path">Directory path to enumerate.</param>
	/// <returns>Array of nested filesystem entry paths.</returns>
	private string[] EnumerateFileSystemEntriesSafe(string path)
	{
		try
		{
			return _fileSystem.EnumerateFileSystemEntries(path).ToArray();
		}
		catch (Exception exception)
		{
			_logger.Warning(
				ENUMERATION_FAILED_EVENT,
				"Filesystem entry enumeration failed.",
				BuildContext(
					("path", path),
					("exception", exception.GetType().Name)));
			return [];
		}
	}

	/// <summary>
	/// Returns current UTC Unix timestamp in seconds.
	/// </summary>
	/// <returns>Current UTC Unix timestamp in seconds.</returns>
	private static long GetCurrentUnixSeconds()
	{
		return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}

	/// <summary>
	/// Parses one chapter path relative to the configured source root.
	/// </summary>
	/// <param name="chapterPath">Full chapter path.</param>
	/// <param name="sourceName">Resolved source name when parse succeeds.</param>
	/// <returns><see langword="true"/> when path depth equals source/manga/chapter under the source root.</returns>
	private bool TryParseSourcePath(string chapterPath, out string sourceName)
	{
		sourceName = string.Empty;
		string relativePath = Path.GetRelativePath(_options.SourcesRootPath, chapterPath);
		if (string.IsNullOrWhiteSpace(relativePath) ||
			string.Equals(relativePath, ".", StringComparison.Ordinal) ||
			Path.IsPathRooted(relativePath))
		{
			return false;
		}

		string[] segments = relativePath.Split(
			[Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (segments.Length != 3)
		{
			return false;
		}

		if (string.Equals(segments[0], "..", StringComparison.Ordinal) ||
			string.Equals(segments[1], "..", StringComparison.Ordinal) ||
			string.Equals(segments[2], "..", StringComparison.Ordinal))
		{
			return false;
		}

		sourceName = segments[0];
		return true;
	}

	/// <summary>
	/// Builds one immutable logging context dictionary.
	/// </summary>
	/// <param name="pairs">Context key-value pairs.</param>
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
