namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Represents one queued chapter path and the earliest timestamp when rename processing can run.
/// </summary>
internal sealed class ChapterRenameQueueEntry
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ChapterRenameQueueEntry"/> class.
	/// </summary>
	/// <param name="allowAtUnixSeconds">Earliest Unix timestamp (seconds) when this entry may be processed.</param>
	/// <param name="path">Chapter directory path.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null, empty, or whitespace.</exception>
	public ChapterRenameQueueEntry(long allowAtUnixSeconds, string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		AllowAtUnixSeconds = allowAtUnixSeconds;
		Path = System.IO.Path.GetFullPath(path);
	}

	/// <summary>
	/// Gets the earliest Unix timestamp (seconds) when this entry may be processed.
	/// </summary>
	public long AllowAtUnixSeconds
	{
		get;
	}

	/// <summary>
	/// Gets the normalized full chapter directory path.
	/// </summary>
	public string Path
	{
		get;
	}
}

