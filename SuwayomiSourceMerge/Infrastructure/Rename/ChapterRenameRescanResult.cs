namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Summarizes one chapter rename candidate rescan pass.
/// </summary>
internal sealed class ChapterRenameRescanResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ChapterRenameRescanResult"/> class.
	/// </summary>
	/// <param name="candidateEntries">Number of candidate chapter directories discovered by the rescan.</param>
	/// <param name="enqueuedEntries">Number of discovered candidates that were newly enqueued.</param>
	public ChapterRenameRescanResult(int candidateEntries, int enqueuedEntries)
	{
		CandidateEntries = candidateEntries;
		EnqueuedEntries = enqueuedEntries;
	}

	/// <summary>
	/// Gets number of candidate chapter directories discovered by the rescan.
	/// </summary>
	public int CandidateEntries
	{
		get;
	}

	/// <summary>
	/// Gets number of discovered candidates newly enqueued.
	/// </summary>
	public int EnqueuedEntries
	{
		get;
	}
}

