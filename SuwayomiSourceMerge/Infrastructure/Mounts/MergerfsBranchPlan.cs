namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents one deterministic mergerfs branch-link planning result.
/// </summary>
internal sealed class MergerfsBranchPlan
{

	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsBranchPlan"/> class.
	/// </summary>
	/// <param name="preferredOverridePath">Fully-qualified absolute preferred override directory path used for new writes.</param>
	/// <param name="branchDirectoryPath">Fully-qualified absolute branch-link directory path derived from branch-links root and group id.</param>
	/// <param name="branchSpecification">Deterministic mergerfs branch specification string.</param>
	/// <param name="desiredIdentity">Deterministic desired identity token for remount detection.</param>
	/// <param name="groupId">Deterministic group id derived from the group key.</param>
	/// <param name="branchLinks">Ordered branch-link definitions used to build branch specifications.</param>
	/// <exception cref="ArgumentException">Thrown when required values are missing or invalid.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="branchLinks"/> is <see langword="null"/>.</exception>
	public MergerfsBranchPlan(
		string preferredOverridePath,
		string branchDirectoryPath,
		string branchSpecification,
		string desiredIdentity,
		string groupId,
		IReadOnlyList<MergerfsBranchLinkDefinition> branchLinks)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredOverridePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(branchDirectoryPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(branchSpecification);
		ArgumentException.ThrowIfNullOrWhiteSpace(desiredIdentity);
		ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
		ArgumentNullException.ThrowIfNull(branchLinks);

		string normalizedPreferredOverridePath = PathSafetyPolicy.NormalizeFullyQualifiedPath(
			preferredOverridePath,
			nameof(preferredOverridePath));
		string normalizedBranchDirectoryPath = PathSafetyPolicy.NormalizeFullyQualifiedPath(
			branchDirectoryPath,
			nameof(branchDirectoryPath));

		if (branchLinks.Count == 0)
		{
			throw new ArgumentException(
				"Branch links must contain at least one entry.",
				nameof(branchLinks));
		}

		MergerfsBranchLinkDefinition[] branchLinkArray = new MergerfsBranchLinkDefinition[branchLinks.Count];
		for (int index = 0; index < branchLinks.Count; index++)
		{
			MergerfsBranchLinkDefinition? definition = branchLinks[index];
			if (definition is null)
			{
				throw new ArgumentException(
					$"Branch links must not contain null items. Null item at index {index}.",
					nameof(branchLinks));
			}

			branchLinkArray[index] = definition;
		}

		PreferredOverridePath = normalizedPreferredOverridePath;
		BranchDirectoryPath = normalizedBranchDirectoryPath;
		BranchSpecification = branchSpecification;
		DesiredIdentity = desiredIdentity;
		GroupId = groupId;
		BranchLinks = branchLinkArray;
	}

	/// <summary>
	/// Gets the preferred override directory path used for writes.
	/// </summary>
	public string PreferredOverridePath
	{
		get;
	}

	/// <summary>
	/// Gets the fully-qualified absolute branch-link directory path.
	/// </summary>
	public string BranchDirectoryPath
	{
		get;
	}

	/// <summary>
	/// Gets the deterministic mergerfs branch specification string.
	/// </summary>
	public string BranchSpecification
	{
		get;
	}

	/// <summary>
	/// Gets the deterministic desired identity token.
	/// </summary>
	public string DesiredIdentity
	{
		get;
	}

	/// <summary>
	/// Gets the deterministic group id derived from the group key.
	/// </summary>
	public string GroupId
	{
		get;
	}

	/// <summary>
	/// Gets ordered branch-link definitions used to prepare branch links and compose branch specifications.
	/// </summary>
	public IReadOnlyList<MergerfsBranchLinkDefinition> BranchLinks
	{
		get;
	}
}
