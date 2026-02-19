namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Mount-command fake fixtures for <see cref="MergeMountWorkflowTests"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Mount command fake that records apply/unmount calls and emits configurable outcomes.
	/// </summary>
	private sealed class RecordingMountCommandService : IMergerfsMountCommandService
	{
		/// <summary>
		/// Sequence of apply outcomes consumed per action call.
		/// </summary>
		private readonly Queue<MountActionApplyOutcome> _applyOutcomeSequence = [];

		/// <summary>
		/// Sequence of unmount outcomes consumed per unmount call.
		/// </summary>
		private readonly Queue<MountActionApplyOutcome> _unmountOutcomeSequence = [];

		/// <summary>
		/// Sequence of readiness-probe outcomes consumed per probe call.
		/// </summary>
		private readonly Queue<MountReadinessProbeResult> _readinessProbeSequence = [];

		/// <summary>
		/// Gets applied reconciliation actions.
		/// </summary>
		public List<MountReconciliationAction> AppliedActions
		{
			get;
		} = [];

		/// <summary>
		/// Gets or sets apply-action outcome.
		/// </summary>
		public MountActionApplyOutcome ApplyOutcome
		{
			get;
			set;
		} = MountActionApplyOutcome.Success;

		/// <summary>
		/// Gets or sets default unmount outcome.
		/// </summary>
		public MountActionApplyOutcome UnmountOutcome
		{
			get;
			set;
		} = MountActionApplyOutcome.Success;

		/// <summary>
		/// Gets a value indicating whether the most recent apply action requested high-priority wrappers.
		/// </summary>
		public bool LastApplyCleanupHighPriority
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets or sets a value indicating whether successful mount/remount actions should create mountpoint directories.
		/// </summary>
		public bool AutoCreateMountPointOnSuccess
		{
			get;
			set;
		} = true;

		/// <summary>
		/// Gets recorded unmounted mountpoints.
		/// </summary>
		public List<string> UnmountedMountPoints
		{
			get;
		} = [];

		/// <summary>
		/// Gets or sets default readiness-probe result.
		/// </summary>
		public MountReadinessProbeResult ReadinessProbeResult
		{
			get;
			set;
		} = MountReadinessProbeResult.Ready("probe");

		/// <summary>
		/// Enqueues one apply outcome.
		/// </summary>
		/// <param name="outcome">Outcome value.</param>
		public void EnqueueApplyOutcome(MountActionApplyOutcome outcome)
		{
			_applyOutcomeSequence.Enqueue(outcome);
		}

		/// <summary>
		/// Enqueues one unmount outcome.
		/// </summary>
		/// <param name="outcome">Outcome value.</param>
		public void EnqueueUnmountOutcome(MountActionApplyOutcome outcome)
		{
			_unmountOutcomeSequence.Enqueue(outcome);
		}

		/// <summary>
		/// Enqueues one readiness-probe result.
		/// </summary>
		/// <param name="result">Probe result.</param>
		public void EnqueueReadinessProbeResult(MountReadinessProbeResult result)
		{
			_readinessProbeSequence.Enqueue(result);
		}

		/// <inheritdoc />
		public MountActionApplyResult ApplyAction(
			MountReconciliationAction action,
			string mergerfsOptionsBase,
			TimeSpan commandTimeout,
			TimeSpan pollInterval,
			bool cleanupHighPriority,
			int cleanupPriorityIoniceClass,
			int cleanupPriorityNiceValue,
			CancellationToken cancellationToken = default)
		{
			AppliedActions.Add(action);
			LastApplyCleanupHighPriority = cleanupHighPriority;
			MountActionApplyOutcome outcome = _applyOutcomeSequence.Count > 0
				? _applyOutcomeSequence.Dequeue()
				: ApplyOutcome;
			if (outcome == MountActionApplyOutcome.Success &&
				AutoCreateMountPointOnSuccess &&
				(action.Kind == MountReconciliationActionKind.Mount || action.Kind == MountReconciliationActionKind.Remount))
			{
				Directory.CreateDirectory(action.MountPoint);
			}

			return new MountActionApplyResult(action, outcome, "apply");
		}

		/// <inheritdoc />
		public MountActionApplyResult UnmountMountPoint(
			string mountPoint,
			TimeSpan commandTimeout,
			TimeSpan pollInterval,
			bool cleanupHighPriority,
			int cleanupPriorityIoniceClass,
			int cleanupPriorityNiceValue,
			CancellationToken cancellationToken = default)
		{
			UnmountedMountPoints.Add(mountPoint);
			MountActionApplyOutcome outcome = _unmountOutcomeSequence.Count > 0
				? _unmountOutcomeSequence.Dequeue()
				: UnmountOutcome;
			return new MountActionApplyResult(
				new MountReconciliationAction(
					MountReconciliationActionKind.Unmount,
					mountPoint,
					desiredIdentity: null,
					mountPayload: null,
					MountReconciliationReason.StaleMount),
				outcome,
				"unmount");
		}

		/// <inheritdoc />
		public MountReadinessProbeResult ProbeMountPointReadiness(
			string mountPoint,
			TimeSpan commandTimeout,
			TimeSpan pollInterval,
			CancellationToken cancellationToken = default)
		{
			return _readinessProbeSequence.Count > 0
				? _readinessProbeSequence.Dequeue()
				: ReadinessProbeResult;
		}
	}
}
