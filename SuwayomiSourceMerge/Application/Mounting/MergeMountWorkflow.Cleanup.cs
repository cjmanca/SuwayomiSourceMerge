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

		HashSet<string> observedManagedMountPoints = new(PathSafetyPolicy.GetPathComparer());
		int totalPreUnmountWarnings = 0;
		int totalPostUnmountWarnings = 0;
		bool hasDegradedVisibilityPreOrPostWarning = false;
		MountSnapshotEntry[] stillMountedManagedEntries = [];
		int cleanupIterationsExecuted = 0;

		for (int iteration = 1; iteration <= MaxCleanupIterations; iteration++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			cleanupIterationsExecuted = iteration;

			MountSnapshot mountSnapshot = _mountSnapshotService.Capture();
			totalPreUnmountWarnings += mountSnapshot.Warnings.Count;
			hasDegradedVisibilityPreOrPostWarning =
				hasDegradedVisibilityPreOrPostWarning ||
				mountSnapshot.Warnings.Any(IsPostUnmountSnapshotWarningDegradedVisibility);
			for (int warningIndex = 0; warningIndex < mountSnapshot.Warnings.Count; warningIndex++)
			{
				_logger.Warning(
					MergeCleanupEvent,
					mountSnapshot.Warnings[warningIndex].Message,
					BuildContext(
						("phase", phase),
						("iteration", iteration.ToString()),
						("warning_code", mountSnapshot.Warnings[warningIndex].Code)));
			}

			string[] managedMergerfsMountPoints = mountSnapshot.Entries
				.Where(IsManagedMergerfsMount)
				.Select(static entry => entry.MountPoint)
				.Distinct(PathSafetyPolicy.GetPathComparer())
				.OrderByDescending(GetPathDepth)
				.ThenBy(static path => path, PathSafetyPolicy.GetPathComparer())
				.ToArray();
			for (int managedMountIndex = 0; managedMountIndex < managedMergerfsMountPoints.Length; managedMountIndex++)
			{
				observedManagedMountPoints.Add(Path.GetFullPath(managedMergerfsMountPoints[managedMountIndex]));
				cancellationToken.ThrowIfCancellationRequested();
				MountActionApplyResult unmountResult = _mountCommandService.UnmountMountPoint(
					managedMergerfsMountPoints[managedMountIndex],
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
						("iteration", iteration.ToString()),
						("mountpoint", managedMergerfsMountPoints[managedMountIndex]),
						("outcome", unmountResult.Outcome.ToString()),
						("failure_severity", unmountResult.FailureSeverity.ToString()),
						("diagnostic", unmountResult.Diagnostic)));
			}

			MountSnapshot postUnmountSnapshot = _mountSnapshotService.Capture();
			totalPostUnmountWarnings += postUnmountSnapshot.Warnings.Count;
			hasDegradedVisibilityPreOrPostWarning =
				hasDegradedVisibilityPreOrPostWarning ||
				postUnmountSnapshot.Warnings.Any(IsPostUnmountSnapshotWarningDegradedVisibility);
			for (int warningIndex = 0; warningIndex < postUnmountSnapshot.Warnings.Count; warningIndex++)
			{
				_logger.Warning(
					MergeCleanupEvent,
					postUnmountSnapshot.Warnings[warningIndex].Message,
					BuildContext(
						("phase", phase),
						("iteration", iteration.ToString()),
						("warning_code", postUnmountSnapshot.Warnings[warningIndex].Code),
						("snapshot", "post_unmount")));
			}

			stillMountedManagedEntries = postUnmountSnapshot.Entries
				.Where(IsManagedMergerfsMount)
				.ToArray();
			if (stillMountedManagedEntries.Length == 0)
			{
				break;
			}

			if (iteration == MaxCleanupIterations)
			{
				_logger.Warning(
					MergeCleanupEvent,
					"Lifecycle cleanup reached the maximum cleanup iteration limit with managed mounts still active.",
					BuildContext(
						("phase", phase),
						("max_cleanup_iterations", MaxCleanupIterations.ToString()),
						("still_mounted_managed_mounts", stillMountedManagedEntries.Length.ToString())));
			}
		}

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
					("pre_unmount_warning_count", totalPreUnmountWarnings.ToString()),
					("post_unmount_warning_count", totalPostUnmountWarnings.ToString()),
					("cleanup_iterations", cleanupIterationsExecuted.ToString())));
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

		int removedEmptyMergedDirectories = 0;
		int movedNonEmptyMergedDirectories = 0;
		bool skippedMergedDirectoryCleanup = false;
		if (hasDegradedVisibilityPreOrPostWarning)
		{
			skippedMergedDirectoryCleanup = true;
			_logger.Warning(
				MergeCleanupEvent,
				"Skipped merged-root directory cleanup because mount snapshot reliability was degraded by pre/post warning severity.",
				BuildContext(
					("phase", phase),
					("pre_unmount_warning_count", totalPreUnmountWarnings.ToString()),
					("post_unmount_warning_count", totalPostUnmountWarnings.ToString()),
					("cleanup_iterations", cleanupIterationsExecuted.ToString())));
		}
		else
		{
			(removedEmptyMergedDirectories, movedNonEmptyMergedDirectories, skippedMergedDirectoryCleanup) =
				CleanupMergedRootDirectories(phase, stillMountedManagedMountPoints, cancellationToken);
		}

		_logger.Normal(
			MergeCleanupEvent,
			"Lifecycle cleanup pass completed.",
			BuildContext(
				("phase", phase),
				("cleanup_iterations", cleanupIterationsExecuted.ToString()),
				("managed_mounts", observedManagedMountPoints.Count.ToString()),
				("still_mounted_managed_mounts", stillMountedManagedMountPoints.Count.ToString()),
				("inferred_unmapped_branch_count", inferredUnmappedStillMountedBranchCount.ToString()),
				("unresolved_unmapped_branch_count", unresolvedUnmappedStillMountedBranchCount.ToString()),
				("active_branch_dirs", activeBranchDirectories.Count.ToString()),
				("removed_empty_merged_dirs", removedEmptyMergedDirectories.ToString()),
				("moved_non_empty_merged_dirs", movedNonEmptyMergedDirectories.ToString()),
				("skipped_merged_dir_cleanup", skippedMergedDirectoryCleanup.ToString())));
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
			catch (Exception exception) when (!IsFatalException(exception))
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
