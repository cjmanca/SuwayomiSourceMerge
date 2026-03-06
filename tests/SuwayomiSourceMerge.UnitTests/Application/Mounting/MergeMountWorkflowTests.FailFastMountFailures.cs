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
				&& log.Message.Contains("consecutive hard mount failure threshold", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies soft mount failures do not advance hard-failure fail-fast counters.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldNotFailFast_WhenOnlySoftMountFailuresOccur()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory, maxConsecutiveMountFailures: 2);
		string mountPointA = Path.Combine(fixture.Options.MergedRootPath, "A");
		string mountPointB = Path.Combine(fixture.Options.MergedRootPath, "B");
		string mountPointC = Path.Combine(fixture.Options.MergedRootPath, "C");
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan(
		[
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointA, "id-a", "payload-a", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointB, "id-b", "payload-b", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointC, "id-c", "payload-c", MountReconciliationReason.MissingMount)
		]);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Soft);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Soft);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Soft);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Equal(3, fixture.MountCommandService.AppliedActions.Count);
		Assert.DoesNotContain(fixture.Logger.Events, log => log.EventId == "merge.workflow.action_fail_fast");
	}

	/// <summary>
	/// Verifies soft failures reset hard-failure streak counting for fail-fast thresholding.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldResetHardFailureCounterAfterSoftFailure()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory, maxConsecutiveMountFailures: 2);
		string mountPointA = Path.Combine(fixture.Options.MergedRootPath, "A");
		string mountPointB = Path.Combine(fixture.Options.MergedRootPath, "B");
		string mountPointC = Path.Combine(fixture.Options.MergedRootPath, "C");
		string mountPointD = Path.Combine(fixture.Options.MergedRootPath, "D");
		string mountPointE = Path.Combine(fixture.Options.MergedRootPath, "E");
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan(
		[
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointA, "id-a", "payload-a", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointB, "id-b", "payload-b", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointC, "id-c", "payload-c", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointD, "id-d", "payload-d", MountReconciliationReason.MissingMount),
			new MountReconciliationAction(MountReconciliationActionKind.Mount, mountPointE, "id-e", "payload-e", MountReconciliationReason.MissingMount)
		]);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Soft);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Hard);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Soft);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Hard);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Success);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Equal(5, fixture.MountCommandService.AppliedActions.Count);
		Assert.DoesNotContain(fixture.Logger.Events, static log => log.EventId == "merge.workflow.action_fail_fast");
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
	/// Verifies ENOTCONN readiness probe failures trigger one inline detach/mount/probe recovery attempt.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldRecoverFromEnotconnReadinessFailure()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Success);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Success);
		fixture.MountCommandService.EnqueueReadinessProbeResult(
			MountReadinessProbeResult.NotReady("Readiness probe command exited non-zero (1): Transport endpoint is not connected"));
		fixture.MountCommandService.EnqueueReadinessProbeResult(
			MountReadinessProbeResult.Ready("Readiness probe command succeeded."));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(2, fixture.MountCommandService.AppliedActions.Count);
		Assert.Single(fixture.MountCommandService.UnmountedMountPoints);
	}

	/// <summary>
	/// Verifies remount ENOTCONN recovery retries with a mount-only action to avoid double-unmount flows.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldRetryEnotconnRecoveryWithMountAction_WhenOriginalActionWasRemount()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string mountPoint = Path.Combine(fixture.Options.MergedRootPath, "Canonical Title");
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan(
		[
			new MountReconciliationAction(
				MountReconciliationActionKind.Remount,
				mountPoint,
				"id-remount",
				"/state/linkA=RW:/state/linkB=RO",
				MountReconciliationReason.DesiredIdentityMismatch)
		]);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Success);
		fixture.MountCommandService.EnqueueApplyOutcome(MountActionApplyOutcome.Success);
		fixture.MountCommandService.EnqueueReadinessProbeResult(
			MountReadinessProbeResult.NotReady("Readiness probe command exited non-zero (1): Transport endpoint is not connected"));
		fixture.MountCommandService.EnqueueReadinessProbeResult(
			MountReadinessProbeResult.Ready("Readiness probe command succeeded."));
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(2, fixture.MountCommandService.AppliedActions.Count);
		Assert.Equal(MountReconciliationActionKind.Remount, fixture.MountCommandService.AppliedActions[0].Kind);
		Assert.Equal(MountReconciliationActionKind.Mount, fixture.MountCommandService.AppliedActions[1].Kind);
		Assert.Single(fixture.MountCommandService.UnmountedMountPoints);
	}

	/// <summary>
	/// Verifies non-ENOTCONN readiness probe failures keep existing no-recovery failure behavior.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldNotAttemptRecovery_WhenReadinessProbeFailsWithoutEnotconn()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Success;
		fixture.MountCommandService.ReadinessProbeResult = MountReadinessProbeResult.NotReady("Readiness probe command exited non-zero (1): permission denied");
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Single(fixture.MountCommandService.AppliedActions);
		Assert.Empty(fixture.MountCommandService.UnmountedMountPoints);
	}

	/// <summary>
	/// Verifies failed ENOTCONN recovery contributes hard failures and emits combined diagnostics.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldEmitCombinedRecoveryDiagnostic_WhenEnotconnRecoveryFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory, maxConsecutiveMountFailures: 1);
		fixture.MountCommandService.ApplyOutcome = MountActionApplyOutcome.Success;
		fixture.MountCommandService.ReadinessProbeResult = MountReadinessProbeResult.NotReady("Readiness probe command exited non-zero (1): Transport endpoint is not connected");
		fixture.MountCommandService.UnmountOutcome = MountActionApplyOutcome.Failure;
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Contains(fixture.Logger.Events, static log => log.EventId == "merge.workflow.action_fail_fast");
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.action"
				&& entry.Context is not null
				&& entry.Context.TryGetValue("diagnostic", out string? diagnostic)
				&& diagnostic.Contains("recovery_unmount", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies per-action diagnostics log the failure severity classification.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldLogFailureSeverityContext_ForAppliedActions()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.MountCommandService.EnqueueApplyResult(MountActionApplyOutcome.Failure, MountActionFailureSeverity.Soft);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		_ = workflow.RunMergePass("startup", force: false);

		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.action"
				&& entry.Context is not null
				&& entry.Context.TryGetValue("failure_severity", out string? severity)
				&& severity == nameof(MountActionFailureSeverity.Soft));
	}
}
