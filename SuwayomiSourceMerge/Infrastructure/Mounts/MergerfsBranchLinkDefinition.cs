namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Describes one planned branch-link entry used to compose a mergerfs branch specification.
/// </summary>
internal sealed class MergerfsBranchLinkDefinition
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsBranchLinkDefinition"/> class.
	/// </summary>
	/// <param name="linkName">Filesystem-safe link name under the branch-link directory.</param>
	/// <param name="linkPath">Absolute branch-link path.</param>
	/// <param name="targetPath">Absolute target path represented by the branch-link.</param>
	/// <param name="accessMode">Access mode to emit in branch specifications.</param>
	/// <exception cref="ArgumentException">Thrown when required values are missing or invalid.</exception>
	public MergerfsBranchLinkDefinition(
		string linkName,
		string linkPath,
		string targetPath,
		MergerfsBranchAccessMode accessMode)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(linkName);
		ArgumentException.ThrowIfNullOrWhiteSpace(linkPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

		PathSafetyPolicy.ValidateLinkNameSegment(linkName, nameof(linkName));

		string trimmedLinkPath = linkPath.Trim();
		if (!Path.IsPathRooted(trimmedLinkPath))
		{
			throw new ArgumentException(
				"Link path must be an absolute path.",
				nameof(linkPath));
		}

		string trimmedTargetPath = targetPath.Trim();
		if (!Path.IsPathRooted(trimmedTargetPath))
		{
			throw new ArgumentException(
				"Target path must be an absolute path.",
				nameof(targetPath));
		}

		LinkName = linkName;
		LinkPath = Path.GetFullPath(trimmedLinkPath);
		TargetPath = Path.GetFullPath(trimmedTargetPath);
		AccessMode = accessMode;
	}

	/// <summary>
	/// Gets the filesystem-safe link name under the branch-link directory.
	/// </summary>
	public string LinkName
	{
		get;
	}

	/// <summary>
	/// Gets the absolute branch-link path.
	/// </summary>
	public string LinkPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute target path represented by the branch-link.
	/// </summary>
	public string TargetPath
	{
		get;
	}

	/// <summary>
	/// Gets the branch access mode.
	/// </summary>
	public MergerfsBranchAccessMode AccessMode
	{
		get;
	}
}
