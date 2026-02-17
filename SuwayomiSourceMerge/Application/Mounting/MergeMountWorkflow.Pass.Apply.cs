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
}
