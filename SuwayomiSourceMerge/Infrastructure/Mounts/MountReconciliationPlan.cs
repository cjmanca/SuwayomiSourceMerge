namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents ordered reconciliation actions computed for one pass.
/// </summary>
internal sealed class MountReconciliationPlan
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MountReconciliationPlan"/> class.
	/// </summary>
	/// <param name="actions">Ordered reconciliation actions.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="actions"/> is <see langword="null"/>.</exception>
	public MountReconciliationPlan(IReadOnlyList<MountReconciliationAction> actions)
	{
		ArgumentNullException.ThrowIfNull(actions);

		Actions = actions.ToArray();
	}

	/// <summary>
	/// Gets the ordered reconciliation actions.
	/// </summary>
	public IReadOnlyList<MountReconciliationAction> Actions
	{
		get;
	}
}
