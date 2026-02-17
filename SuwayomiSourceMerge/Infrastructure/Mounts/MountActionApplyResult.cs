namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents the outcome classification for one applied mount reconciliation action.
/// </summary>
internal sealed class MountActionApplyResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MountActionApplyResult"/> class.
	/// </summary>
	/// <param name="action">Reconciliation action that was applied.</param>
	/// <param name="outcome">Outcome classification.</param>
	/// <param name="diagnostic">Diagnostic message describing the apply result.</param>
	public MountActionApplyResult(
		MountReconciliationAction action,
		MountActionApplyOutcome outcome,
		string diagnostic)
	{
		Action = action ?? throw new ArgumentNullException(nameof(action));
		ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);

		Outcome = outcome;
		Diagnostic = diagnostic;
	}

	/// <summary>
	/// Gets the reconciliation action that was applied.
	/// </summary>
	public MountReconciliationAction Action
	{
		get;
	}

	/// <summary>
	/// Gets the outcome classification.
	/// </summary>
	public MountActionApplyOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets a diagnostic message describing the result.
	/// </summary>
	public string Diagnostic
	{
		get;
	}
}

/// <summary>
/// Classifies one mount-action apply result.
/// </summary>
internal enum MountActionApplyOutcome
{
	/// <summary>
	/// Action completed successfully.
	/// </summary>
	Success,

	/// <summary>
	/// Action failed because target state was busy or temporarily unavailable.
	/// </summary>
	Busy,

	/// <summary>
	/// Action failed due to a non-busy command or environment failure.
	/// </summary>
	Failure
}
