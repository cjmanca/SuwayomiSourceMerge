namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Verifies severity mapping policy for mount snapshot warnings.
/// </summary>
public sealed class MountSnapshotWarningPolicyTests
{
	/// <summary>
	/// Verifies known warning codes map to degraded-visibility severity.
	/// </summary>
	[Fact]
	public void ResolveSeverity_Expected_ShouldReturnDegradedVisibility_ForKnownCodes()
	{
		Assert.Equal(
			MountSnapshotWarningSeverity.DegradedVisibility,
			MountSnapshotWarningPolicy.ResolveSeverity(MountSnapshotWarningCodes.CommandFailure));
		Assert.Equal(
			MountSnapshotWarningSeverity.DegradedVisibility,
			MountSnapshotWarningPolicy.ResolveSeverity(MountSnapshotWarningCodes.ParseFailure));
	}

	/// <summary>
	/// Verifies unknown warning codes map to non-fatal severity.
	/// </summary>
	[Fact]
	public void ResolveSeverity_Edge_ShouldReturnNonFatal_ForUnknownCode()
	{
		Assert.Equal(
			MountSnapshotWarningSeverity.NonFatal,
			MountSnapshotWarningPolicy.ResolveSeverity("MOUNT-SNAP-999"));
	}

	/// <summary>
	/// Verifies helper creation applies resolved severity to warning instances.
	/// </summary>
	[Fact]
	public void Create_Expected_ShouldApplyPolicySeverity()
	{
		MountSnapshotWarning warning = MountSnapshotWarningPolicy.Create(
			MountSnapshotWarningCodes.ParseFailure,
			"Malformed line.");

		Assert.Equal(MountSnapshotWarningCodes.ParseFailure, warning.Code);
		Assert.Equal(MountSnapshotWarningSeverity.DegradedVisibility, warning.Severity);
	}
}
