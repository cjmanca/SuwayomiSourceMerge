namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Prepares branch-link directories and symbolic links used by mergerfs branch specifications.
/// </summary>
internal interface IBranchLinkStagingService
{
	/// <summary>
	/// Stages branch links for one planned title group.
	/// </summary>
	/// <param name="plan">Branch planning result containing branch directory and link definitions.</param>
	void StageBranchLinks(MergerfsBranchPlan plan);

	/// <summary>
	/// Removes stale branch-link directories under the provided root.
	/// </summary>
	/// <param name="branchLinksRootPath">Branch-link root path.</param>
	/// <param name="activeBranchDirectoryPaths">Branch directories that should be preserved.</param>
	void CleanupStaleBranchDirectories(string branchLinksRootPath, IReadOnlySet<string> activeBranchDirectoryPaths);
}
