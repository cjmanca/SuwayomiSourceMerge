namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Polls filesystem watch roots and returns batched inotify events.
/// </summary>
internal interface IInotifyEventReader
{
	/// <summary>
	/// Polls watch roots once and returns parsed inotify event records.
	/// </summary>
	/// <param name="watchRoots">Directories passed to inotifywait.</param>
	/// <param name="timeout">Maximum wait duration for one poll.</param>
	/// <param name="cancellationToken">Token used to cancel polling.</param>
	/// <returns>Parsed poll result with outcome and warnings.</returns>
	InotifyPollResult Poll(
		IReadOnlyList<string> watchRoots,
		TimeSpan timeout,
		CancellationToken cancellationToken = default);
}
