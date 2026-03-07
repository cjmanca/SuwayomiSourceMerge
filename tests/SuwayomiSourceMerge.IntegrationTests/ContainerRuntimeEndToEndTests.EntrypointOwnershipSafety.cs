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
