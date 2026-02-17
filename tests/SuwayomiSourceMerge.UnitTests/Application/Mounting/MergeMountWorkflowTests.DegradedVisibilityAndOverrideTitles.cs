namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Volumes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies degraded-visibility safety and override-only grouping behavior for <see cref="MergeMountWorkflow"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Verifies override titles with empty normalized keys are skipped with warning diagnostics.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldSkipInvalidOverrideTitles_AndLogWarning()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "!!!"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.warning"
				&& entry.Level == LogLevel.Warning
				&& entry.Message.Contains("empty comparison key", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies override-only grouping excludes override directory names that normalize to empty keys.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldExcludeInvalidOverrideOnlyTitles_FromDesiredGrouping()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "!!!"));
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Valid Override"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Single(fixture.ReconciliationService.LastInput!.DesiredMounts);
		Assert.Equal(
			Path.Combine(fixture.Options.MergedRootPath, "Valid Override"),
			fixture.ReconciliationService.LastInput.DesiredMounts[0].MountPoint);
	}

	/// <summary>
	/// Verifies invalid override-title filtering does not mask downstream mount apply failures.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenApplyFailsAndOverrideTitlesContainInvalidEntries()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "!!!"));
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Failure;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.NotEmpty(fixture.MountCommandService.AppliedActions);
	}

	/// <summary>
	/// Verifies source-discovery warnings suppress stale-unmount apply actions.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldSuppressStaleUnmountActions_WhenSourceDiscoveryReportsWarnings()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		fixture.VolumeDiscoveryService.Warnings =
		[
			new ContainerVolumeDiscoveryWarning(
				"VOL-DISC-001",
				fixture.Options.SourcesRootPath,
				"Source root path is temporarily unavailable.")
		];
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
		Assert.Empty(fixture.MountCommandService.AppliedActions);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.warning"
				&& entry.Level == LogLevel.Warning
				&& entry.Message.Contains("Suppressed stale-unmount actions", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies override-only titles are included in desired grouping even when no source branches exist.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldIncludeOverrideOnlyTitle_WhenNoSourceBranchesExist()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Override Only Title"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Single(fixture.ReconciliationService.LastInput!.DesiredMounts);
		Assert.Equal(
			Path.Combine(fixture.Options.MergedRootPath, "Override Only Title"),
			fixture.ReconciliationService.LastInput.DesiredMounts[0].MountPoint);
		Assert.Single(fixture.BranchPlanningService.PlannedRequests);
		Assert.Empty(fixture.BranchPlanningService.PlannedRequests[0].SourceBranches);
	}

	/// <summary>
	/// Verifies override-only alias titles still honor canonical-equivalence naming.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldUseCanonicalTitle_ForOverrideOnlyAlias()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Title One"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Single(fixture.ReconciliationService.LastInput!.DesiredMounts);
		Assert.Equal(
			Path.Combine(fixture.Options.MergedRootPath, "Canonical Title"),
			fixture.ReconciliationService.LastInput.DesiredMounts[0].MountPoint);
		Assert.Single(fixture.BranchPlanningService.PlannedRequests);
		Assert.Equal("Canonical Title", fixture.BranchPlanningService.PlannedRequests[0].CanonicalTitle);
	}

	/// <summary>
	/// Verifies source-discovery warning suppression does not mask non-stale mount-action failures.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenSourceDiscoveryWarningAndMountApplyFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		fixture.VolumeDiscoveryService.Warnings =
		[
			new ContainerVolumeDiscoveryWarning(
				"VOL-DISC-001",
				fixture.Options.SourcesRootPath,
				"Source root path is temporarily unavailable.")
		];
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Override Only Title"));
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Failure;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
		Assert.Equal(MountReconciliationActionKind.Mount, fixture.MountCommandService.AppliedActions[0].Kind);
	}
}
