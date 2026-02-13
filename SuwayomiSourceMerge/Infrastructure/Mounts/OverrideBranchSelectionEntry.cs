namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Describes one selected override branch entry for a canonical title.
/// </summary>
internal sealed class OverrideBranchSelectionEntry
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideBranchSelectionEntry"/> class.
	/// </summary>
	/// <param name="volumeRootPath">Absolute override volume root path for this entry.</param>
	/// <param name="titlePath">Absolute per-title path under the volume root.</param>
	/// <param name="isPreferred">Whether this entry is the preferred write branch.</param>
	/// <exception cref="ArgumentException">Thrown when required values are missing or invalid.</exception>
	public OverrideBranchSelectionEntry(string volumeRootPath, string titlePath, bool isPreferred)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(volumeRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(titlePath);

		if (!Path.IsPathRooted(volumeRootPath))
		{
			throw new ArgumentException(
				"Volume root path must be an absolute path.",
				nameof(volumeRootPath));
		}

		if (!Path.IsPathRooted(titlePath))
		{
			throw new ArgumentException(
				"Title path must be an absolute path.",
				nameof(titlePath));
		}

		VolumeRootPath = Path.GetFullPath(volumeRootPath.Trim());
		TitlePath = Path.GetFullPath(titlePath.Trim());
		IsPreferred = isPreferred;
	}

	/// <summary>
	/// Gets the absolute override volume root path.
	/// </summary>
	public string VolumeRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute per-title override path.
	/// </summary>
	public string TitlePath
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether this entry is the preferred write branch.
	/// </summary>
	public bool IsPreferred
	{
		get;
	}
}
