namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies merge-pass outcome mapping and cancellation behavior for <see cref="MergeMountWorkflow"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Verifies missing override volume discovery causes merge dispatch failure.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenNoOverrideVolumesAreDiscovered()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.OverrideVolumePaths = [];
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
	}

	/// <summary>
	/// Verifies busy mount action outcomes map to busy merge dispatch results.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnBusy_WhenAnyMountActionIsBusy()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Busy;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Busy, outcome);
	}

	/// <summary>
	/// Verifies mixed busy/failure action outcomes map to mixed dispatch outcomes.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnMixed_WhenBusyAndFailureActionsAreObserved()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.ReconciliationService.NextPlanFactory = input => new MountReconciliationPlan(
		[
			new MountReconciliationAction(
				MountReconciliationActionKind.Mount,
				input.DesiredMounts[0].MountPoint,
				input.DesiredMounts[0].DesiredIdentity,
				input.DesiredMounts[0].MountPayload,
				MountReconciliationReason.MissingMount),
			new MountReconciliationAction(
				MountReconciliationActionKind.Remount,
				input.DesiredMounts[0].MountPoint,
				input.DesiredMounts[0].DesiredIdentity,
				input.DesiredMounts[0].MountPayload,
				MountReconciliationReason.ForcedRemount)
		]);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Busy);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Failure);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Mixed, outcome);
	}

	/// <summary>
	/// Verifies build failures combined with busy action outcomes map to mixed dispatch outcomes.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnMixed_WhenBuildFailureAndBusyOutcomeAreBothPresent()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		Directory.CreateDirectory(Path.Combine(fixture.Options.SourcesRootPath, "disk1", "SourceA", "Broken Title"));
		fixture.BranchPlanningService.ThrowOnCanonicalTitles.Add("Broken Title");
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Busy;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Mixed, outcome);
	}

	/// <summary>
	/// Verifies non-busy mount failures map to failure dispatch outcomes.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenAnyMountActionFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Failure;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
	}

	/// <summary>
	/// Verifies branch-planning cancellations are propagated instead of being downgraded to generic build failures.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldRethrowCancellation_WhenBranchPlanningIsCancelled()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.BranchPlanningService.ThrowCancellationOnCanonicalTitles.Add("Canonical Title");
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		Assert.Throws<OperationCanceledException>(() => workflow.RunMergePass("interval elapsed", force: false));
	}

	/// <summary>
	/// Verifies fatal branch-planning exceptions are rethrown and not downgraded to build failures.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldRethrowFatalException_WhenBranchPlanningThrowsFatal()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.BranchPlanningService.ThrowFatalOnCanonicalTitles.Add("Canonical Title");
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		Assert.Throws<OutOfMemoryException>(() => workflow.RunMergePass("interval elapsed", force: false));
	}

	/// <summary>
	/// Verifies override-title discovery observes cancellation once discovery has already started.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldThrow_WhenCancellationRequestedDuringOverrideTitleDiscovery()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		using CancellationTokenSource cancellationTokenSource = new();
		fixture.VolumeDiscoveryService.OnDiscover = cancellationTokenSource.Cancel;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		Assert.Throws<OperationCanceledException>(() => workflow.RunMergePass(
			"interval elapsed",
			force: false,
			cancellationToken: cancellationTokenSource.Token));
	}
}
