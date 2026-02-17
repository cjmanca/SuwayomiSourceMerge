using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Mounts;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Mount-reconciliation action application behavior for <see cref="MergeMountWorkflow"/> merge passes.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
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
		int consecutiveMountFailures = 0;

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
				applyResult = ValidateMountReadiness(action, applyResult);
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
					("diagnostic", applyResult.Diagnostic)));

			if (IsMountOrRemount(action))
			{
				consecutiveMountFailures = applyResult.Outcome == MountActionApplyOutcome.Failure
					? consecutiveMountFailures + 1
					: 0;
			}
			else
			{
				consecutiveMountFailures = 0;
			}

			if (consecutiveMountFailures >= _options.MaxConsecutiveMountFailures)
			{
				int skippedActions = actions.Count - index - 1;
				hadFailure = true;
				_logger.Warning(
					MergeActionFailFastEvent,
					"Aborted remaining apply actions after reaching consecutive mount failure threshold.",
					BuildContext(
						("threshold", _options.MaxConsecutiveMountFailures.ToString()),
						("consecutive_failures", consecutiveMountFailures.ToString()),
						("skipped_actions", skippedActions.ToString()),
						("last_mountpoint", action.MountPoint)));
				break;
			}
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
	/// Validates mount readiness after a command-reported mount/remount success.
	/// </summary>
	/// <param name="action">Applied mount/remount action.</param>
	/// <param name="applyResult">Original successful apply result.</param>
	/// <returns>Updated apply result with readiness failures mapped to failure outcomes.</returns>
	private MountActionApplyResult ValidateMountReadiness(
		MountReconciliationAction action,
		MountActionApplyResult applyResult)
	{
		string normalizedMountPoint = Path.GetFullPath(action.MountPoint);
		MountSnapshot readinessSnapshot = _mountSnapshotService.Capture();
		MountSnapshotEntry? matchingEntry = FindSnapshotEntryForMountPoint(readinessSnapshot.Entries, normalizedMountPoint);
		if (matchingEntry is null)
		{
			string warningCode = readinessSnapshot.Warnings.Count > 0
				? readinessSnapshot.Warnings[0].Code
				: "none";
			return new MountActionApplyResult(
				action,
				MountActionApplyOutcome.Failure,
				$"Mount readiness check failed: no mount snapshot entry found for '{normalizedMountPoint}'. snapshot_entries='{readinessSnapshot.Entries.Count}' snapshot_warnings='{readinessSnapshot.Warnings.Count}' first_warning_code='{warningCode}'.");
		}

		if (!matchingEntry.FileSystemType.Contains("mergerfs", StringComparison.OrdinalIgnoreCase))
		{
			return new MountActionApplyResult(
				action,
				MountActionApplyOutcome.Failure,
				$"Mount readiness check failed: expected mergerfs filesystem type for '{normalizedMountPoint}' but observed '{matchingEntry.FileSystemType}'.");
		}

		if (!TryProbeMountPointAccessibility(normalizedMountPoint, out string probeFailure))
		{
			return new MountActionApplyResult(
				action,
				MountActionApplyOutcome.Failure,
				$"Mount readiness check failed: mountpoint probe failed for '{normalizedMountPoint}'. {probeFailure}");
		}

		return applyResult;
	}

	/// <summary>
	/// Locates one snapshot entry for the target mountpoint using normalized path equality.
	/// </summary>
	/// <param name="entries">Snapshot entries.</param>
	/// <param name="normalizedMountPoint">Normalized target mountpoint.</param>
	/// <returns>Matching entry when found; otherwise <see langword="null"/>.</returns>
	private static MountSnapshotEntry? FindSnapshotEntryForMountPoint(
		IReadOnlyList<MountSnapshotEntry> entries,
		string normalizedMountPoint)
	{
		for (int index = 0; index < entries.Count; index++)
		{
			MountSnapshotEntry entry = entries[index];
			string entryMountPoint = Path.GetFullPath(entry.MountPoint);
			if (string.Equals(entryMountPoint, normalizedMountPoint, _pathComparison))
			{
				return entry;
			}
		}

		return null;
	}

	/// <summary>
	/// Probes mountpoint accessibility with a bounded directory enumeration read.
	/// </summary>
	/// <param name="mountPoint">Mountpoint path.</param>
	/// <param name="failure">Failure diagnostic when probe fails.</param>
	/// <returns><see langword="true"/> when probe succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryProbeMountPointAccessibility(string mountPoint, out string failure)
	{
		if (!Directory.Exists(mountPoint))
		{
			failure = "Mountpoint directory does not exist.";
			return false;
		}

		try
		{
			string[] firstEntries = Directory.EnumerateFileSystemEntries(mountPoint).Take(1).ToArray();
			if (firstEntries.Length > 0)
			{
				_ = firstEntries[0].Length;
			}
			failure = string.Empty;
			return true;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
		{
			failure = $"{exception.GetType().Name}: {exception.Message}";
			return false;
		}
	}
}
