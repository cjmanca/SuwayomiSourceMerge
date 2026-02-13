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
	/// <param name="linkPath">Fully-qualified absolute branch-link path.</param>
	/// <param name="targetPath">Fully-qualified absolute target path represented by the branch-link.</param>
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

		LinkName = linkName;
		LinkPath = PathSafetyPolicy.NormalizeFullyQualifiedPath(linkPath, nameof(linkPath));
		TargetPath = PathSafetyPolicy.NormalizeFullyQualifiedPath(targetPath, nameof(targetPath));
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
	/// Gets the fully-qualified absolute branch-link path.
	/// </summary>
	public string LinkPath
	{
		get;
	}

	/// <summary>
	/// Gets the fully-qualified absolute target path represented by the branch-link.
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
