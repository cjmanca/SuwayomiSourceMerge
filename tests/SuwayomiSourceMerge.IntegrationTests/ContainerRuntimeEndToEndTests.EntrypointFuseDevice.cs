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

	/// <summary>
	/// Verifies non-root runtime FUSE permission failures include actionable remediation guidance.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldReportActionableGuidance_WhenNonRootCannotAccessCharacterFuseDevice()
	{
		string imageTag = BuildDerivedNonWritableFuseImageTag();
		using ContainerFixtureWorkspace workspace = new();
		string hostManagedFuseConfigPath = Path.Combine(workspace.StateRootPath, "fuse.conf");
		File.WriteAllText(hostManagedFuseConfigPath, "user_allow_other\n");
		EnsureNonRootContainerCanAccessWorkspaceDirectories(workspace);
		EnsureNonRootContainerCanReadFuseConfig(workspace.StateRootPath, hostManagedFuseConfigPath);

		try
		{
			BuildDerivedNonWritableFuseImage(imageTag);

			DockerCommandResult result = _fixture.Runner.Execute(
			[
				"run",
				"--rm",
				"--user",
				"99:100",
				"--volume",
				$"{workspace.ConfigRootPath}:/ssm/config",
				"--volume",
				$"{workspace.SourcesRootPath}:/ssm/sources",
				"--volume",
				$"{workspace.OverrideRootPath}:/ssm/override",
				"--volume",
				$"{workspace.MergedRootPath}:/ssm/merged",
				"--volume",
				$"{workspace.StateRootPath}:/ssm/state",
				"--env",
				"PUID=99",
				"--env",
				"PGID=100",
				"--env",
				"ENTRYPOINT_FUSE_CONF_MODE=host-managed",
				"--env",
				"FUSE_CONF_PATH=/ssm/state/fuse.conf",
				"--env",
				"FUSE_DEVICE_PATH=/tmp/nonwritable-fuse",
				imageTag,
				"bash",
				"-lc",
				"true"
			],
			timeout: TimeSpan.FromMinutes(2));

			Assert.False(result.TimedOut);
			Assert.NotEqual(0, result.ExitCode);
			Assert.Contains("cannot read/write '/tmp/nonwritable-fuse'", result.StandardError, StringComparison.Ordinal);
			Assert.Contains("Fix access by mapping '/tmp/nonwritable-fuse' into the container", result.StandardError, StringComparison.Ordinal);
			Assert.Contains("--cap-add SYS_ADMIN", result.StandardError, StringComparison.Ordinal);
		}
		finally
		{
			RemoveImageBestEffort(imageTag);
		}
	}

	/// <summary>
	/// Builds a unique derived image tag for non-root FUSE permission denial scenarios.
	/// </summary>
	/// <returns>Derived image tag.</returns>
	private static string BuildDerivedNonWritableFuseImageTag()
	{
		return $"ssm-integration-fuse-denied:{Guid.NewGuid():N}";
	}

	/// <summary>
	/// Builds one derived image containing a non-writable character device for deterministic access-failure assertions.
	/// </summary>
	/// <param name="imageTag">Derived image tag.</param>
	private void BuildDerivedNonWritableFuseImage(string imageTag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(imageTag);

		string buildContextPath = Path.Combine(Path.GetTempPath(), $"ssm-integration-fuse-denied-image-{Guid.NewGuid():N}");
		Directory.CreateDirectory(buildContextPath);

		try
		{
			string dockerfilePath = Path.Combine(buildContextPath, "Dockerfile");
			string dockerfile = $$"""
			FROM {{_fixture.ImageTag}}
			RUN mknod /tmp/nonwritable-fuse c 1 3 && chmod 000 /tmp/nonwritable-fuse
			""";
			File.WriteAllText(dockerfilePath, dockerfile);

			DockerCommandResult buildResult = _fixture.Runner.Execute(
				["build", "--tag", imageTag, "--file", dockerfilePath, buildContextPath],
				timeout: TimeSpan.FromMinutes(3));

			if (buildResult.TimedOut || buildResult.ExitCode != 0)
			{
				throw new InvalidOperationException(
					$"Failed to build entrypoint FUSE-denied image '{imageTag}'.{Environment.NewLine}" +
					$"Command: {buildResult.Command}{Environment.NewLine}" +
					$"TimedOut: {buildResult.TimedOut}{Environment.NewLine}" +
					$"ExitCode: {buildResult.ExitCode}{Environment.NewLine}" +
					$"Stdout:{Environment.NewLine}{buildResult.StandardOutput}{Environment.NewLine}" +
					$"Stderr:{Environment.NewLine}{buildResult.StandardError}");
			}
		}
		finally
		{
			try
			{
				if (Directory.Exists(buildContextPath))
				{
					Directory.Delete(buildContextPath, recursive: true);
				}
			}
			catch
			{
				// Best-effort temp-build-context cleanup.
			}
		}
	}
}
