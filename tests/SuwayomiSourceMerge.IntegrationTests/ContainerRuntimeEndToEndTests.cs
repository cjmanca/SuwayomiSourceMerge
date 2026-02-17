using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// End-to-end Docker runtime tests for container assets and daemon orchestration.
/// </summary>
[Collection(DockerIntegrationFixture.CollectionName)]
public sealed class ContainerRuntimeEndToEndTests
{
	/// <summary>
	/// Shared Docker fixture.
	/// </summary>
	private readonly DockerIntegrationFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="ContainerRuntimeEndToEndTests"/> class.
	/// </summary>
	/// <param name="fixture">Shared Docker fixture.</param>
	public ContainerRuntimeEndToEndTests(DockerIntegrationFixture fixture)
	{
		_fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
	}

	/// <summary>
	/// Verifies healthy container startup, production merge dispatch, and graceful stop cleanup.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldBootstrapDispatchAndStopGracefully()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.CreateMockToolScripts();
		Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "disk1", "SourceA", "MangaA"));
		Directory.CreateDirectory(Path.Combine(workspace.OverrideRootPath, "priority", "MangaA"));
		workspace.WriteSettingsFile("/ssm/sources", "/ssm/override", "debug");

		string containerName = BuildContainerName("expected");
		try
		{
			string findmntOutput = "TARGET=\"/ssm/merged/MangaA\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"mock\" OPTIONS=\"rw\"\\n";
			_fixture.Runner.RunContainerDetached(
				_fixture.ImageTag,
				containerName,
				CreateEnvironmentVariables(
					"/ssm/override/priority/MangaA/details.json|CLOSE_WRITE\n",
					findmntOutput),
				CreateBindMounts(workspace));

			string daemonLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
			string commandLogPath = Path.Combine(workspace.StateRootPath, "mock-commands.log");
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"host.startup\"", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"watcher.tick.summary\"", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"merge.dispatch.completed\"", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForFileContains(commandLogPath, "mergerfs ", TimeSpan.FromSeconds(60));
			string startupLogs = _fixture.Runner.GetContainerLogs(containerName);
			Assert.DoesNotContain("already maps to group 'users' (expected 'ssm')", startupLogs, StringComparison.Ordinal);
			Assert.DoesNotContain("uid 99 outside of the UID_MIN", startupLogs, StringComparison.Ordinal);

			int unmountCommandCountBeforeStop = CountUnmountCommandLines(commandLogPath);

			Assert.True(File.Exists(Path.Combine(workspace.ConfigRootPath, "manga_equivalents.yml")));
			Assert.True(File.Exists(Path.Combine(workspace.ConfigRootPath, "scene_tags.yml")));
			Assert.True(File.Exists(Path.Combine(workspace.ConfigRootPath, "source_priority.yml")));

			_fixture.Runner.SendSignal(containerName, "SIGINT");
			int exitCode = _fixture.Runner.WaitContainerExitCode(containerName, TimeSpan.FromSeconds(30));
			Assert.Equal(0, exitCode);

			DockerAssertions.WaitForCondition(
				() => CountUnmountCommandLines(commandLogPath) > unmountCommandCountBeforeStop,
				TimeSpan.FromSeconds(30),
				"Expected shutdown sweep to invoke at least one additional unmount command.");
		}
		finally
		{
			_fixture.Runner.RemoveContainerForce(containerName);
		}
	}

	/// <summary>
	/// Verifies missing watch-root paths produce warnings without crashing daemon operation.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldWarnAndRemainRunning_WhenConfiguredWatchRootsAreMissing()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.CreateMockToolScripts();
		workspace.WriteSettingsFile("/ssm/missing-sources", "/ssm/missing-override", "debug");

		string containerName = BuildContainerName("edge-missing-roots");
		try
		{
			_fixture.Runner.RunContainerDetached(
				_fixture.ImageTag,
				containerName,
				CreateEnvironmentVariables(inotifyOutput: string.Empty),
				CreateBindMounts(workspace));

			string daemonLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
			DockerAssertions.WaitForFileContains(daemonLogPath, "Skipping missing watch root", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForCondition(
				() => _fixture.Runner.IsContainerRunning(containerName),
				TimeSpan.FromSeconds(30),
				"Container was expected to keep running for missing watch-root edge case.");

			_fixture.Runner.SendSignal(containerName, "SIGINT");
			int exitCode = _fixture.Runner.WaitContainerExitCode(containerName, TimeSpan.FromSeconds(30));
			Assert.Equal(0, exitCode);
		}
		finally
		{
			_fixture.Runner.RemoveContainerForce(containerName);
		}
	}

	/// <summary>
	/// Verifies invalid settings fail startup with deterministic validation error diagnostics.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldExitNonZero_WhenSettingsValidationFails()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.CreateMockToolScripts();
		workspace.WriteSettingsFile("/ssm/sources", "/ssm/override", "invalid-level");

		string containerName = BuildContainerName("failure-invalid-settings");
		try
		{
			_fixture.Runner.RunContainerDetached(
				_fixture.ImageTag,
				containerName,
				CreateEnvironmentVariables(inotifyOutput: string.Empty),
				CreateBindMounts(workspace));

			int exitCode = _fixture.Runner.WaitContainerExitCode(containerName, TimeSpan.FromSeconds(60));
			string logs = _fixture.Runner.GetContainerLogs(containerName);

			Assert.NotEqual(0, exitCode);
			Assert.Contains("Configuration bootstrap failed.", logs, StringComparison.Ordinal);
			Assert.Contains("CFG-SET-005", logs, StringComparison.Ordinal);
		}
		finally
		{
			_fixture.Runner.RemoveContainerForce(containerName);
		}
	}

	/// <summary>
	/// Verifies entrypoint falls back to deterministic user/group names when default names collide on different IDs.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldStartWithFallbackIdentityNames_WhenDefaultNamesCollide()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.CreateMockToolScripts();
		Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "disk1", "SourceA", "MangaA"));
		Directory.CreateDirectory(Path.Combine(workspace.OverrideRootPath, "priority", "MangaA"));
		workspace.WriteSettingsFile("/ssm/sources", "/ssm/override", "debug");

		const string collidingSsmGroupId = "59980";
		const string collidingSsmUserId = "59981";
		const string requestedGroupId = "59990";
		const string requestedUserId = "59991";

		string collisionImageTag = BuildDerivedIdentityCollisionImageTag();
		string containerName = BuildContainerName("edge-name-collision");
		try
		{
			BuildDerivedIdentityCollisionImage(
				collisionImageTag,
				collidingSsmGroupId,
				collidingSsmUserId);

			_fixture.Runner.RunContainerDetached(
				collisionImageTag,
				containerName,
				CreateEnvironmentVariables(
					"/ssm/override/priority/MangaA/details.json|CLOSE_WRITE\n",
					puid: requestedUserId,
					pgid: requestedGroupId),
				CreateBindMounts(workspace));

			string daemonLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"host.startup\"", TimeSpan.FromSeconds(60));

			string logs = _fixture.Runner.GetContainerLogs(containerName);
			Assert.Contains("using fallback group name", logs, StringComparison.Ordinal);
			Assert.Contains("using fallback user name", logs, StringComparison.Ordinal);

			_fixture.Runner.SendSignal(containerName, "SIGINT");
			int exitCode = _fixture.Runner.WaitContainerExitCode(containerName, TimeSpan.FromSeconds(30));
			Assert.Equal(0, exitCode);
		}
		finally
		{
			_fixture.Runner.RemoveContainerForce(containerName);
			RemoveImageBestEffort(collisionImageTag);
		}
	}

	/// <summary>
	/// Builds deterministic environment variables for container execution.
	/// </summary>
	/// <param name="inotifyOutput">Mock inotify output payload.</param>
	/// <returns>Environment variable map.</returns>
	private static IReadOnlyDictionary<string, string> CreateEnvironmentVariables(
		string inotifyOutput,
		string findmntOutput = "",
		string puid = "99",
		string pgid = "100")
	{
		ArgumentNullException.ThrowIfNull(inotifyOutput);
		ArgumentNullException.ThrowIfNull(findmntOutput);
		ArgumentException.ThrowIfNullOrWhiteSpace(puid);
		ArgumentException.ThrowIfNullOrWhiteSpace(pgid);

		return new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["PUID"] = puid,
			["PGID"] = pgid,
			["PATH"] = "/ssm/mock-bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
			["INOTIFYWAIT_STDOUT"] = inotifyOutput,
			["FINDMNT_STDOUT"] = findmntOutput
		};
	}

	/// <summary>
	/// Builds bind-mount tuples for one workspace.
	/// </summary>
	/// <param name="workspace">Workspace.</param>
	/// <returns>Bind mount list.</returns>
	private static IReadOnlyList<(string HostPath, string ContainerPath, bool ReadOnly)> CreateBindMounts(ContainerFixtureWorkspace workspace)
	{
		return
		[
			(workspace.ConfigRootPath, "/ssm/config", false),
			(workspace.SourcesRootPath, "/ssm/sources", false),
			(workspace.OverrideRootPath, "/ssm/override", false),
			(workspace.MergedRootPath, "/ssm/merged", false),
			(workspace.StateRootPath, "/ssm/state", false),
			(workspace.MockBinPath, "/ssm/mock-bin", true)
		];
	}

	/// <summary>
	/// Builds a unique container name for one test case.
	/// </summary>
	/// <param name="prefix">Name prefix.</param>
	/// <returns>Container name.</returns>
	private static string BuildContainerName(string prefix)
	{
		return $"ssm-{prefix}-{Guid.NewGuid():N}".ToLowerInvariant();
	}

	/// <summary>
	/// Builds a unique derived image tag for entrypoint identity-collision scenarios.
	/// </summary>
	/// <returns>Derived image tag.</returns>
	private static string BuildDerivedIdentityCollisionImageTag()
	{
		return $"ssm-integration-collision:{Guid.NewGuid():N}";
	}

	/// <summary>
	/// Builds one derived image that forces pre-existing <c>ssm</c> user/group names on different IDs.
	/// </summary>
	/// <param name="imageTag">Derived image tag.</param>
	/// <param name="groupId">Conflicting <c>ssm</c> group id.</param>
	/// <param name="userId">Conflicting <c>ssm</c> user id.</param>
	private void BuildDerivedIdentityCollisionImage(string imageTag, string groupId, string userId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(imageTag);
		ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
		ArgumentException.ThrowIfNullOrWhiteSpace(userId);

		string buildContextPath = Path.Combine(Path.GetTempPath(), $"ssm-integration-collision-image-{Guid.NewGuid():N}");
		Directory.CreateDirectory(buildContextPath);

		try
		{
			string dockerfilePath = Path.Combine(buildContextPath, "Dockerfile");
			string dockerfile = $$"""
			FROM {{_fixture.ImageTag}}
			RUN if ! getent group ssm >/dev/null 2>&1; then \
			      groupadd --gid {{groupId}} ssm; \
			    else \
			      groupmod --gid {{groupId}} ssm; \
			    fi \
			    && if ! id -u ssm >/dev/null 2>&1; then \
			      useradd --uid {{userId}} --gid {{groupId}} --no-create-home --shell /usr/sbin/nologin ssm; \
			    else \
			      usermod --uid {{userId}} --gid {{groupId}} ssm; \
			    fi
			""";
			File.WriteAllText(dockerfilePath, dockerfile);

			DockerCommandResult buildResult = _fixture.Runner.Execute(
				["build", "--tag", imageTag, "--file", dockerfilePath, buildContextPath],
				timeout: TimeSpan.FromMinutes(3));

			if (buildResult.TimedOut || buildResult.ExitCode != 0)
			{
				throw new InvalidOperationException(
					$"Failed to build entrypoint collision image '{imageTag}'.{Environment.NewLine}" +
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

	/// <summary>
	/// Removes one image tag using best-effort semantics.
	/// </summary>
	/// <param name="imageTag">Image tag.</param>
	private void RemoveImageBestEffort(string imageTag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(imageTag);

		try
		{
			_fixture.Runner.Execute(["rmi", "--force", imageTag], timeout: TimeSpan.FromSeconds(30));
		}
		catch
		{
			// Best-effort image cleanup.
		}
	}

	/// <summary>
	/// Counts recorded unmount command invocations in the mock command log.
	/// </summary>
	/// <param name="commandLogPath">Mock command log path.</param>
	/// <returns>Unmount command line count.</returns>
	private static int CountUnmountCommandLines(string commandLogPath)
	{
		return DockerAssertions.CountFileLinesMatching(
			commandLogPath,
			static line => line.StartsWith("fusermount3 ", StringComparison.Ordinal) ||
				line.StartsWith("fusermount ", StringComparison.Ordinal) ||
				line.StartsWith("umount ", StringComparison.Ordinal),
			TimeSpan.FromSeconds(5),
			$"Timed out reading unmount commands from '{commandLogPath}'.");
	}
}
