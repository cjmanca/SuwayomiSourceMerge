namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Computes deterministic reconciliation actions from desired and actual mount state.
/// </summary>
internal interface IMountReconciliationService
{
	/// <summary>
	/// Reconciles desired and actual mount state.
	/// </summary>
	/// <param name="input">Desired and actual mount state input model.</param>
	/// <returns>Deterministic reconciliation action plan.</returns>
	MountReconciliationPlan Reconcile(MountReconciliationInput input);
}
