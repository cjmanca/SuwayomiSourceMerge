namespace SuwayomiSourceMerge.Infrastructure.Volumes;

/// <summary>
/// Provides filesystem operations used by container volume discovery.
/// </summary>
/// <remarks>
/// This abstraction allows discovery logic to be tested with deterministic fakes
/// while production code continues to use the real filesystem.
/// </remarks>
internal interface IContainerVolumeFileSystem
{
	/// <summary>
	/// Resolves a path to its absolute canonical form.
	/// </summary>
	/// <param name="path">Path to normalize.</param>
	/// <returns>The normalized absolute path.</returns>
	string GetFullPath(string path);

	/// <summary>
	/// Determines whether a directory exists at the specified path.
	/// </summary>
	/// <param name="path">Path to evaluate.</param>
	/// <returns><see langword="true"/> when the directory exists; otherwise <see langword="false"/>.</returns>
	bool DirectoryExists(string path);

	/// <summary>
	/// Enumerates direct-child directories for the specified root path.
	/// </summary>
	/// <param name="path">Directory root to enumerate.</param>
	/// <returns>A sequence of direct-child directory paths.</returns>
	IEnumerable<string> EnumerateDirectories(string path);
}
