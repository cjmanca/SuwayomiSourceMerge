using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Entrypoint ownership safety regression tests.
/// </summary>
public sealed partial class ContainerRuntimeEndToEndTests
{
	/// <summary>
	/// Verifies merged-child ownership repair skips symlinked entries.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldSkipSymlinkedMergedChild_WhenRepairingOwnership()
	{
		using ContainerFixtureWorkspace workspace = new();
		Directory.CreateDirectory(Path.Combine(workspace.MergedRootPath, "Target"));
		const string symlinkTarget = "/ssm/merged/Target";
		string symlinkPath = Path.Combine(workspace.MergedRootPath, "EscapeLink");
		if (!TryCreateDirectorySymbolicLink(symlinkPath, symlinkTarget))
		{
			return;
		}

		if (!IsContainerPathSymlink(workspace, "/ssm/merged/EscapeLink"))
		{
			return;
		}

		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			"--volume",
			$"{workspace.MergedRootPath}:/ssm/merged",
			"--env",
			"PUID=99",
			"--env",
			"PGID=100",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"true"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
		Assert.Contains("Skipping symlinked merged child '/ssm/merged/EscapeLink'", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies entrypoint log ownership repair skips symlinked log files.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldSkipSymlinkedEntrypointLogPath_WhenRepairingLogOwnership()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.WriteConfigFile(
			"settings.yml",
			"""
			paths:
			  log_root_path: /ssm/config
			logging:
			  file_name: daemon.log
			""");

		File.WriteAllText(Path.Combine(workspace.ConfigRootPath, "daemon-target.log"), "seed");
		const string targetLogPath = "/ssm/config/daemon-target.log";
		string symlinkLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
		File.Delete(symlinkLogPath);
		if (!TryCreateFileSymbolicLink(symlinkLogPath, targetLogPath))
		{
			return;
		}

		if (!IsContainerPathSymlink(workspace, "/ssm/config/daemon.log"))
		{
			return;
		}

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["PUID"] = "99",
				["PGID"] = "100"
			});

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
		Assert.Contains("Skipping chown for symlinked entrypoint log path '/ssm/config/daemon.log'", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies startup ownership repair includes one-level child bind roots for sources and overrides.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldRepairOneLevelChildBindRootOwnership_ForSourcesAndOverrides()
	{
		using ContainerFixtureWorkspace workspace = new();
		Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "disk1"));
		Directory.CreateDirectory(Path.Combine(workspace.OverrideRootPath, "priority"));
		workspace.WriteMockToolScript(
			"chown",
			"""
			#!/usr/bin/env sh
			LOG_FILE="${MOCK_COMMAND_LOG_PATH:-/ssm/state/mock-commands.log}"
			printf "%s %s\n" "chown" "$*" >> "$LOG_FILE"
			exit 0
			""");

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["PUID"] = "99",
				["PGID"] = "100",
				["PATH"] = "/ssm/mock-bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
				["MOCK_COMMAND_LOG_PATH"] = "/ssm/state/mock-commands.log"
			});

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);

		string commandLogPath = Path.Combine(workspace.StateRootPath, "mock-commands.log");
		DockerAssertions.WaitForFileContains(commandLogPath, "/ssm/sources/disk1", TimeSpan.FromSeconds(30));
		DockerAssertions.WaitForFileContains(commandLogPath, "/ssm/override/priority", TimeSpan.FromSeconds(30));
	}

	/// <summary>
	/// Verifies entrypoint startup creates mover lock sentinel files under source and override bind children.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldCreateMoverLockSentinels_ForBindChildren()
	{
		const string lockDirectoryName = ".ssm-lock";
		const string lockSentinelFileName = ".nosync";

		using ContainerFixtureWorkspace workspace = new();
		Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "disk1"));
		Directory.CreateDirectory(Path.Combine(workspace.OverrideRootPath, "priority"));

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["PUID"] = "99",
				["PGID"] = "100"
			});

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);

		string sourceLockFilePath = Path.Combine(workspace.SourcesRootPath, "disk1", lockDirectoryName, lockSentinelFileName);
		string overrideLockFilePath = Path.Combine(workspace.OverrideRootPath, "priority", lockDirectoryName, lockSentinelFileName);
		Assert.True(File.Exists(sourceLockFilePath));
		Assert.True(File.Exists(overrideLockFilePath));
		Assert.Equal(0, new FileInfo(sourceLockFilePath).Length);
		Assert.Equal(0, new FileInfo(overrideLockFilePath).Length);
	}

	/// <summary>
	/// Verifies entrypoint startup normalizes existing lock sentinel files to empty files.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldNormalizeExistingMoverLockSentinelFileToEmpty_WhenPresent()
	{
		const string lockDirectoryName = ".ssm-lock";
		const string lockSentinelFileName = ".nosync";

		using ContainerFixtureWorkspace workspace = new();
		string sourceChildPath = Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "disk1")).FullName;
		string lockDirectoryPath = Directory.CreateDirectory(Path.Combine(sourceChildPath, lockDirectoryName)).FullName;
		string lockFilePath = Path.Combine(lockDirectoryPath, lockSentinelFileName);
		File.WriteAllText(lockFilePath, "non-empty");

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["PUID"] = "99",
				["PGID"] = "100"
			});

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
		Assert.Equal(0, new FileInfo(lockFilePath).Length);
	}

	/// <summary>
	/// Verifies lock-path type conflicts are reported as warnings and do not abort startup.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldWarnAndContinue_WhenMoverLockPathIsNotDirectory()
	{
		const string lockDirectoryName = ".ssm-lock";
		const string lockSentinelFileName = ".nosync";

		using ContainerFixtureWorkspace workspace = new();
		string sourceChildPath = Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "disk1")).FullName;
		File.WriteAllText(Path.Combine(sourceChildPath, lockDirectoryName), "invalid");
		Directory.CreateDirectory(Path.Combine(workspace.OverrideRootPath, "priority"));

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			new Dictionary<string, string>(StringComparer.Ordinal)
			{
				["PUID"] = "99",
				["PGID"] = "100"
			});

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
		Assert.Contains("exists but is not a directory; skipping lock sentinel setup", result.StandardError, StringComparison.Ordinal);
		Assert.True(
			File.Exists(Path.Combine(workspace.OverrideRootPath, "priority", lockDirectoryName, lockSentinelFileName)),
			"Expected unaffected bind child to receive lock sentinel setup.");
	}

	/// <summary>
	/// Attempts to create one directory symbolic link and returns false when host policy disallows it.
	/// </summary>
	/// <param name="symlinkPath">Symlink path.</param>
	/// <param name="targetPath">Target path.</param>
	/// <returns><see langword="true"/> when created; otherwise <see langword="false"/>.</returns>
	private static bool TryCreateDirectorySymbolicLink(string symlinkPath, string targetPath)
	{
		try
		{
			Directory.CreateSymbolicLink(symlinkPath, targetPath);
			return true;
		}
		catch (IOException exception) when (OperatingSystem.IsWindows() && exception.Message.Contains("required privilege", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
		{
			return false;
		}
		catch (PlatformNotSupportedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Attempts to create one file symbolic link and returns false when host policy disallows it.
	/// </summary>
	/// <param name="symlinkPath">Symlink path.</param>
	/// <param name="targetPath">Target path.</param>
	/// <returns><see langword="true"/> when created; otherwise <see langword="false"/>.</returns>
	private static bool TryCreateFileSymbolicLink(string symlinkPath, string targetPath)
	{
		try
		{
			File.CreateSymbolicLink(symlinkPath, targetPath);
			return true;
		}
		catch (IOException exception) when (OperatingSystem.IsWindows() && exception.Message.Contains("required privilege", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
		{
			return false;
		}
		catch (PlatformNotSupportedException)
		{
			return false;
		}
	}

	/// <summary>
	/// Verifies one bind-mounted path is visible as a symbolic link from inside a test container.
	/// </summary>
	/// <param name="workspace">Workspace providing bind mounts.</param>
	/// <param name="containerPath">Container path to probe.</param>
	/// <returns><see langword="true"/> when container reports a symlink; otherwise <see langword="false"/>.</returns>
	private bool IsContainerPathSymlink(ContainerFixtureWorkspace workspace, string containerPath)
	{
		ArgumentNullException.ThrowIfNull(workspace);
		ArgumentException.ThrowIfNullOrWhiteSpace(containerPath);

		List<string> arguments = ["run", "--rm"];
		foreach ((string hostPath, string bindContainerPath, bool readOnly) in CreateBindMounts(workspace))
		{
			string suffix = readOnly ? ":ro" : string.Empty;
			arguments.Add("--volume");
			arguments.Add($"{hostPath}:{bindContainerPath}{suffix}");
		}

		arguments.Add(_fixture.ImageTag);
		arguments.Add("bash");
		arguments.Add("-lc");
		arguments.Add($"test -L '{containerPath}'");

		DockerCommandResult result = _fixture.Runner.Execute(arguments, timeout: TimeSpan.FromMinutes(2));
		return !result.TimedOut && result.ExitCode == 0;
	}
}
