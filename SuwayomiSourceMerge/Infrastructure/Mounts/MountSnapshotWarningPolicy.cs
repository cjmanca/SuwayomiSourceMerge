namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Canonical warning codes emitted by mount snapshot providers.
/// </summary>
internal static class MountSnapshotWarningCodes
{
	/// <summary>
	/// Warning code emitted when snapshot command execution fails.
	/// </summary>
	public const string CommandFailure = "MOUNT-SNAP-001";

	/// <summary>
	/// Warning code emitted when one snapshot output line cannot be parsed safely.
	/// </summary>
	public const string ParseFailure = "MOUNT-SNAP-002";
}

/// <summary>
/// Resolves severity for mount snapshot warning codes.
/// </summary>
internal static class MountSnapshotWarningPolicy
{
	/// <summary>
	/// Creates one mount snapshot warning using canonical severity mapping.
	/// </summary>
	/// <param name="code">Warning code.</param>
	/// <param name="message">Warning message.</param>
	/// <returns>Warning with policy-resolved severity.</returns>
	public static MountSnapshotWarning Create(string code, string message)
	{
		return new MountSnapshotWarning(code, message, ResolveSeverity(code));
	}

	/// <summary>
	/// Resolves severity for one warning code.
	/// </summary>
	/// <param name="code">Warning code.</param>
	/// <returns>Severity classification.</returns>
	public static MountSnapshotWarningSeverity ResolveSeverity(string code)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(code);

		return code switch
		{
			MountSnapshotWarningCodes.CommandFailure => MountSnapshotWarningSeverity.DegradedVisibility,
			MountSnapshotWarningCodes.ParseFailure => MountSnapshotWarningSeverity.DegradedVisibility,
			_ => MountSnapshotWarningSeverity.NonFatal
		};
	}
}
