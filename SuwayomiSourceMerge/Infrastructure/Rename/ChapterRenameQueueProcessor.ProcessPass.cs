namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Partial queue-processor implementation containing one-pass processing helpers.
/// </summary>
internal sealed partial class ChapterRenameQueueProcessor
{
	/// <summary>
	/// Computes one queue-processing pass including remaining entries and summary metrics.
	/// </summary>
	/// <param name="entries">Queue entries to process.</param>
	/// <param name="nowUnixSeconds">Current timestamp in Unix seconds.</param>
	/// <returns>Computed pass result and remaining queue entries.</returns>
	private ProcessPassComputation ComputeProcessPass(IReadOnlyList<ChapterRenameQueueEntry> entries, long nowUnixSeconds)
	{
		int renamed = 0;
		int unchanged = 0;
		int deferredMissing = 0;
		int droppedMissing = 0;
		int deferredNotReady = 0;
		int deferredNotQuiet = 0;
		int collisionSkipped = 0;
		int moveFailed = 0;

		List<ChapterRenameQueueEntry> remainingEntries = new(entries.Count);
		for (int index = 0; index < entries.Count; index++)
		{
			ChapterRenameQueueEntry entry = entries[index];
			if (!_fileSystem.DirectoryExists(entry.Path))
			{
				if (nowUnixSeconds - entry.AllowAtUnixSeconds > _options.RenameRescanSeconds)
				{
					droppedMissing++;
					_logger.Debug(
						MissingPathDroppedEvent,
						"Dropped missing chapter path after grace window.",
						BuildContext(("path", entry.Path)));
				}
				else
				{
					deferredMissing++;
					remainingEntries.Add(entry);
				}

				continue;
			}

			if (nowUnixSeconds < entry.AllowAtUnixSeconds)
			{
				deferredNotReady++;
				remainingEntries.Add(entry);
				continue;
			}

			if (!IsQuietEnough(entry.Path, nowUnixSeconds))
			{
				deferredNotQuiet++;
				remainingEntries.Add(entry);
				continue;
			}

			string chapterName = Path.GetFileName(entry.Path);
			string sanitizedName = _sanitizer.Sanitize(chapterName);
			if (string.Equals(chapterName, sanitizedName, StringComparison.Ordinal))
			{
				unchanged++;
				continue;
			}

			string? parentPath = Path.GetDirectoryName(entry.Path);
			if (string.IsNullOrWhiteSpace(parentPath))
			{
				moveFailed++;
				_logger.Warning(
					MoveWarningEvent,
					"Could not determine parent path for queued entry.",
					BuildContext(("path", entry.Path)));
				continue;
			}

			string destinationName = ResolveDestinationName(parentPath, sanitizedName);
			if (string.IsNullOrEmpty(destinationName))
			{
				collisionSkipped++;
				_logger.Warning(
					CollisionExhaustedEvent,
					"Collision suffix options exhausted; rename skipped.",
					BuildContext(
						("path", entry.Path),
						("target", sanitizedName)));
				continue;
			}

			string destinationPath = Path.Combine(parentPath, destinationName);
			if (!_fileSystem.TryMoveDirectory(entry.Path, destinationPath))
			{
				moveFailed++;
				_logger.Warning(
					MoveWarningEvent,
					"Rename move failed; leaving entry as-is.",
					BuildContext(
						("source_path", entry.Path),
						("destination_path", destinationPath)));
				continue;
			}

			renamed++;
			_logger.Debug(
				RenamedEvent,
				"Renamed chapter directory.",
				BuildContext(
					("source_name", chapterName),
					("destination_name", destinationName)));
		}

		ChapterRenameProcessResult result = new(
			entries.Count,
			renamed,
			unchanged,
			deferredMissing,
			droppedMissing,
			deferredNotReady,
			deferredNotQuiet,
			collisionSkipped,
			moveFailed,
			remainingEntries.Count);

		return new ProcessPassComputation(result, remainingEntries);
	}

	/// <summary>
	/// Captures one snapshot of currently queued chapter paths.
	/// </summary>
	/// <returns>Set of queued paths for duplicate checks within one rescan pass.</returns>
	private HashSet<string> SnapshotQueuedPaths()
	{
		IReadOnlyList<ChapterRenameQueueEntry> snapshot = _queueStore.ReadAll();
		HashSet<string> queuedPaths = new(StringComparer.Ordinal);
		for (int index = 0; index < snapshot.Count; index++)
		{
			queuedPaths.Add(snapshot[index].Path);
		}

		return queuedPaths;
	}

	/// <summary>
	/// Holds one queue-processing pass result and remaining queue entries.
	/// </summary>
	private sealed class ProcessPassComputation
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessPassComputation"/> class.
		/// </summary>
		/// <param name="result">Pass summary result.</param>
		/// <param name="remainingEntries">Remaining queue entries after processing.</param>
		public ProcessPassComputation(ChapterRenameProcessResult result, IReadOnlyList<ChapterRenameQueueEntry> remainingEntries)
		{
			Result = result ?? throw new ArgumentNullException(nameof(result));
			RemainingEntries = remainingEntries ?? throw new ArgumentNullException(nameof(remainingEntries));
		}

		/// <summary>
		/// Gets summary metrics for one queue-processing pass.
		/// </summary>
		public ChapterRenameProcessResult Result
		{
			get;
		}

		/// <summary>
		/// Gets queue entries that remain after one processing pass.
		/// </summary>
		public IReadOnlyList<ChapterRenameQueueEntry> RemainingEntries
		{
			get;
		}
	}
}
