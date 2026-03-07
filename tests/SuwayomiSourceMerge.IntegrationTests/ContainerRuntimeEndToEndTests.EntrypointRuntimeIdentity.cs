using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Entrypoint runtime-identity behavior regression tests.
/// </summary>
public sealed partial class ContainerRuntimeEndToEndTests
{
	/// <summary>
	/// Verifies non-root startup bypasses root-only identity setup and command handoff.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldBypassRootOnlyEntrypointSetup_WhenContainerStartsAsNonRoot()
	{
		using ContainerFixtureWorkspace workspace = new();
		string hostManagedFuseConfigPath = Path.Combine(workspace.StateRootPath, "fuse.conf");
		File.WriteAllText(hostManagedFuseConfigPath, "user_allow_other\n");
		EnsureNonRootContainerCanAccessWorkspaceDirectories(workspace);
		EnsureNonRootContainerCanReadFuseConfig(workspace.StateRootPath, hostManagedFuseConfigPath);

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
			"FUSE_DEVICE_PATH=/dev/null",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"true"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
		Assert.DoesNotContain("useradd:", result.StandardError, StringComparison.Ordinal);
		Assert.DoesNotContain("groupadd:", result.StandardError, StringComparison.Ordinal);
		Assert.DoesNotContain("gosu:", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Non-root startup detected", result.StandardError, StringComparison.Ordinal);
		Assert.DoesNotContain("differs from configured PUID/PGID", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies non-root startup bypasses root-only setup even when PUID/PGID differ from process identity.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldContinueWithWarning_WhenContainerStartsAsNonRootAndPuidPgidDiffer()
	{
		using ContainerFixtureWorkspace workspace = new();
		string hostManagedFuseConfigPath = Path.Combine(workspace.StateRootPath, "fuse.conf");
		File.WriteAllText(hostManagedFuseConfigPath, "user_allow_other\n");
		EnsureNonRootContainerCanAccessWorkspaceDirectories(workspace);
		EnsureNonRootContainerCanReadFuseConfig(workspace.StateRootPath, hostManagedFuseConfigPath);

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
			"PUID=1001",
			"--env",
			"PGID=100",
			"--env",
			"ENTRYPOINT_FUSE_CONF_MODE=host-managed",
			"--env",
			"FUSE_CONF_PATH=/ssm/state/fuse.conf",
			"--env",
			"FUSE_DEVICE_PATH=/dev/null",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"true"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
		Assert.Contains("Non-root startup detected", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("PUID/PGID identity remapping is skipped", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("differs from configured PUID/PGID", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Ensures non-root container runtime users can read one host-managed fuse configuration bind mount.
	/// </summary>
	/// <param name="stateRootPath">State bind root path.</param>
	/// <param name="fuseConfigPath">Fuse config path.</param>
	private static void EnsureNonRootContainerCanReadFuseConfig(string stateRootPath, string fuseConfigPath)
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}

		File.SetUnixFileMode(
			stateRootPath,
			UnixFileMode.UserRead |
			UnixFileMode.UserWrite |
			UnixFileMode.UserExecute |
			UnixFileMode.GroupRead |
			UnixFileMode.GroupExecute |
			UnixFileMode.OtherRead |
			UnixFileMode.OtherExecute);

		File.SetUnixFileMode(
			fuseConfigPath,
			UnixFileMode.UserRead |
			UnixFileMode.UserWrite |
			UnixFileMode.GroupRead |
			UnixFileMode.OtherRead);
	}

	/// <summary>
	/// Ensures bind-mounted workspace directories are writable by non-root container users.
	/// </summary>
	/// <param name="workspace">Workspace.</param>
	private static void EnsureNonRootContainerCanAccessWorkspaceDirectories(ContainerFixtureWorkspace workspace)
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}

		ArgumentNullException.ThrowIfNull(workspace);
		string[] writableDirectories =
		[
			workspace.ConfigRootPath,
			workspace.SourcesRootPath,
			workspace.OverrideRootPath,
			workspace.MergedRootPath,
			workspace.StateRootPath
		];

		foreach (string directoryPath in writableDirectories)
		{
			File.SetUnixFileMode(
				directoryPath,
				UnixFileMode.UserRead |
				UnixFileMode.UserWrite |
				UnixFileMode.UserExecute |
				UnixFileMode.GroupRead |
				UnixFileMode.GroupWrite |
				UnixFileMode.GroupExecute |
				UnixFileMode.OtherRead |
				UnixFileMode.OtherWrite |
				UnixFileMode.OtherExecute);
		}
	}
}
