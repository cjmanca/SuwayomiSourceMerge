using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Entrypoint FUSE-device access regression tests.
/// </summary>
public sealed partial class ContainerRuntimeEndToEndTests
{
	/// <summary>
	/// Verifies non-root runtime fails fast with actionable diagnostics when the configured FUSE device path is inaccessible.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldFailFast_WhenNonRootCannotAccessFuseDevicePath()
	{
		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			"--env",
			"PUID=99",
			"--env",
			"PGID=100",
			"--env",
			"FUSE_DEVICE_PATH=/proc/1/mem",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"true"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("cannot read/write '/proc/1/mem'", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Operation not permitted", result.StandardError, StringComparison.Ordinal);
	}
}
