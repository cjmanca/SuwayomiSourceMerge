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
		string symlinkTarget = Directory.CreateDirectory(Path.Combine(workspace.MergedRootPath, "Target")).FullName;
		string symlinkPath = Path.Combine(workspace.MergedRootPath, "EscapeLink");
		if (!TryCreateDirectorySymbolicLink(symlinkPath, symlinkTarget))
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

		string targetLogPath = Path.Combine(workspace.ConfigRootPath, "daemon-target.log");
		File.WriteAllText(targetLogPath, "seed");
		string symlinkLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
		File.Delete(symlinkLogPath);
		if (!TryCreateFileSymbolicLink(symlinkLogPath, targetLogPath))
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
}
