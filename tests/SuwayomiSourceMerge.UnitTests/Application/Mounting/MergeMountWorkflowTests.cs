namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Volumes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MergeMountWorkflow"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Verifies source and override discovery produce desired mounts and successful dispatch.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldBuildDesiredMounts_AndReturnSuccess()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);
		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Single(fixture.ReconciliationService.LastInput!.DesiredMounts);
		Assert.Single(fixture.BranchStagingService.StagedPlans);
		Assert.Single(fixture.DetailsService.Requests);
		Assert.Same(
			fixture.Options.MetadataOrchestration,
			fixture.DetailsService.Requests[0].MetadataOrchestration);
	}

	/// <summary>
	/// Verifies override-force reasons force-remount only the targeted canonical mountpoint when resolvable.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldTargetSingleMountpoint_WhenOverrideForceReasonIsResolvable()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan([]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		MergeScanDispatchOutcome outcome = workflow.RunMergePass("override-force:Title One", force: true);
		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Single(fixture.ReconciliationService.LastInput!.ForceRemountMountPoints);
		Assert.Contains(
			Path.Combine(fixture.Options.MergedRootPath, "Canonical Title"),
			fixture.ReconciliationService.LastInput.ForceRemountMountPoints);
	}

	/// <summary>
	/// Verifies unresolved override-force reasons do not fallback to force-all remount behavior.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldNotForceAnyMountpoint_WhenOverrideForceReasonIsUnresolvable()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan([]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("override-force:Unknown Title", force: true);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Empty(fixture.ReconciliationService.LastInput!.ForceRemountMountPoints);
	}

	/// <summary>
	/// Verifies unresolved override-force requests are logged as warnings.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldLogWarning_WhenOverrideForceReasonIsUnresolvable()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan([]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("override-force:Unknown Title", force: true);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.warning" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning &&
				entry.Message.Contains("did not resolve", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies apply-path cleanup wrapper setting controls action apply command wrapping.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldUseApplyCleanupPrioritySetting_WhenApplyingActions()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory, cleanupApplyHighPriority: true);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.True(fixture.MountCommandService.LastApplyCleanupHighPriority);
	}

	/// <summary>
	/// Verifies degraded snapshot visibility suppresses stale-unmount apply actions while still applying mount actions.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldSuppressStaleUnmountActions_WhenSnapshotVisibilityIsDegraded()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountSnapshotService.EnqueueSnapshot(new MountSnapshot(
			[],
			[
				new MountSnapshotWarning(
					"MOUNT-SNAP-001",
					"degraded visibility",
					MountSnapshotWarningSeverity.DegradedVisibility)
			]));
		fixture.ReconciliationService.NextPlanFactory = input => new MountReconciliationPlan(
		[
			new MountReconciliationAction(
				MountReconciliationActionKind.Mount,
				input.DesiredMounts[0].MountPoint,
				input.DesiredMounts[0].DesiredIdentity,
				input.DesiredMounts[0].MountPayload,
				MountReconciliationReason.MissingMount),
			new MountReconciliationAction(
				MountReconciliationActionKind.Unmount,
				Path.Combine(fixture.Options.MergedRootPath, "Stale Title"),
				desiredIdentity: null,
				mountPayload: null,
				MountReconciliationReason.StaleMount)
		]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
		Assert.Equal(MountReconciliationActionKind.Mount, fixture.MountCommandService.AppliedActions[0].Kind);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.warning" &&
				entry.Message.Contains("Suppressed stale-unmount actions", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies build-failure degraded visibility suppresses stale-unmount apply actions.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldSuppressStaleUnmountActions_WhenBuildFailureIsDetected()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		Directory.CreateDirectory(Path.Combine(fixture.Options.SourcesRootPath, "disk1", "SourceA", "Broken Title"));
		fixture.BranchPlanningService.ThrowOnCanonicalTitles.Add("Broken Title");
		fixture.ReconciliationService.NextPlanFactory = input => new MountReconciliationPlan(
		[
			new MountReconciliationAction(
				MountReconciliationActionKind.Mount,
				input.DesiredMounts[0].MountPoint,
				input.DesiredMounts[0].DesiredIdentity,
				input.DesiredMounts[0].MountPayload,
				MountReconciliationReason.MissingMount),
			new MountReconciliationAction(
				MountReconciliationActionKind.Unmount,
				Path.Combine(fixture.Options.MergedRootPath, "Stale Title"),
				desiredIdentity: null,
				mountPayload: null,
				MountReconciliationReason.StaleMount)
		]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
		Assert.Equal(MountReconciliationActionKind.Mount, fixture.MountCommandService.AppliedActions[0].Kind);
	}

	/// <summary>
	/// Verifies healthy visibility still allows stale-unmount apply actions.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldApplyStaleUnmountActions_WhenVisibilityIsHealthy()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan(
		[
			new MountReconciliationAction(
				MountReconciliationActionKind.Unmount,
				Path.Combine(fixture.Options.MergedRootPath, "Stale Title"),
				desiredIdentity: null,
				mountPayload: null,
				MountReconciliationReason.StaleMount)
		]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
		Assert.Equal(MountReconciliationActionKind.Unmount, fixture.MountCommandService.AppliedActions[0].Kind);
		Assert.Equal(MountReconciliationReason.StaleMount, fixture.MountCommandService.AppliedActions[0].Reason);
	}

	/// <summary>
	/// Verifies source exclusion matching is trim and case-insensitive during merge grouping.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldExcludeSource_WhenExcludedSourceHasWhitespaceAndCaseDifferences()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(
			temporaryDirectory,
			excludedSources: ["  sourcea  "]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Empty(fixture.ReconciliationService.LastInput!.DesiredMounts);
		Assert.Empty(fixture.BranchStagingService.StagedPlans);
		Assert.Empty(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Verifies startup cleanup unmounts managed mergerfs mountpoints and skips stale branch cleanup when mappings are unavailable.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldRunStartupCleanup_WhenEnabled()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string mountedPath = Path.Combine(fixture.Options.MergedRootPath, "Canonical Title");
		fixture.MountSnapshotService.NextSnapshot = new MountSnapshot(
			[
				new MountSnapshotEntry(
					mountedPath,
					"fuse.mergerfs",
					"source",
					"rw",
					isHealthy: true)
			],
			[]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		workflow.OnWorkerStarting();
		Assert.Contains(mountedPath, fixture.MountCommandService.UnmountedMountPoints);
		Assert.Equal(0, fixture.BranchStagingService.CleanupCalls);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.cleanup" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning &&
				entry.Message.Contains("skipped stale branch-directory pruning", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies cleanup still prunes stale branch directories when one still-mounted target lacks a mapping but its branch path is inferable from source.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldRunStalePrune_WhenUnmappedStillMountedSourceInfersBranchDirectory()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		Assert.Equal(MergeScanDispatchOutcome.Success, workflow.RunMergePass("interval elapsed", force: false));

		string branchDirectoryPath = fixture.BranchStagingService.StagedPlans[0].BranchDirectoryPath;
		string inferredMountSource = string.Join(
			':',
			$"{Path.Combine(branchDirectoryPath, "00_override")}=RW",
			$"{Path.Combine(branchDirectoryPath, "10_source_00")}=RO");
		string unmappedStillMountedPath = Path.Combine(fixture.Options.MergedRootPath, "Unmapped Title");
		MountSnapshot stillMountedSnapshot = new(
			[
				new MountSnapshotEntry(
					unmappedStillMountedPath,
					"fuse.mergerfs",
					inferredMountSource,
					"rw",
					isHealthy: true)
			],
			[]);
		fixture.MountSnapshotService.EnqueueSnapshot(stillMountedSnapshot);
		fixture.MountSnapshotService.EnqueueSnapshot(stillMountedSnapshot);

		workflow.OnWorkerStarting();

		Assert.Equal(1, fixture.BranchStagingService.CleanupCalls);
		Assert.NotNull(fixture.BranchStagingService.LastCleanupActiveBranchDirectoryPaths);
		Assert.Contains(branchDirectoryPath, fixture.BranchStagingService.LastCleanupActiveBranchDirectoryPaths!);
		Assert.DoesNotContain(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.cleanup" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning &&
				entry.Message.Contains("lacked resolvable branch-directory mappings", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies cleanup preserves active mapped branch directories when unmount attempts fail and mountpoints remain active.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldPreserveActiveMappedBranchDirectories_WhenUnmountFailsAndMountRemains()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		Assert.Equal(MergeScanDispatchOutcome.Success, workflow.RunMergePass("interval elapsed", force: false));

		string mountedPath = Path.Combine(fixture.Options.MergedRootPath, "Canonical Title");
		string branchDirectoryPath = fixture.BranchStagingService.StagedPlans[0].BranchDirectoryPath;
		MountSnapshot stillMountedSnapshot = new(
			[
				new MountSnapshotEntry(
					mountedPath,
					"fuse.mergerfs",
					"source",
					"rw",
					isHealthy: true)
			],
			[]);
		fixture.MountSnapshotService.EnqueueSnapshot(stillMountedSnapshot);
		fixture.MountSnapshotService.EnqueueSnapshot(stillMountedSnapshot);
		fixture.MountCommandService.EnqueueUnmountOutcome(MountActionApplyOutcome.Failure);

		workflow.OnWorkerStarting();

		Assert.Equal(1, fixture.BranchStagingService.CleanupCalls);
		Assert.NotNull(fixture.BranchStagingService.LastCleanupActiveBranchDirectoryPaths);
		Assert.Contains(branchDirectoryPath, fixture.BranchStagingService.LastCleanupActiveBranchDirectoryPaths!);
	}

	/// <summary>
	/// Verifies cleanup skips stale branch pruning when post-unmount snapshot warnings indicate degraded mount visibility.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldSkipStalePrune_WhenPostUnmountSnapshotWarningIsFatalOrIncomplete()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		Assert.Equal(MergeScanDispatchOutcome.Success, workflow.RunMergePass("interval elapsed", force: false));

		string mountedPath = Path.Combine(fixture.Options.MergedRootPath, "Canonical Title");
		MountSnapshot preUnmountSnapshot = new(
			[
				new MountSnapshotEntry(
					mountedPath,
					"fuse.mergerfs",
					"source",
					"rw",
					isHealthy: true)
			],
			[]);
		MountSnapshot postUnmountSnapshot = new(
			[],
			[
				new MountSnapshotWarning(
					"MOUNT-SNAP-001",
					"findmnt failed",
					MountSnapshotWarningSeverity.DegradedVisibility)
			]);
		fixture.MountSnapshotService.EnqueueSnapshot(preUnmountSnapshot);
		fixture.MountSnapshotService.EnqueueSnapshot(postUnmountSnapshot);

		workflow.OnWorkerStarting();

		Assert.Equal(0, fixture.BranchStagingService.CleanupCalls);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.cleanup" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning &&
				entry.Message.Contains("mount snapshot reliability was degraded", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies cleanup skips stale branch pruning when pre-unmount snapshot warnings indicate degraded mount visibility.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldSkipStalePrune_WhenPreUnmountSnapshotWarningIsDegradedVisibility()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		Assert.Equal(MergeScanDispatchOutcome.Success, workflow.RunMergePass("interval elapsed", force: false));

		string mountedPath = Path.Combine(fixture.Options.MergedRootPath, "Canonical Title");
		MountSnapshot preUnmountSnapshot = new(
			[
				new MountSnapshotEntry(
					mountedPath,
					"fuse.mergerfs",
					"source",
					"rw",
					isHealthy: true)
			],
			[
				new MountSnapshotWarning(
					"MOUNT-SNAP-998",
					"provider indicated degraded visibility",
					MountSnapshotWarningSeverity.DegradedVisibility)
			]);
		MountSnapshot postUnmountSnapshot = new([], []);
		fixture.MountSnapshotService.EnqueueSnapshot(preUnmountSnapshot);
		fixture.MountSnapshotService.EnqueueSnapshot(postUnmountSnapshot);

		workflow.OnWorkerStarting();

		Assert.Equal(0, fixture.BranchStagingService.CleanupCalls);
	}

	/// <summary>
	/// Verifies cleanup still prunes stale branch directories when post-unmount warnings are non-fatal.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldRunStalePrune_WhenPostUnmountSnapshotWarningIsNonFatal()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		Assert.Equal(MergeScanDispatchOutcome.Success, workflow.RunMergePass("interval elapsed", force: false));

		string mountedPath = Path.Combine(fixture.Options.MergedRootPath, "Canonical Title");
		MountSnapshot preUnmountSnapshot = new(
			[
				new MountSnapshotEntry(
					mountedPath,
					"fuse.mergerfs",
					"source",
					"rw",
					isHealthy: true)
			],
			[]);
		MountSnapshot postUnmountSnapshot = new(
			[],
			[
				new MountSnapshotWarning(
					"MOUNT-SNAP-999",
					"custom non-fatal warning",
					MountSnapshotWarningSeverity.NonFatal)
			]);
		fixture.MountSnapshotService.EnqueueSnapshot(preUnmountSnapshot);
		fixture.MountSnapshotService.EnqueueSnapshot(postUnmountSnapshot);

		workflow.OnWorkerStarting();

		Assert.Equal(1, fixture.BranchStagingService.CleanupCalls);
	}

}
