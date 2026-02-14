namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Default filesystem adapter used by chapter rename processing.
/// </summary>
internal sealed class ChapterRenameFileSystem : IChapterRenameFileSystem
{
	/// <inheritdoc />
	public bool DirectoryExists(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return Directory.Exists(path);
	}

	/// <inheritdoc />
	public IEnumerable<string> EnumerateDirectories(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (!Directory.Exists(path))
		{
			return [];
		}

		try
		{
			return Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly);
		}
		catch (DirectoryNotFoundException)
		{
			return [];
		}
	}

	/// <inheritdoc />
	public IEnumerable<string> EnumerateFileSystemEntries(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		if (!Directory.Exists(path))
		{
			return [];
		}

		try
		{
			return Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories);
		}
		catch (DirectoryNotFoundException)
		{
			return [];
		}
	}

	/// <inheritdoc />
	public string GetFullPath(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return Path.GetFullPath(path);
	}

	/// <inheritdoc />
	public bool PathExists(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);
		return File.Exists(path) || Directory.Exists(path);
	}

	/// <inheritdoc />
	public bool TryGetLastWriteTimeUtc(string path, out DateTimeOffset lastWriteTimeUtc)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		try
		{
			if (File.Exists(path))
			{
				lastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
				return lastWriteTimeUtc > DateTimeOffset.UnixEpoch;
			}

			if (Directory.Exists(path))
			{
				lastWriteTimeUtc = Directory.GetLastWriteTimeUtc(path);
				return lastWriteTimeUtc > DateTimeOffset.UnixEpoch;
			}
		}
		catch
		{
			// Best-effort timestamp retrieval.
		}

		lastWriteTimeUtc = default;
		return false;
	}

	/// <inheritdoc />
	public bool TryMoveDirectory(string sourcePath, string destinationPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

		try
		{
			Directory.Move(sourcePath, destinationPath);
			return true;
		}
		catch
		{
			return false;
		}
	}
}
