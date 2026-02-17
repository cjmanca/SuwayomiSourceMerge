namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Represents one persistent inotify monitor session used by <see cref="PersistentInotifywaitEventReader"/>.
/// </summary>
internal interface IPersistentInotifyMonitorSession : IDisposable
{
	/// <summary>
	/// Gets the watch path associated with this monitor session.
	/// </summary>
	string WatchPath
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether this session was started in recursive mode.
	/// </summary>
	bool IsRecursive
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether the monitor process is currently running.
	/// </summary>
	bool IsRunning
	{
		get;
	}

	/// <summary>
	/// Attempts to dequeue one parsed inotify event record.
	/// </summary>
	/// <param name="record">Dequeued event record.</param>
	/// <returns><see langword="true"/> when an event was dequeued; otherwise <see langword="false"/>.</returns>
	bool TryDequeueEvent(out InotifyEventRecord record);

	/// <summary>
	/// Attempts to dequeue one warning emitted by the monitor process.
	/// </summary>
	/// <param name="warning">Dequeued warning text.</param>
	/// <returns><see langword="true"/> when a warning was dequeued; otherwise <see langword="false"/>.</returns>
	bool TryDequeueWarning(out string warning);
}
