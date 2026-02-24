namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Provides filesystem operations used by override cover setup and write flows.
/// </summary>
internal interface IOverrideCoverFileOperations
{
	/// <summary>
	/// Creates one directory path if it does not already exist.
	/// </summary>
	/// <param name="path">Directory path.</param>
	void CreateDirectory(string path);

	/// <summary>
	/// Writes bytes to one file path, replacing existing file contents.
	/// </summary>
	/// <param name="path">File path.</param>
	/// <param name="bytes">File bytes.</param>
	void WriteAllBytes(string path, byte[] bytes);

	/// <summary>
	/// Moves one file path to a destination path.
	/// </summary>
	/// <param name="sourcePath">Source file path.</param>
	/// <param name="destinationPath">Destination file path.</param>
	/// <param name="overwrite">Whether existing destination files can be replaced.</param>
	void MoveFile(string sourcePath, string destinationPath, bool overwrite);

	/// <summary>
	/// Returns whether one file path exists.
	/// </summary>
	/// <param name="path">File path.</param>
	/// <returns><see langword="true"/> when a file exists; otherwise <see langword="false"/>.</returns>
	bool FileExists(string path);

	/// <summary>
	/// Deletes one file path.
	/// </summary>
	/// <param name="path">File path.</param>
	void DeleteFile(string path);
}
