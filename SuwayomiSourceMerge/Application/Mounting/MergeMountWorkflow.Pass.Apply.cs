using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Mounts;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Mount-reconciliation action application behavior for <see cref="MergeMountWorkflow"/> merge passes.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
	/// <summary>
	/// Diagnostic token used to detect ENOTCONN mount-readiness failures.
	/// </summary>
	private const string TransportEndpointNotConnectedToken = "Transport endpoint is not connected";

	/// <summary>
	/// Applies one reconciliation plan and maps aggregate action outcomes to dispatch outcome semantics.
	/// </summary>
	/// <param name="actions">Actions to apply.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Aggregate dispatch outcome and busy/failure flags.</returns>
	private (MergeScanDispatchOutcome Outcome, bool HadBusy, bool HadFailure) ApplyPlanActions(
		IReadOnlyList<MountReconciliationAction> actions,
		CancellationToken cancellationToken)
	{
		bool hadBusy = false;
		bool hadFailure = false;
		int consecutiveHardMountFailures = 0;
		List<MountReconciliationAction> successfulMountActions = [];

		for (int index = 0; index < actions.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			MountReconciliationAction action = actions[index];
			MountActionApplyResult applyResult = _mountCommandService.ApplyAction(
				action,
				_options.MergerfsOptionsBase,
				_options.UnmountCommandTimeout,
				_options.CommandPollInterval,
				cleanupHighPriority: _options.CleanupApplyHighPriority,
				cleanupPriorityIoniceClass: _options.CleanupPriorityIoniceClass,
				cleanupPriorityNiceValue: _options.CleanupPriorityNiceValue,
				cancellationToken);
			if (IsMountOrRemount(action) && applyResult.Outcome == MountActionApplyOutcome.Success)
			{
				applyResult = ValidateMountReadinessProbe(action, applyResult, cancellationToken);
				if (applyResult.Outcome == MountActionApplyOutcome.Success)
				{
					successfulMountActions.Add(action);
				}
			}

			if (applyResult.Outcome == MountActionApplyOutcome.Busy)
			{
				hadBusy = true;
			}
			else if (applyResult.Outcome == MountActionApplyOutcome.Failure)
			{
				hadFailure = true;
			}

			_logger.Debug(
				MergeActionEvent,
				"Applied mount reconciliation action.",
				BuildContext(
					("kind", action.Kind.ToString()),
					("mountpoint", action.MountPoint),
					("reason", action.Reason.ToString()),
					("outcome", applyResult.Outcome.ToString()),
					("failure_severity", applyResult.FailureSeverity.ToString()),
					("diagnostic", applyResult.Diagnostic)));

			if (IsMountOrRemount(action))
			{
				if (applyResult.Outcome == MountActionApplyOutcome.Failure)
				{
					if (applyResult.FailureSeverity == MountActionFailureSeverity.Hard)
					{
						consecutiveHardMountFailures++;
					}
					else
					{
						consecutiveHardMountFailures = 0;
					}
				}
				else
				{
					consecutiveHardMountFailures = 0;
				}
			}
			else
			{
				consecutiveHardMountFailures = 0;
			}

			if (consecutiveHardMountFailures >= _options.MaxConsecutiveMountFailures)
			{
				int skippedActions = actions.Count - index - 1;
				hadFailure = true;
				_logger.Warning(
					MergeActionFailFastEvent,
					"Aborted remaining apply actions after reaching consecutive hard mount failure threshold.",
					BuildContext(
						("threshold", _options.MaxConsecutiveMountFailures.ToString()),
						("consecutive_hard_failures", consecutiveHardMountFailures.ToString()),
						("skipped_actions", skippedActions.ToString()),
						("last_mountpoint", action.MountPoint)));
				break;
			}
		}

		if (successfulMountActions.Count > 0)
		{
			hadFailure = ValidatePostApplyMountSnapshot(successfulMountActions) || hadFailure;
		}

		if (hadBusy && hadFailure)
		{
			return (MergeScanDispatchOutcome.Mixed, hadBusy, hadFailure);
		}

		if (hadBusy)
		{
			return (MergeScanDispatchOutcome.Busy, hadBusy, hadFailure);
		}

		if (hadFailure)
		{
			return (MergeScanDispatchOutcome.Failure, hadBusy, hadFailure);
		}

		return (MergeScanDispatchOutcome.Success, hadBusy, hadFailure);
	}

	/// <summary>
	/// Returns whether one reconciliation action is a mount/remount action.
	/// </summary>
	/// <param name="action">Action to inspect.</param>
	/// <returns><see langword="true"/> when action is mount or remount.</returns>
	private static bool IsMountOrRemount(MountReconciliationAction action)
	{
		return action.Kind == MountReconciliationActionKind.Mount
			|| action.Kind == MountReconciliationActionKind.Remount;
	}

	/// <summary>
	/// Validates one mount-readiness probe after a command-reported mount/remount success.
	/// </summary>
	/// <param name="action">Applied mount/remount action.</param>
	/// <param name="applyResult">Original successful apply result.</param>
	/// <returns>Updated apply result with readiness failures mapped to failure outcomes.</returns>
	private MountActionApplyResult ValidateMountReadinessProbe(
		MountReconciliationAction action,
		MountActionApplyResult applyResult,
		CancellationToken cancellationToken)
	{
		string normalizedMountPoint = Path.GetFullPath(action.MountPoint);
		MountReadinessProbeResult probeResult = _mountCommandService.ProbeMountPointReadiness(
			normalizedMountPoint,
			_options.UnmountCommandTimeout,
			_options.CommandPollInterval,
			cancellationToken);
		if (!probeResult.IsReady)
		{
			if (ContainsTransportEndpointNotConnectedDiagnostic(probeResult.Diagnostic))
			{
				return AttemptEnotconnRecovery(action, normalizedMountPoint, probeResult, cancellationToken);
			}

			return new MountActionApplyResult(
				action,
				MountActionApplyOutcome.Failure,
				$"Mount readiness check failed: mountpoint probe failed for '{normalizedMountPoint}'. {probeResult.Diagnostic}");
		}

		return applyResult;
	}

	/// <summary>
	/// Attempts one inline ENOTCONN recovery cycle for one mount/remount action.
	/// </summary>
	/// <param name="action">Applied mount/remount action.</param>
	/// <param name="normalizedMountPoint">Normalized mountpoint path.</param>
	/// <param name="initialProbeResult">Initial failed readiness probe result.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Recovery success/failure apply result.</returns>
	private MountActionApplyResult AttemptEnotconnRecovery(
		MountReconciliationAction action,
		string normalizedMountPoint,
		MountReadinessProbeResult initialProbeResult,
		CancellationToken cancellationToken)
	{
		MountActionApplyResult recoveryUnmountResult = _mountCommandService.UnmountMountPoint(
			normalizedMountPoint,
			_options.UnmountCommandTimeout,
			_options.CommandPollInterval,
			_options.CleanupApplyHighPriority,
			_options.CleanupPriorityIoniceClass,
			_options.CleanupPriorityNiceValue,
			cancellationToken);
		if (recoveryUnmountResult.Outcome != MountActionApplyOutcome.Success)
		{
			return new MountActionApplyResult(
				action,
				MountActionApplyOutcome.Failure,
				BuildEnotconnRecoveryDiagnostic(
					normalizedMountPoint,
					initialProbeResult.Diagnostic,
					recoveryUnmountResult.Diagnostic,
					retryApplyDiagnostic: "not attempted",
					retryProbeDiagnostic: "not attempted"),
				MountActionFailureSeverity.Hard);
		}

		MountReconciliationAction retryAction = BuildMountOnlyRetryAction(action);
		MountActionApplyResult retryApplyResult = _mountCommandService.ApplyAction(
			retryAction,
			_options.MergerfsOptionsBase,
			_options.UnmountCommandTimeout,
			_options.CommandPollInterval,
			_options.CleanupApplyHighPriority,
			_options.CleanupPriorityIoniceClass,
			_options.CleanupPriorityNiceValue,
			cancellationToken);
		if (retryApplyResult.Outcome != MountActionApplyOutcome.Success)
		{
			return new MountActionApplyResult(
				action,
				MountActionApplyOutcome.Failure,
				BuildEnotconnRecoveryDiagnostic(
					normalizedMountPoint,
					initialProbeResult.Diagnostic,
					recoveryUnmountResult.Diagnostic,
					retryApplyResult.Diagnostic,
					retryProbeDiagnostic: "not attempted"),
				MountActionFailureSeverity.Hard);
		}

		MountReadinessProbeResult retryProbeResult = _mountCommandService.ProbeMountPointReadiness(
			normalizedMountPoint,
			_options.UnmountCommandTimeout,
			_options.CommandPollInterval,
			cancellationToken);
		if (!retryProbeResult.IsReady)
		{
			return new MountActionApplyResult(
				action,
				MountActionApplyOutcome.Failure,
				BuildEnotconnRecoveryDiagnostic(
					normalizedMountPoint,
					initialProbeResult.Diagnostic,
					recoveryUnmountResult.Diagnostic,
					retryApplyResult.Diagnostic,
					retryProbeResult.Diagnostic),
				MountActionFailureSeverity.Hard);
		}

		return new MountActionApplyResult(
			action,
			MountActionApplyOutcome.Success,
			$"Mount readiness recovered after ENOTCONN probe failure for '{normalizedMountPoint}'. initial_probe='{initialProbeResult.Diagnostic}' recovery_unmount='{recoveryUnmountResult.Diagnostic}' retry_apply='{retryApplyResult.Diagnostic}' retry_probe='{retryProbeResult.Diagnostic}'.");
	}

	/// <summary>
	/// Builds one mount-only retry action for ENOTCONN recovery.
	/// </summary>
	/// <param name="action">Original mount/remount action.</param>
	/// <returns>Mount action preserving original target and payload fields.</returns>
	private static MountReconciliationAction BuildMountOnlyRetryAction(MountReconciliationAction action)
	{
		ArgumentNullException.ThrowIfNull(action);
		if (action.Kind == MountReconciliationActionKind.Mount)
		{
			return action;
		}

		return new MountReconciliationAction(
			MountReconciliationActionKind.Mount,
			action.MountPoint,
			action.DesiredIdentity,
			action.MountPayload,
			action.Reason);
	}

	/// <summary>
	/// Builds one combined ENOTCONN recovery diagnostic payload.
	/// </summary>
	/// <param name="normalizedMountPoint">Normalized mountpoint path.</param>
	/// <param name="initialProbeDiagnostic">Initial probe diagnostic.</param>
	/// <param name="recoveryUnmountDiagnostic">Recovery unmount diagnostic.</param>
	/// <param name="retryApplyDiagnostic">Retry apply diagnostic.</param>
	/// <param name="retryProbeDiagnostic">Retry probe diagnostic.</param>
	/// <returns>Combined diagnostic message.</returns>
	private static string BuildEnotconnRecoveryDiagnostic(
		string normalizedMountPoint,
		string initialProbeDiagnostic,
		string recoveryUnmountDiagnostic,
		string retryApplyDiagnostic,
		string retryProbeDiagnostic)
	{
		return
			$"Mount readiness ENOTCONN recovery failed for '{normalizedMountPoint}'. initial_probe='{initialProbeDiagnostic}' recovery_unmount='{recoveryUnmountDiagnostic}' retry_apply='{retryApplyDiagnostic}' retry_probe='{retryProbeDiagnostic}'.";
	}

	/// <summary>
	/// Returns whether one readiness diagnostic indicates ENOTCONN transport-endpoint disconnect state.
	/// </summary>
	/// <param name="diagnostic">Readiness diagnostic text.</param>
	/// <returns><see langword="true"/> when ENOTCONN text is present; otherwise <see langword="false"/>.</returns>
	private static bool ContainsTransportEndpointNotConnectedDiagnostic(string diagnostic)
	{
		ArgumentNullException.ThrowIfNull(diagnostic);
		return diagnostic.Contains(TransportEndpointNotConnectedToken, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Validates mount/remount success actions against one post-apply snapshot.
	/// </summary>
	/// <param name="successfulMountActions">Successful mount/remount actions from this apply pass.</param>
	/// <returns><see langword="true"/> when one or more mount readiness failures were detected; otherwise <see langword="false"/>.</returns>
	private bool ValidatePostApplyMountSnapshot(IReadOnlyList<MountReconciliationAction> successfulMountActions)
	{
		ArgumentNullException.ThrowIfNull(successfulMountActions);

		MountSnapshot readinessSnapshot = _mountSnapshotService.Capture();
		Dictionary<string, MountSnapshotEntry> snapshotEntriesByMountPoint = BuildSnapshotEntriesByMountPoint(readinessSnapshot.Entries);
		HashSet<string> checkedMountPoints = new(PathSafetyPolicy.GetPathComparer());
		bool hadFailure = false;

		for (int index = 0; index < successfulMountActions.Count; index++)
		{
			MountReconciliationAction action = successfulMountActions[index];
			string normalizedMountPoint = Path.GetFullPath(action.MountPoint);
			if (!checkedMountPoints.Add(normalizedMountPoint))
			{
				continue;
			}

			if (!snapshotEntriesByMountPoint.TryGetValue(normalizedMountPoint, out MountSnapshotEntry? entry))
			{
				hadFailure = true;
				LogSnapshotReadinessFailure(
					normalizedMountPoint,
					$"Mount readiness check failed after apply: no mount snapshot entry found for '{normalizedMountPoint}'. snapshot_entries='{readinessSnapshot.Entries.Count}' snapshot_warnings='{readinessSnapshot.Warnings.Count}' first_warning_code='{GetFirstSnapshotWarningCode(readinessSnapshot)}' first_warning_message='{GetFirstSnapshotWarningMessage(readinessSnapshot)}'.");
				continue;
			}

			if (!entry.FileSystemType.Contains("mergerfs", StringComparison.OrdinalIgnoreCase))
			{
				hadFailure = true;
				LogSnapshotReadinessFailure(
					normalizedMountPoint,
					$"Mount readiness check failed after apply: expected mergerfs filesystem type for '{normalizedMountPoint}' but observed '{entry.FileSystemType}'.");
			}
		}

		return hadFailure;
	}

	/// <summary>
	/// Builds an entry lookup by normalized mountpoint path.
	/// </summary>
	/// <param name="entries">Snapshot entries to index.</param>
	/// <returns>Lookup keyed by normalized mountpoint path.</returns>
	private static Dictionary<string, MountSnapshotEntry> BuildSnapshotEntriesByMountPoint(
		IReadOnlyList<MountSnapshotEntry> entries)
	{
		ArgumentNullException.ThrowIfNull(entries);

		Dictionary<string, MountSnapshotEntry> entriesByMountPoint = new(PathSafetyPolicy.GetPathComparer());
		for (int index = 0; index < entries.Count; index++)
		{
			MountSnapshotEntry entry = entries[index];
			string entryMountPoint = Path.GetFullPath(entry.MountPoint);
			entriesByMountPoint.TryAdd(entryMountPoint, entry);
		}

		return entriesByMountPoint;
	}

	/// <summary>
	/// Returns the first snapshot warning code when available.
	/// </summary>
	/// <param name="snapshot">Readiness snapshot value.</param>
	/// <returns>First warning code when available; otherwise <c>none</c>.</returns>
	private static string GetFirstSnapshotWarningCode(MountSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		return snapshot.Warnings.Count > 0
			? snapshot.Warnings[0].Code
			: "none";
	}

	/// <summary>
	/// Returns the first snapshot warning message when available.
	/// </summary>
	/// <param name="snapshot">Readiness snapshot value.</param>
	/// <returns>First warning message when available; otherwise <c>none</c>.</returns>
	private static string GetFirstSnapshotWarningMessage(MountSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		return snapshot.Warnings.Count > 0
			? snapshot.Warnings[0].Message
			: "none";
	}

	/// <summary>
	/// Logs one snapshot-based mount readiness failure.
	/// </summary>
	/// <param name="mountPoint">Mountpoint that failed snapshot readiness validation.</param>
	/// <param name="message">Failure message.</param>
	private void LogSnapshotReadinessFailure(string mountPoint, string message)
	{
		_logger.Warning(
			MergePassWarningEvent,
			message,
			BuildContext(("mountpoint", mountPoint)));
	}

}
