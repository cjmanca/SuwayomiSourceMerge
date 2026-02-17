using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Entrypoint FUSE-device access regression tests.
/// </summary>
public sealed partial class ContainerRuntimeEndToEndTests
{
	/// <summary>
	/// Verifies non-root runtime fails fast when the configured FUSE device path is not a character device.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldFailFast_WhenFuseDevicePathIsProcPseudoFile()
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
		Assert.Contains("'/proc/1/mem' is not a character device", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Configure FUSE_DEVICE_PATH", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies non-root runtime fails fast when <c>FUSE_DEVICE_PATH</c> exists but is not a character device.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldFailFast_WhenFuseDevicePathIsNotCharacterDevice()
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
			"FUSE_DEVICE_PATH=/etc/passwd",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"true"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("not a character device", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Configure FUSE_DEVICE_PATH", result.StandardError, StringComparison.Ordinal);
	}
}
