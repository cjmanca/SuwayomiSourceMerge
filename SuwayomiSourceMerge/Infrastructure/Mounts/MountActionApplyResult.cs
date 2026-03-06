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
	/// <param name="failureSeverity">
	/// Optional failure-severity classification. When omitted, failures default to <see cref="MountActionFailureSeverity.Hard"/> and
	/// non-failures default to <see cref="MountActionFailureSeverity.None"/>.
	/// </param>
	public MountActionApplyResult(
		MountReconciliationAction action,
		MountActionApplyOutcome outcome,
		string diagnostic,
		MountActionFailureSeverity? failureSeverity = null)
	{
		Action = action ?? throw new ArgumentNullException(nameof(action));
		ArgumentException.ThrowIfNullOrWhiteSpace(diagnostic);

		Outcome = outcome;
		Diagnostic = diagnostic;
		FailureSeverity = ResolveFailureSeverity(outcome, failureSeverity);
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

	/// <summary>
	/// Gets the failure-severity classification for this apply result.
	/// </summary>
	public MountActionFailureSeverity FailureSeverity
	{
		get;
	}

	/// <summary>
	/// Resolves one normalized failure-severity value from outcome and optional explicit severity input.
	/// </summary>
	/// <param name="outcome">Apply outcome.</param>
	/// <param name="failureSeverity">Optional explicit failure-severity value.</param>
	/// <returns>Normalized failure-severity value.</returns>
	private static MountActionFailureSeverity ResolveFailureSeverity(
		MountActionApplyOutcome outcome,
		MountActionFailureSeverity? failureSeverity)
	{
		if (!failureSeverity.HasValue)
		{
			return outcome == MountActionApplyOutcome.Failure
				? MountActionFailureSeverity.Hard
				: MountActionFailureSeverity.None;
		}

		if (!Enum.IsDefined(failureSeverity.Value))
		{
			throw new ArgumentOutOfRangeException(nameof(failureSeverity), failureSeverity, "Failure severity must be a defined value.");
		}

		if (outcome != MountActionApplyOutcome.Failure)
		{
			return MountActionFailureSeverity.None;
		}

		return failureSeverity.Value == MountActionFailureSeverity.None
			? MountActionFailureSeverity.Hard
			: failureSeverity.Value;
	}
}

/// <summary>
/// Classifies mount-action failure severity for fail-fast counters and diagnostics.
/// </summary>
internal enum MountActionFailureSeverity
{
	/// <summary>
	/// No failure occurred.
	/// </summary>
	None,

	/// <summary>
	/// Failure is recoverable and should not contribute to hard-failure fail-fast counting.
	/// </summary>
	Soft,

	/// <summary>
	/// Failure is non-recoverable and should contribute to hard-failure fail-fast counting.
	/// </summary>
	Hard
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
