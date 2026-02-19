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
	/// Verifies fallback canonical title resolution strips trailing scene-tag suffixes for display naming.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldStripSceneTagSuffix_FromFallbackCanonicalSourceTitle()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.SourceVolumePaths[0], "SourceA", "Solo Leveling [Official]"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Contains(
			fixture.ReconciliationService.LastInput!.DesiredMounts,
			static mount => mount.MountPoint.EndsWith(
				Path.DirectorySeparatorChar + "Solo Leveling",
				StringComparison.Ordinal));
		Assert.DoesNotContain(
			fixture.ReconciliationService.LastInput.DesiredMounts,
			static mount => mount.MountPoint.EndsWith(
				Path.DirectorySeparatorChar + "Solo Leveling [Official]",
				StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies fallback canonical title resolution keeps original title when stripping would produce an empty value.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldKeepOriginalTitle_WhenSceneTagStrippingEmptiesFallbackTitle()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.SourceVolumePaths[0], "SourceA", "(Official)"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Contains(
			fixture.ReconciliationService.LastInput!.DesiredMounts,
			static mount => mount.MountPoint.EndsWith(
				Path.DirectorySeparatorChar + "(Official)",
				StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies tagged-only override titles are preserved to avoid creating new stripped duplicates.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldPreserveTaggedOnlyOverrideTitle_AndLogManualRenameWarning()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		Directory.CreateDirectory(Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Solo Leveling [Official]"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Contains(
			fixture.ReconciliationService.LastInput!.DesiredMounts,
			static mount => mount.MountPoint.EndsWith(
				Path.DirectorySeparatorChar + "Solo Leveling [Official]",
				StringComparison.Ordinal));
		Assert.DoesNotContain(
			fixture.ReconciliationService.LastInput.DesiredMounts,
			static mount => mount.MountPoint.EndsWith(
				Path.DirectorySeparatorChar + "Solo Leveling",
				StringComparison.Ordinal));
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.warning"
				&& entry.Level == LogLevel.Warning
				&& entry.Message.Contains("Preserved tagged-only override title", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies duplicate override-title variants collapse to stripped canonical titles.
	/// </summary>
	/// <param name="strippedTitle">Expected stripped canonical title.</param>
	/// <param name="taggedTitle">Tagged variant that should collapse into <paramref name="strippedTitle"/>.</param>
	[Theory]
	[MemberData(nameof(GetTaggedDuplicateFixtures))]
	public void RunMergePass_Expected_ShouldChooseStrippedCanonical_WhenTaggedAndStrippedOverrideVariantsExist(
		string strippedTitle,
		string taggedTitle)
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		string overrideVolumePath = fixture.VolumeDiscoveryService.OverrideVolumePaths[0];
		Directory.CreateDirectory(Path.Combine(overrideVolumePath, strippedTitle));
		Directory.CreateDirectory(Path.Combine(overrideVolumePath, taggedTitle));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.NotNull(fixture.ReconciliationService.LastInput);
		Assert.Contains(
			fixture.ReconciliationService.LastInput!.DesiredMounts,
			mount => mount.MountPoint.EndsWith(
				Path.DirectorySeparatorChar + strippedTitle,
				StringComparison.Ordinal));
		Assert.DoesNotContain(
			fixture.ReconciliationService.LastInput.DesiredMounts,
			mount => mount.MountPoint.EndsWith(
				Path.DirectorySeparatorChar + taggedTitle,
				StringComparison.Ordinal));
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

	public static IEnumerable<object[]> GetTaggedDuplicateFixtures()
	{
		yield return ["Becoming a Magic School Mage", "Becoming a Magic School Mage (Official)"];
		yield return ["Disciple of the Holy Sword", "Disciple of the Holy Sword (Official)"];
		yield return ["Heavenly Demon Reborn!", "Heavenly Demon Reborn! [Official]"];
		yield return ["I Stole the First Ranker's Soul", "I Stole the First Ranker's Soul [Official]"];
		yield return ["Illusion Hunter from Another World", "Illusion Hunter from Another World [Official]"];
		yield return ["I'm a Curse Crafter, and I Don't Need an S-Rank Party!", "I'm a Curse Crafter, and I Don't Need an S-Rank Party! [Official]"];
		yield return ["Log Into The Future", "Log Into The Future [Tapas Official]"];
		yield return ["Magic Academy's Genius Blinker", "Magic Academy's Genius Blinker (Asura Scans)"];
		yield return ["Solo Leveling", "Solo Leveling [Official]"];
		yield return ["The Legend of the Northern Blade", "The Legend of the Northern Blade (Official)"];
		yield return ["The Legendary Spearman Returns", "The Legendary Spearman Returns (Official)"];
	}
}
