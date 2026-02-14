namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Summarizes one chapter rename queue processing pass.
/// </summary>
internal sealed class ChapterRenameProcessResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ChapterRenameProcessResult"/> class.
	/// </summary>
	/// <param name="processedEntries">Number of processed queue entries.</param>
	/// <param name="renamedEntries">Number of entries renamed successfully.</param>
	/// <param name="unchangedEntries">Number of entries that required no rename.</param>
	/// <param name="deferredMissingEntries">Number of missing-path entries retained in queue.</param>
	/// <param name="droppedMissingEntries">Number of missing-path entries dropped after grace window expiration.</param>
	/// <param name="deferredNotReadyEntries">Number of entries deferred because delay window is not elapsed.</param>
	/// <param name="deferredNotQuietEntries">Number of entries deferred because quiet-window requirements were not met.</param>
	/// <param name="collisionSkippedEntries">Number of entries skipped after collision suffixes were exhausted.</param>
	/// <param name="moveFailedEntries">Number of entries whose rename move failed.</param>
	/// <param name="remainingQueuedEntries">Number of entries remaining in queue after processing.</param>
	public ChapterRenameProcessResult(
		int processedEntries,
		int renamedEntries,
		int unchangedEntries,
		int deferredMissingEntries,
		int droppedMissingEntries,
		int deferredNotReadyEntries,
		int deferredNotQuietEntries,
		int collisionSkippedEntries,
		int moveFailedEntries,
		int remainingQueuedEntries)
	{
		ProcessedEntries = processedEntries;
		RenamedEntries = renamedEntries;
		UnchangedEntries = unchangedEntries;
		DeferredMissingEntries = deferredMissingEntries;
		DroppedMissingEntries = droppedMissingEntries;
		DeferredNotReadyEntries = deferredNotReadyEntries;
		DeferredNotQuietEntries = deferredNotQuietEntries;
		CollisionSkippedEntries = collisionSkippedEntries;
		MoveFailedEntries = moveFailedEntries;
		RemainingQueuedEntries = remainingQueuedEntries;
	}

	/// <summary>
	/// Gets the number of processed queue entries.
	/// </summary>
	public int ProcessedEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of entries renamed successfully.
	/// </summary>
	public int RenamedEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of entries that required no rename.
	/// </summary>
	public int UnchangedEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of missing-path entries retained in queue.
	/// </summary>
	public int DeferredMissingEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of missing-path entries dropped after grace expiration.
	/// </summary>
	public int DroppedMissingEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of entries deferred because delay windows have not elapsed.
	/// </summary>
	public int DeferredNotReadyEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of entries deferred because quiet windows have not elapsed.
	/// </summary>
	public int DeferredNotQuietEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of entries skipped after collision suffix options were exhausted.
	/// </summary>
	public int CollisionSkippedEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of entries whose rename move failed.
	/// </summary>
	public int MoveFailedEntries
	{
		get;
	}

	/// <summary>
	/// Gets the number of entries remaining in queue after processing.
	/// </summary>
	public int RemainingQueuedEntries
	{
		get;
	}
}

