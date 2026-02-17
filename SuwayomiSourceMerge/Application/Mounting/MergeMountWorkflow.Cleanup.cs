using SuwayomiSourceMerge.Infrastructure.Mounts;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Lifecycle cleanup behavior for <see cref="MergeMountWorkflow"/>.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
	/// <summary>
	/// Runs one lifecycle cleanup pass.
	/// </summary>
	/// <param name="phase">Lifecycle phase name.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private void RunCleanupPass(string phase, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(phase);
		cancellationToken.ThrowIfCancellationRequested();

		MountSnapshot mountSnapshot = _mountSnapshotService.Capture();
		for (int index = 0; index < mountSnapshot.Warnings.Count; index++)
		{
			_logger.Warning(
				MergeCleanupEvent,
				mountSnapshot.Warnings[index].Message,
				BuildContext(
					("phase", phase),
					("warning_code", mountSnapshot.Warnings[index].Code)));
		}

		string[] managedMergerfsMountPoints = mountSnapshot.Entries
			.Where(IsManagedMergerfsMount)
			.Select(static entry => entry.MountPoint)
			.Distinct(PathSafetyPolicy.GetPathComparer())
			.OrderByDescending(GetPathDepth)
			.ThenBy(static path => path, PathSafetyPolicy.GetPathComparer())
			.ToArray();

		for (int index = 0; index < managedMergerfsMountPoints.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			MountActionApplyResult unmountResult = _mountCommandService.UnmountMountPoint(
				managedMergerfsMountPoints[index],
				_options.UnmountCommandTimeout,
				_options.CommandPollInterval,
				_options.CleanupHighPriority,
				_options.CleanupPriorityIoniceClass,
				_options.CleanupPriorityNiceValue,
				cancellationToken);
			_logger.Debug(
				MergeCleanupEvent,
				"Lifecycle cleanup unmount attempt finished.",
				BuildContext(
					("phase", phase),
					("mountpoint", managedMergerfsMountPoints[index]),
					("outcome", unmountResult.Outcome.ToString()),
					("diagnostic", unmountResult.Diagnostic)));
		}

		MountSnapshot postUnmountSnapshot = _mountSnapshotService.Capture();
		for (int index = 0; index < postUnmountSnapshot.Warnings.Count; index++)
		{
			_logger.Warning(
				MergeCleanupEvent,
				postUnmountSnapshot.Warnings[index].Message,
				BuildContext(
					("phase", phase),
					("warning_code", postUnmountSnapshot.Warnings[index].Code),
					("snapshot", "post_unmount")));
		}

		bool hasDegradedVisibilityPreOrPostWarning = mountSnapshot.Warnings
			.Any(IsPostUnmountSnapshotWarningDegradedVisibility) ||
			postUnmountSnapshot.Warnings
			.Any(IsPostUnmountSnapshotWarningDegradedVisibility);

		MountSnapshotEntry[] stillMountedManagedEntries = postUnmountSnapshot.Entries
			.Where(IsManagedMergerfsMount)
			.ToArray();
		HashSet<string> stillMountedManagedMountPoints = stillMountedManagedEntries
			.Select(static entry => Path.GetFullPath(entry.MountPoint))
			.ToHashSet(PathSafetyPolicy.GetPathComparer());

		HashSet<string> activeBranchDirectories;
		Dictionary<string, string> branchDirectoryByMountPoint;
		lock (_syncRoot)
		{
			activeBranchDirectories = new HashSet<string>(_lastDesiredBranchDirectories, PathSafetyPolicy.GetPathComparer());
			branchDirectoryByMountPoint = new Dictionary<string, string>(_lastBranchDirectoryByMountPoint, PathSafetyPolicy.GetPathComparer());
		}

		int inferredUnmappedStillMountedBranchCount = 0;
		int unresolvedUnmappedStillMountedBranchCount = 0;
		for (int index = 0; index < stillMountedManagedEntries.Length; index++)
		{
			string mountedMountPoint = Path.GetFullPath(stillMountedManagedEntries[index].MountPoint);
			if (!branchDirectoryByMountPoint.TryGetValue(mountedMountPoint, out string? branchDirectory))
			{
				if (TryResolveBranchDirectoryFromMountSource(stillMountedManagedEntries[index].Source, out string inferredBranchDirectoryPath))
				{
					activeBranchDirectories.Add(inferredBranchDirectoryPath);
					inferredUnmappedStillMountedBranchCount++;
					continue;
				}

				unresolvedUnmappedStillMountedBranchCount++;
				continue;
			}

			activeBranchDirectories.Add(branchDirectory);
		}

		if (hasDegradedVisibilityPreOrPostWarning)
		{
			_logger.Warning(
				MergeCleanupEvent,
				"Lifecycle cleanup skipped stale branch-directory pruning because mount snapshot reliability was degraded by pre/post warning severity.",
				BuildContext(
					("phase", phase),
					("pre_unmount_warning_count", mountSnapshot.Warnings.Count.ToString()),
					("post_unmount_warning_count", postUnmountSnapshot.Warnings.Count.ToString())));
		}
		else if (unresolvedUnmappedStillMountedBranchCount > 0)
		{
			_logger.Warning(
				MergeCleanupEvent,
				"Lifecycle cleanup skipped stale branch-directory pruning because still-mounted targets lacked resolvable branch-directory mappings.",
				BuildContext(
					("phase", phase),
					("still_mounted_managed_mounts", stillMountedManagedMountPoints.Count.ToString()),
					("inferred_unmapped_branch_count", inferredUnmappedStillMountedBranchCount.ToString()),
					("unresolved_unmapped_branch_count", unresolvedUnmappedStillMountedBranchCount.ToString())));
		}
		else
		{
			_branchLinkStagingService.CleanupStaleBranchDirectories(_options.BranchLinksRootPath, activeBranchDirectories);
		}

		_logger.Debug(
			MergeCleanupEvent,
			"Lifecycle cleanup pass completed.",
			BuildContext(
				("phase", phase),
				("managed_mounts", managedMergerfsMountPoints.Length.ToString()),
				("still_mounted_managed_mounts", stillMountedManagedMountPoints.Count.ToString()),
				("inferred_unmapped_branch_count", inferredUnmappedStillMountedBranchCount.ToString()),
				("unresolved_unmapped_branch_count", unresolvedUnmappedStillMountedBranchCount.ToString()),
				("active_branch_dirs", activeBranchDirectories.Count.ToString())));
	}

	/// <summary>
	/// Tries to infer one active branch-directory path from one mergerfs source specification.
	/// </summary>
	/// <param name="mountSource">Mount source payload from a mount snapshot entry.</param>
	/// <param name="branchDirectoryPath">Resolved branch-directory path when inference succeeds.</param>
	/// <returns><see langword="true"/> when exactly one branch directory under the managed branch-links root can be inferred.</returns>
	private bool TryResolveBranchDirectoryFromMountSource(string mountSource, out string branchDirectoryPath)
	{
		ArgumentNullException.ThrowIfNull(mountSource);

		branchDirectoryPath = string.Empty;
		string normalizedBranchLinksRootPath = Path.GetFullPath(_options.BranchLinksRootPath);
		HashSet<string> inferredBranchDirectoryPaths = new(PathSafetyPolicy.GetPathComparer());

		string[] branchSpecifications = mountSource.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		for (int index = 0; index < branchSpecifications.Length; index++)
		{
			string branchSpecification = branchSpecifications[index];
			int accessModeDelimiterIndex = branchSpecification.IndexOf('=');
			string branchLinkPath = accessModeDelimiterIndex >= 0
				? branchSpecification[..accessModeDelimiterIndex]
				: branchSpecification;

			if (!Path.IsPathRooted(branchLinkPath))
			{
				continue;
			}

			string normalizedBranchLinkPath;
			try
			{
				normalizedBranchLinkPath = Path.GetFullPath(branchLinkPath);
			}
			catch (Exception)
			{
				continue;
			}

			string? branchDirectoryCandidate = Path.GetDirectoryName(normalizedBranchLinkPath);
			if (string.IsNullOrWhiteSpace(branchDirectoryCandidate))
			{
				continue;
			}

			string normalizedBranchDirectoryCandidate = Path.GetFullPath(branchDirectoryCandidate);
			if (!IsStrictChildPath(normalizedBranchDirectoryCandidate, normalizedBranchLinksRootPath))
			{
				continue;
			}

			inferredBranchDirectoryPaths.Add(normalizedBranchDirectoryCandidate);
		}

		if (inferredBranchDirectoryPaths.Count != 1)
		{
			return false;
		}

		branchDirectoryPath = inferredBranchDirectoryPaths.First();
		return true;
	}

	/// <summary>
	/// Returns whether one candidate path is a strict child of one root path.
	/// </summary>
	/// <param name="candidatePath">Candidate full path.</param>
	/// <param name="rootPath">Root full path.</param>
	/// <returns><see langword="true"/> when <paramref name="candidatePath"/> is inside <paramref name="rootPath"/> but not equal to it.</returns>
	private static bool IsStrictChildPath(string candidatePath, string rootPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

		if (string.Equals(candidatePath, rootPath, _pathComparison))
		{
			return false;
		}

		string strictChildPrefix = rootPath + Path.DirectorySeparatorChar;
		return candidatePath.StartsWith(strictChildPrefix, _pathComparison);
	}

	/// <summary>
	/// Returns whether one post-unmount snapshot warning indicates degraded mount visibility.
	/// </summary>
	/// <param name="warning">Warning entry.</param>
	/// <returns><see langword="true"/> when stale-branch pruning should be skipped.</returns>
	private static bool IsPostUnmountSnapshotWarningDegradedVisibility(MountSnapshotWarning warning)
	{
		ArgumentNullException.ThrowIfNull(warning);

		return warning.Severity == MountSnapshotWarningSeverity.DegradedVisibility;
	}

	/// <summary>
	/// Returns whether one mount snapshot entry is a mergerfs mount under the managed merged root.
	/// </summary>
	/// <param name="entry">Snapshot entry.</param>
	/// <returns><see langword="true"/> when entry is managed mergerfs mount.</returns>
	private bool IsManagedMergerfsMount(MountSnapshotEntry entry)
	{
		if (!entry.FileSystemType.Contains("mergerfs", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		string normalizedMountPoint = Path.GetFullPath(entry.MountPoint);
		string normalizedRoot = Path.GetFullPath(_options.MergedRootPath);
		if (string.Equals(normalizedMountPoint, normalizedRoot, _pathComparison))
		{
			return true;
		}

		string prefix = normalizedRoot + Path.DirectorySeparatorChar;
		return normalizedMountPoint.StartsWith(prefix, _pathComparison);
	}
}
