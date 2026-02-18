namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Readiness probe coverage for <see cref="MergerfsMountCommandService"/>.
/// </summary>
public sealed partial class MergerfsMountCommandServiceTests
{
	/// <summary>
	/// Verifies readiness probes return ready when probe command succeeds.
	/// </summary>
	[Fact]
	public void ProbeMountPointReadiness_Expected_ShouldReturnReady_WhenProbeCommandSucceeds()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);

		MountReadinessProbeResult result = service.ProbeMountPointReadiness(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10));

		Assert.True(result.IsReady);
		Assert.Single(executor.Requests);
		Assert.Equal("ls", executor.Requests[0].FileName);
		Assert.Equal("-A", executor.Requests[0].Arguments[0]);
	}

	/// <summary>
	/// Verifies timeout probe outcomes are classified as not ready.
	/// </summary>
	[Fact]
	public void ProbeMountPointReadiness_Edge_ShouldReturnNotReady_WhenProbeCommandTimesOut()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.TimedOut,
				ExternalCommandFailureKind.None,
				null,
				string.Empty,
				string.Empty,
				true,
				false,
				TimeSpan.FromSeconds(5)));
		MergerfsMountCommandService service = new(executor);

		MountReadinessProbeResult result = service.ProbeMountPointReadiness(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10));

		Assert.False(result.IsReady);
		Assert.Contains("timed out", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Verifies non-zero probe exits are classified as not ready with stderr diagnostics.
	/// </summary>
	[Fact]
	public void ProbeMountPointReadiness_Failure_ShouldReturnNotReady_WhenProbeCommandExitsNonZero()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				1,
				string.Empty,
				"Transport endpoint is not connected",
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);

		MountReadinessProbeResult result = service.ProbeMountPointReadiness(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10));

		Assert.False(result.IsReady);
		Assert.Contains("Transport endpoint is not connected", result.Diagnostic, StringComparison.Ordinal);
	}
}
