using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// End-to-end Docker runtime tests for container assets and daemon orchestration.
/// </summary>
[Collection(DockerIntegrationFixture.CollectionName)]
public sealed partial class ContainerRuntimeEndToEndTests
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
			DockerAssertions.WaitForFileContains(daemonLogPath, "scan.merge_trigger_request_timeout_buffer_seconds", TimeSpan.FromSeconds(60));
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
			DockerAssertions.WaitForFileContains(daemonLogPath, "using fallback group name", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForFileContains(daemonLogPath, "using fallback user name", TimeSpan.FromSeconds(60));

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
	/// Verifies the entrypoint enables <c>user_allow_other</c> so non-root mergerfs mounts can use allow_other.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldEnableUserAllowOtherInFuseConfig_BeforeDroppingPrivileges()
	{
		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			"--env",
			"PUID=99",
			"--env",
			"PGID=100",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"grep -Eq '^[[:space:]]*user_allow_other([[:space:]]*#.*)?$' /etc/fuse.conf"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
	}

	/// <summary>
	/// Verifies root runtime skips fuse.conf edit attempts.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldSkipFuseConfigEdit_WhenPuidIsRoot()
	{
		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			"--env",
			"PUID=0",
			"--env",
			"PGID=0",
			"--env",
			"FUSE_CONF_PATH=/proc/1/mem",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"true"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
		Assert.DoesNotContain("Failed to update '/proc/1/mem'", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies non-root runtime fails fast with remediation guidance when fuse.conf cannot be edited.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldFailWithGuidance_WhenNonRootCannotEditFuseConfig()
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
			"FUSE_CONF_PATH=/proc/1/mem",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"true"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("Failed to update '/proc/1/mem' with 'user_allow_other'", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Root cause detail:", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Manual edit", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("sh -c", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("PUID=0", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies entrypoint ownership setup does not recursively chown <c>/ssm/merged</c>, which can fail on stale FUSE mountpoints.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldNotRecursivelyChownMergedRoot_WhenRecursiveMergedChownWouldFail()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.WriteMockToolScript(
			"chown",
			"""
			#!/usr/bin/env sh
			has_recursive=0
			targets_merged=0
			for argument in "$@"; do
			  if [ "$argument" = "-R" ]; then
			    has_recursive=1
			  fi
			  if [ "$argument" = "/ssm/merged" ]; then
			    targets_merged=1
			  fi
			done
			if [ "$has_recursive" -eq 1 ] && [ "$targets_merged" -eq 1 ]; then
			  echo "chown: cannot access '/ssm/merged/Stale': Transport endpoint is not connected" >&2
			  exit 1
			fi
			exit 0
			""");

		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			"--volume",
			$"{workspace.MockBinPath}:/ssm/mock-bin:ro",
			"--env",
			"PATH=/ssm/mock-bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
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
		Assert.DoesNotContain("Transport endpoint is not connected", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies merged-child ownership repair failures are logged as warnings without aborting startup.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldContinue_WhenMergedChildOwnershipRepairFails()
	{
		using ContainerFixtureWorkspace workspace = new();
		Directory.CreateDirectory(Path.Combine(workspace.MergedRootPath, "Stale"));
		workspace.WriteMockToolScript(
			"chown",
			"""
			#!/usr/bin/env sh
			for argument in "$@"; do
			  if [ "$argument" = "/ssm/merged/Stale" ]; then
			    echo "chown: cannot access '/ssm/merged/Stale': Transport endpoint is not connected" >&2
			    exit 1
			  fi
			done
			exit 0
			""");

		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			"--volume",
			$"{workspace.MockBinPath}:/ssm/mock-bin:ro",
			"--volume",
			$"{workspace.MergedRootPath}:/ssm/merged",
			"--env",
			"PATH=/ssm/mock-bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
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
		Assert.Contains("Failed to chown existing merged child '/ssm/merged/Stale'", result.StandardError, StringComparison.Ordinal);
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
