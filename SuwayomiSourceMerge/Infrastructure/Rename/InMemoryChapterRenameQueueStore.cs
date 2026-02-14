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
	/// Queue entries keyed by full path. Dictionary insertion order is used for deterministic processing order.
	/// </summary>
	private readonly Dictionary<string, ChapterRenameQueueEntry> _entriesByPath = new(StringComparer.Ordinal);

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
			return true;
		}
	}

	/// <inheritdoc />
	public IReadOnlyList<ChapterRenameQueueEntry> ReadAll()
	{
		lock (_syncRoot)
		{
			return _entriesByPath.Values.ToArray();
		}
	}

	/// <inheritdoc />
	public void Transform(Func<IReadOnlyList<ChapterRenameQueueEntry>, IReadOnlyList<ChapterRenameQueueEntry>> transformer)
	{
		ArgumentNullException.ThrowIfNull(transformer);

		lock (_syncRoot)
		{
			IReadOnlyList<ChapterRenameQueueEntry> snapshot = _entriesByPath.Values.ToArray();
			IReadOnlyList<ChapterRenameQueueEntry>? replacementEntries = transformer(snapshot);
			ArgumentNullException.ThrowIfNull(replacementEntries);

			Dictionary<string, ChapterRenameQueueEntry> validatedEntriesByPath = new(StringComparer.Ordinal);
			for (int index = 0; index < replacementEntries.Count; index++)
			{
				ChapterRenameQueueEntry? entry = replacementEntries[index];
				ArgumentNullException.ThrowIfNull(entry);

				if (!validatedEntriesByPath.ContainsKey(entry.Path))
				{
					validatedEntriesByPath.Add(entry.Path, entry);
				}
			}

			_entriesByPath.Clear();
			foreach (KeyValuePair<string, ChapterRenameQueueEntry> pair in validatedEntriesByPath)
			{
				_entriesByPath.Add(pair.Key, pair.Value);
			}
		}
	}
}
