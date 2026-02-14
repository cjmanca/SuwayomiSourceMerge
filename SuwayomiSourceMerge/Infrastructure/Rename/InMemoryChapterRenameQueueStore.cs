namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// In-memory queue store with path-based de-duplication.
/// </summary>
internal sealed class InMemoryChapterRenameQueueStore : IChapterRenameQueueStore
{
	/// <summary>
	/// Synchronization lock for queue mutations and reads.
	/// </summary>
	private readonly Lock _syncRoot = new();

	/// <summary>
	/// Queue entries keyed by full path.
	/// </summary>
	private readonly Dictionary<string, ChapterRenameQueueEntry> _entriesByPath = new(StringComparer.Ordinal);

	/// <summary>
	/// Explicit queue ordering for deterministic processing independent of dictionary iteration behavior.
	/// </summary>
	private readonly List<string> _orderedPaths = [];

	/// <inheritdoc />
	public int Count
	{
		get
		{
			lock (_syncRoot)
			{
				return _entriesByPath.Count;
			}
		}
	}

	/// <inheritdoc />
	public bool TryEnqueue(ChapterRenameQueueEntry entry)
	{
		ArgumentNullException.ThrowIfNull(entry);

		lock (_syncRoot)
		{
			if (_entriesByPath.ContainsKey(entry.Path))
			{
				return false;
			}

			_entriesByPath.Add(entry.Path, entry);
			_orderedPaths.Add(entry.Path);
			return true;
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<ChapterRenameQueueEntry> ReadAll()
	{
		lock (_syncRoot)
		{
			return SnapshotOrderedEntriesUnsafe();
		}
	}

	/// <inheritdoc />
	public void Transform(Func<IReadOnlyList<ChapterRenameQueueEntry>, IReadOnlyList<ChapterRenameQueueEntry>> transformer)
	{
		ArgumentNullException.ThrowIfNull(transformer);

		lock (_syncRoot)
		{
			IReadOnlyList<ChapterRenameQueueEntry> snapshot = SnapshotOrderedEntriesUnsafe();
			IReadOnlyList<ChapterRenameQueueEntry>? replacementEntries = transformer(snapshot);
			ArgumentNullException.ThrowIfNull(replacementEntries);

			Dictionary<string, ChapterRenameQueueEntry> validatedEntriesByPath = new(StringComparer.Ordinal);
			List<string> validatedOrderedPaths = new(replacementEntries.Count);
			for (int index = 0; index < replacementEntries.Count; index++)
			{
				ChapterRenameQueueEntry? entry = replacementEntries[index];
				ArgumentNullException.ThrowIfNull(entry);

				if (!validatedEntriesByPath.ContainsKey(entry.Path))
				{
					validatedEntriesByPath.Add(entry.Path, entry);
					validatedOrderedPaths.Add(entry.Path);
				}
			}

			_entriesByPath.Clear();
			_orderedPaths.Clear();
			for (int index = 0; index < validatedOrderedPaths.Count; index++)
			{
				string path = validatedOrderedPaths[index];
				_entriesByPath.Add(path, validatedEntriesByPath[path]);
				_orderedPaths.Add(path);
			}
		}
	}

	/// <summary>
	/// Builds one ordered snapshot of queued entries. Caller must hold <see cref="_syncRoot"/>.
	/// </summary>
	/// <returns>Queued entries ordered by enqueue/transform insertion order.</returns>
	private ChapterRenameQueueEntry[] SnapshotOrderedEntriesUnsafe()
	{
		ChapterRenameQueueEntry[] snapshot = new ChapterRenameQueueEntry[_orderedPaths.Count];
		for (int index = 0; index < _orderedPaths.Count; index++)
		{
			snapshot[index] = _entriesByPath[_orderedPaths[index]];
		}

		return snapshot;
	}
}
