namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Provides filesystem operations used by chapter rename processing.
/// </summary>
internal interface IChapterRenameFileSystem
{
	/// <summary>
	/// Returns the full normalized path.
	/// </summary>
	/// <param name="path">Input path.</param>
	/// <returns>Full normalized path.</returns>
	string GetFullPath(string path);

	/// <summary>
	/// Determines whether one directory exists.
	/// </summary>
	/// <param name="path">Directory path to evaluate.</param>
	/// <returns><see langword="true"/> when the directory exists; otherwise <see langword="false"/>.</returns>
	bool DirectoryExists(string path);

	/// <summary>
	/// Determines whether one filesystem item exists.
	/// </summary>
	/// <param name="path">Filesystem path to evaluate.</param>
	/// <returns><see langword="true"/> when a file or directory exists; otherwise <see langword="false"/>.</returns>
	bool PathExists(string path);

	/// <summary>
	/// Enumerates direct child directories under one root.
	/// </summary>
	/// <param name="path">Root directory path.</param>
	/// <returns>Direct child directory paths.</returns>
	IEnumerable<string> EnumerateDirectories(string path);

	/// <summary>
	/// Enumerates all nested filesystem entries under one root path.
	/// </summary>
	/// <param name="path">Root directory path.</param>
	/// <returns>Nested filesystem entry paths.</returns>
	IEnumerable<string> EnumerateFileSystemEntries(string path);

	/// <summary>
	/// Attempts to read one filesystem item's last-write timestamp.
	/// </summary>
	/// <param name="path">Filesystem path to evaluate.</param>
	/// <param name="lastWriteTimeUtc">Resolved last-write timestamp when available.</param>
	/// <returns><see langword="true"/> when a timestamp is available; otherwise <see langword="false"/>.</returns>
	bool TryGetLastWriteTimeUtc(string path, out DateTimeOffset lastWriteTimeUtc);

	/// <summary>
	/// Attempts to move one directory to a destination path.
	/// </summary>
	/// <param name="sourcePath">Source directory path.</param>
	/// <param name="destinationPath">Destination directory path.</param>
	/// <returns><see langword="true"/> when move succeeded; otherwise <see langword="false"/>.</returns>
	bool TryMoveDirectory(string sourcePath, string destinationPath);
}

