using SuwayomiSourceMerge.Infrastructure.Watching;

namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Summarizes one <see cref="FilesystemEventTriggerPipeline.Tick"/> pass.
/// </summary>
internal sealed class FilesystemEventTickResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="FilesystemEventTickResult"/> class.
	/// </summary>
	/// <param name="pollOutcome">Inotify poll outcome.</param>
	/// <param name="polledEvents">Number of parsed inotify events.</param>
	/// <param name="pollWarnings">Number of poll warnings.</param>
	/// <param name="enqueuedChapterPaths">Number of chapter paths enqueued from event routing.</param>
	/// <param name="mergeRequestsQueued">Number of merge requests queued during this tick.</param>
	/// <param name="renameProcessRuns">Number of rename queue process passes run during this tick.</param>
	/// <param name="renameRescanRuns">Number of rename rescans run during this tick.</param>
	/// <param name="mergeDispatchOutcome">Merge dispatch outcome for this tick.</param>
	public FilesystemEventTickResult(
		InotifyPollOutcome pollOutcome,
		int polledEvents,
		int pollWarnings,
		int enqueuedChapterPaths,
		int mergeRequestsQueued,
		int renameProcessRuns,
		int renameRescanRuns,
		MergeScanDispatchOutcome mergeDispatchOutcome)
	{
		if (polledEvents < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(polledEvents), "Polled events must be >= 0.");
		}

		if (pollWarnings < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(pollWarnings), "Poll warnings must be >= 0.");
		}

		if (enqueuedChapterPaths < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(enqueuedChapterPaths), "Enqueued chapter paths must be >= 0.");
		}

		if (mergeRequestsQueued < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(mergeRequestsQueued), "Merge requests queued must be >= 0.");
		}

		if (renameProcessRuns < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(renameProcessRuns), "Rename process runs must be >= 0.");
		}

		if (renameRescanRuns < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(renameRescanRuns), "Rename rescan runs must be >= 0.");
		}

		PollOutcome = pollOutcome;
		PolledEvents = polledEvents;
		PollWarnings = pollWarnings;
		EnqueuedChapterPaths = enqueuedChapterPaths;
		MergeRequestsQueued = mergeRequestsQueued;
		RenameProcessRuns = renameProcessRuns;
		RenameRescanRuns = renameRescanRuns;
		MergeDispatchOutcome = mergeDispatchOutcome;
	}

	/// <summary>
	/// Gets inotify poll outcome.
	/// </summary>
	public InotifyPollOutcome PollOutcome
	{
		get;
	}

	/// <summary>
	/// Gets number of parsed inotify events.
	/// </summary>
	public int PolledEvents
	{
		get;
	}

	/// <summary>
	/// Gets number of inotify poll warnings.
	/// </summary>
	public int PollWarnings
	{
		get;
	}

	/// <summary>
	/// Gets number of chapter paths enqueued from event routing.
	/// </summary>
	public int EnqueuedChapterPaths
	{
		get;
	}

	/// <summary>
	/// Gets number of merge requests queued during this tick.
	/// </summary>
	public int MergeRequestsQueued
	{
		get;
	}

	/// <summary>
	/// Gets number of rename queue process passes run during this tick.
	/// </summary>
	public int RenameProcessRuns
	{
		get;
	}

	/// <summary>
	/// Gets number of rename rescans run during this tick.
	/// </summary>
	public int RenameRescanRuns
	{
		get;
	}

	/// <summary>
	/// Gets merge dispatch outcome for this tick.
	/// </summary>
	public MergeScanDispatchOutcome MergeDispatchOutcome
	{
		get;
	}
}
