namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Defines chapter rename queue operations.
/// </summary>
internal interface IChapterRenameQueueProcessor
{
	/// <summary>
	/// Attempts to enqueue one chapter directory path.
	/// </summary>
	/// <param name="chapterPath">Chapter directory path.</param>
	/// <returns><see langword="true"/> when the path was queued; otherwise <see langword="false"/>.</returns>
	bool EnqueueChapterPath(string chapterPath);

	/// <summary>
	/// Enqueues chapter paths under one source directory.
	/// </summary>
	/// <param name="sourcePath">Source directory path.</param>
	/// <returns>Number of chapter paths newly enqueued.</returns>
	int EnqueueChaptersUnderSourcePath(string sourcePath);

	/// <summary>
	/// Enqueues chapter paths under one manga directory.
	/// </summary>
	/// <param name="mangaPath">Manga directory path.</param>
	/// <returns>Number of chapter paths newly enqueued.</returns>
	int EnqueueChaptersUnderMangaPath(string mangaPath);

	/// <summary>
	/// Processes the current queue once.
	/// </summary>
	/// <returns>Processing summary.</returns>
	ChapterRenameProcessResult ProcessOnce();

	/// <summary>
	/// Scans sources for rename candidates and enqueues any new candidates.
	/// </summary>
	/// <returns>Rescan summary.</returns>
	ChapterRenameRescanResult RescanAndEnqueue();
}
