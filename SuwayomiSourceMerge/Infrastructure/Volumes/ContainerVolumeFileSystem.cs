namespace SuwayomiSourceMerge.Infrastructure.Volumes;

/// <summary>
/// Default filesystem adapter used by volume discovery in production.
/// </summary>
internal sealed class ContainerVolumeFileSystem : IContainerVolumeFileSystem
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

		return Directory.EnumerateDirectories(path, "*", SearchOption.TopDirectoryOnly);
	}

	/// <inheritdoc />
	public string GetFullPath(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		return Path.GetFullPath(path);
	}
}
