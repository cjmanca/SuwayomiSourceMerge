namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Consecutive mount failure fail-fast and mount readiness coverage for <see cref="MergeMountWorkflow"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Verifies mount apply actions fail fast once the consecutive failure threshold is reached.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldFailFastAfterConsecutiveMountFailuresReachThreshold()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory, maxConsecutiveMountFailures: 2);
		Directory.CreateDirectory(Path.Combine(fixture.Options.SourcesRootPath, "disk1", "SourceA", "Another Title"));
		Directory.CreateDirectory(Path.Combine(fixture.Options.SourcesRootPath, "disk1", "SourceA", "Third Title"));
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Failure;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Equal(2, fixture.MountCommandService.AppliedActions.Count);
		Assert.Contains(
			fixture.Logger.Events,
			static log => log.EventId == "merge.workflow.action_fail_fast"
				&& log.Level == LogLevel.Warning
				&& log.Message.Contains("consecutive mount failure threshold", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies the consecutive failure counter resets when a mount action succeeds.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldResetConsecutiveFailureCounterAfterSuccessfulMount()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory, maxConsecutiveMountFailures: 2);
		string mountPointA = Path.Combine(fixture.Options.MergedRootPath, "A");
		string mountPointB = Path.Combine(fixture.Options.MergedRootPath, "B");
		string mountPointC = Path.Combine(fixture.Options.MergedRootPath, "C");
		string mountPointD = Path.Combine(fixture.Options.MergedRootPath, "D");
		Directory.CreateDirectory(mountPointB);
		Directory.CreateDirectory(mountPointD);
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan(
		[
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointA, "id-a", "payload-a", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointB, "id-b", "payload-b", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointC, "id-c", "payload-c", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointD, "id-d", "payload-d", MountReconciliationReason.MissingMount)
		]);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Failure);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Success);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Failure);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Success);
		fixture.MountSnapshotService.EnqueueSnapshot(new MountSnapshot([], []));
		fixture.MountSnapshotService.EnqueueSnapshot(
			new MountSnapshot(
			[
				new MountSnapshotEntry(mountPointB, "fuse.mergerfs", "src-b", "rw", isHealthy: null),
				new MountSnapshotEntry(mountPointD, "fuse.mergerfs", "src-d", "rw", isHealthy: null)
			],
			[]));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Equal(4, fixture.MountCommandService.AppliedActions.Count);
		Assert.DoesNotContain(fixture.Logger.Events, log => log.EventId == "merge.workflow.action_fail_fast");
	}

	/// <summary>
	/// Verifies one post-apply snapshot validates all successful mounts instead of capturing once per mount action.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldValidateMultipleSuccessfulMountsAgainstOnePostApplySnapshot()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		Directory.CreateDirectory(Path.Combine(fixture.Options.SourcesRootPath, "disk1", "SourceA", "Another Title"));
		Directory.CreateDirectory(Path.Combine(fixture.Options.SourcesRootPath, "disk1", "SourceA", "Third Title"));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(3, fixture.MountCommandService.AppliedActions.Count);
		Assert.Equal(2, fixture.MountSnapshotService.CaptureCount);
	}

	/// <summary>
	/// Verifies post-apply snapshot validation fails when a successful mountpoint resolves to a non-mergerfs filesystem type.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldReturnFailure_WhenPostApplySnapshotReportsWrongFilesystemType()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string mountPoint = Path.Combine(fixture.Options.MergedRootPath, "Canonical Title");
		fixture.MountSnapshotService.AutoIncludeAppliedMountActions = false;
		fixture.MountSnapshotService.EnqueueSnapshot(new MountSnapshot([], []));
		fixture.MountSnapshotService.EnqueueSnapshot(
			new MountSnapshot(
			[
				new MountSnapshotEntry(mountPoint, "ext4", "disk", "rw", isHealthy: null)
			],
			[]));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Equal(2, fixture.MountSnapshotService.CaptureCount);
		Assert.Contains(
			fixture.Logger.Events,
			log => log.EventId == "merge.workflow.warning"
				&& log.Message.Contains("expected mergerfs filesystem type", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies command-reported mount success is downgraded when readiness checks fail.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenMountReadinessValidationFailsAfterCommandSuccess()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Success;
		fixture.MountSnapshotService.AutoIncludeAppliedMountActions = false;
		fixture.MountSnapshotService.NextSnapshot = new MountSnapshot([], []);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
	}

	/// <summary>
	/// Verifies timeout-bounded readiness probe failures downgrade command-reported mount success.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenReadinessProbeTimesOut()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Success;
		fixture.MountCommandService.ReadinessProbeResult = MountReadinessProbeResult.NotReady("Readiness probe command timed out.");
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
	}

	/// <summary>
	/// Verifies readiness-probe failures contribute to consecutive mount failure fail-fast threshold.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldFailFast_WhenReadinessProbeFailureReachesThreshold()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory, maxConsecutiveMountFailures: 1);
		Directory.CreateDirectory(Path.Combine(fixture.Options.SourcesRootPath, "disk1", "SourceA", "Another Title"));
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Success;
		fixture.MountCommandService.ReadinessProbeResult = MountReadinessProbeResult.NotReady("Readiness probe command exited non-zero (1): Transport endpoint is not connected");
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
		Assert.Contains(fixture.Logger.Events, static log => log.EventId == "merge.workflow.action_fail_fast");
	}
}
