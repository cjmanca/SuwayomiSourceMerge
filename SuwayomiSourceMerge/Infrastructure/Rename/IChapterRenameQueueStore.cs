namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Defines queue storage behavior for chapter rename candidates.
/// </summary>
internal interface IChapterRenameQueueStore
{
	/// <summary>
	/// Gets the current queued entry count.
	/// </summary>
	int Count
	{
		get;
	}

	/// <summary>
	/// Attempts to enqueue one entry.
	/// </summary>
	/// <param name="entry">Entry to enqueue.</param>
	/// <returns><see langword="true"/> when the entry was added; <see langword="false"/> when an entry for the same path already exists.</returns>
	bool TryEnqueue(ChapterRenameQueueEntry entry);

	/// <summary>
	/// Reads all queued entries in deterministic insertion order.
	/// </summary>
	/// <returns>Snapshot of queued entries.</returns>
	IReadOnlyList<ChapterRenameQueueEntry> ReadAll();

	/// <summary>
	/// Atomically transforms the queued entries under one exclusive lock.
	/// </summary>
	/// <param name="transformer">
	/// Callback that receives the current queue snapshot and returns replacement entries.
	/// The callback executes while the queue lock is held.
	/// </param>
	void Transform(Func<IReadOnlyList<ChapterRenameQueueEntry>, IReadOnlyList<ChapterRenameQueueEntry>> transformer);
}
