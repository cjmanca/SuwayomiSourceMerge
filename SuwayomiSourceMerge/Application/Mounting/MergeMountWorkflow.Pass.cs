using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Volumes;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Merge-pass orchestration behavior for <see cref="MergeMountWorkflow"/>.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
	/// <inheritdoc />
	public MergeScanDispatchOutcome RunMergePass(
		string reason,
		bool force,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);
		cancellationToken.ThrowIfCancellationRequested();

		_logger.Normal(
			MergePassStartedEvent,
			"Merge workflow pass started.",
			BuildContext(
				("reason", reason),
				("force", force ? "true" : "false")));

		ContainerVolumeDiscoveryResult discoveryResult = _volumeDiscoveryService.Discover(
			_options.SourcesRootPath,
			_options.OverrideRootPath);
		LogVolumeDiscoveryWarnings(discoveryResult);
		bool hasSourceDiscoveryWarning = HasSourceDiscoveryWarnings(discoveryResult);
		if (discoveryResult.SourceVolumePaths.Count == 0)
		{
			_logger.Warning(
				MergePassWarningEvent,
				$"No source volumes were discovered under '{_options.SourcesRootPath}'.",
				BuildContext(
					("reason", reason),
					("override_volumes", discoveryResult.OverrideVolumePaths.Count.ToString()),
					("source_discovery_warnings", discoveryResult.Warnings.Count.ToString())));
		}

		if (discoveryResult.OverrideVolumePaths.Count == 0)
		{
			_logger.Error(
				MergePassErrorEvent,
				"Merge workflow requires at least one override volume.");
			return MergeScanDispatchOutcome.Failure;
		}

		(IReadOnlyList<OverrideTitleCatalogEntry> existingOverrideTitleCatalog, bool hadOverrideEnumerationFailure) = DiscoverExistingOverrideTitleCatalog(
			discoveryResult.OverrideVolumePaths,
			cancellationToken);
		OverrideCanonicalResolver overrideCanonicalResolver = new(existingOverrideTitleCatalog, _sceneTagMatcher);
		LogOverrideCanonicalAdvisories(overrideCanonicalResolver.Advisories);
		(IReadOnlyList<MergeTitleGroup> groups, bool hadSourceEnumerationFailure) = BuildTitleGroups(
			discoveryResult.SourceVolumePaths,
			existingOverrideTitleCatalog,
			overrideCanonicalResolver,
			cancellationToken);

		List<DesiredMountDefinition> desiredMounts = [];
		HashSet<string> desiredBranchDirectories = new(PathSafetyPolicy.GetPathComparer());
		Dictionary<string, string> desiredBranchDirectoryByMountPoint = new(PathSafetyPolicy.GetPathComparer());
		bool buildFailure = hadOverrideEnumerationFailure || hadSourceEnumerationFailure;

		for (int index = 0; index < groups.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			MergeTitleGroup group = groups[index];
			if (PathSafetyPolicy.ContainsDirectorySeparator(group.CanonicalTitle))
			{
				buildFailure = true;
				_logger.Warning(
					MergePassWarningEvent,
					$"Skipped canonical title containing directory separators: '{group.CanonicalTitle}'.");
				continue;
			}

			try
			{
				MergerfsBranchPlanningRequest planningRequest = new(
					group.GroupKey,
					group.CanonicalTitle,
					_options.BranchLinksRootPath,
					discoveryResult.OverrideVolumePaths,
					group.SourceBranches);
				MergerfsBranchPlan branchPlan = _branchPlanningService.Plan(planningRequest);
				_branchLinkStagingService.StageBranchLinks(branchPlan);
				desiredBranchDirectories.Add(branchPlan.BranchDirectoryPath);
				EnsureDetailsJson(branchPlan);

				string mountPoint = BuildMountPointPath(group.CanonicalTitle);
				desiredMounts.Add(new DesiredMountDefinition(
					mountPoint,
					branchPlan.DesiredIdentity,
					branchPlan.BranchSpecification));
				desiredBranchDirectoryByMountPoint[mountPoint] = branchPlan.BranchDirectoryPath;
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception exception)
			{
				buildFailure = true;
				_logger.Error(
					MergePassErrorEvent,
					$"Failed to build merge state for canonical title '{group.CanonicalTitle}'.",
					BuildContext(
						("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
						("message", exception.Message)));
			}
		}

		if (desiredMounts.Count == 0)
		{
			_logger.Warning(
				MergePassWarningEvent,
				"Merge workflow produced zero desired mounts for this pass.",
				BuildContext(
					("reason", reason),
					("groups", groups.Count.ToString()),
					("source_volumes", discoveryResult.SourceVolumePaths.Count.ToString()),
					("override_volumes", discoveryResult.OverrideVolumePaths.Count.ToString()),
					("override_titles", existingOverrideTitleCatalog.Count.ToString()),
					("build_failure", buildFailure ? "true" : "false")));
		}

		MountSnapshot mountSnapshot = _mountSnapshotService.Capture();
		for (int index = 0; index < mountSnapshot.Warnings.Count; index++)
		{
			MountSnapshotWarning warning = mountSnapshot.Warnings[index];
			_logger.Warning(
				MergePassWarningEvent,
				warning.Message,
				BuildContext(("warning_code", warning.Code)));
		}

		bool hasDegradedSnapshotWarning = mountSnapshot.Warnings
			.Any(static warning => warning.Severity == MountSnapshotWarningSeverity.DegradedVisibility);

		HashSet<string> forceRemountSet = ResolveForceRemountSet(
			force,
			reason,
			desiredMounts,
			overrideCanonicalResolver);

		MountReconciliationInput reconciliationInput = new(
			desiredMounts,
			mountSnapshot,
			[_options.MergedRootPath],
			_options.EnableMountHealthcheck,
			forceRemountSet);
		MountReconciliationPlan reconciliationPlan = _mountReconciliationService.Reconcile(reconciliationInput);
		bool suppressStaleUnmountActions = buildFailure || hasDegradedSnapshotWarning || hasSourceDiscoveryWarning;
		IReadOnlyList<MountReconciliationAction> actionsToApply = reconciliationPlan.Actions;
		if (suppressStaleUnmountActions)
		{
			MountReconciliationAction[] filteredActions = reconciliationPlan.Actions
				.Where(static action => action.Kind != MountReconciliationActionKind.Unmount || action.Reason != MountReconciliationReason.StaleMount)
				.ToArray();
			int suppressedActionCount = reconciliationPlan.Actions.Count - filteredActions.Length;
			if (suppressedActionCount > 0)
			{
				_logger.Warning(
					MergePassWarningEvent,
					"Suppressed stale-unmount actions because merge visibility was degraded for this pass.",
					BuildContext(
						("suppressed_actions", suppressedActionCount.ToString()),
						("build_failure", buildFailure ? "true" : "false"),
						("snapshot_degraded", hasDegradedSnapshotWarning ? "true" : "false"),
						("source_discovery_warning", hasSourceDiscoveryWarning ? "true" : "false")));
			}

			actionsToApply = filteredActions;
		}
		(MergeScanDispatchOutcome outcome, bool hadBusy, bool hadFailure) = ApplyPlanActions(
			actionsToApply,
			cancellationToken);

		lock (_syncRoot)
		{
			_lastDesiredBranchDirectories = desiredBranchDirectories;
			_lastBranchDirectoryByMountPoint = desiredBranchDirectoryByMountPoint;
		}

		if (buildFailure)
		{
			hadFailure = true;
			if (outcome == MergeScanDispatchOutcome.Success)
			{
				outcome = MergeScanDispatchOutcome.Failure;
			}
			else if (outcome == MergeScanDispatchOutcome.Busy)
			{
				outcome = MergeScanDispatchOutcome.Mixed;
			}
		}

		_logger.Normal(
			MergePassCompletedEvent,
			"Merge workflow pass completed.",
			BuildContext(
				("reason", reason),
				("force", force ? "true" : "false"),
				("groups", groups.Count.ToString()),
				("desired_mounts", desiredMounts.Count.ToString()),
				("actions", actionsToApply.Count.ToString()),
				("busy", hadBusy ? "true" : "false"),
				("failure", hadFailure ? "true" : "false"),
				("outcome", outcome.ToString())));

		return outcome;
	}

}
