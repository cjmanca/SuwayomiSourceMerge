using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// End-to-end Docker runtime tests for container assets and daemon orchestration.
/// </summary>
[Collection(DockerIntegrationFixture.COLLECTION_NAME)]
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
	/// Verifies healthy container startup, deferred-merge dispatch logging, and graceful stop.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldBootstrapDispatchAndStopGracefully()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.CreateMockToolScripts();
		Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "SourceA", "MangaA"));
		Directory.CreateDirectory(Path.Combine(workspace.OverrideRootPath, "MangaA"));
		workspace.WriteSettingsFile("/ssm/sources", "/ssm/override", "debug");

		string containerName = BuildContainerName("expected");
		try
		{
			_fixture.Runner.RunContainerDetached(
				_fixture.ImageTag,
				containerName,
				CreateEnvironmentVariables("/ssm/override/MangaA/details.json|CLOSE_WRITE\n"),
				CreateBindMounts(workspace));

			string daemonLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"host.startup\"", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"watcher.tick.summary\"", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"merge.dispatch.deferred\"", TimeSpan.FromSeconds(60));

			Assert.True(File.Exists(Path.Combine(workspace.ConfigRootPath, "manga_equivalents.yml")));
			Assert.True(File.Exists(Path.Combine(workspace.ConfigRootPath, "scene_tags.yml")));
			Assert.True(File.Exists(Path.Combine(workspace.ConfigRootPath, "source_priority.yml")));

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
	/// Builds deterministic environment variables for container execution.
	/// </summary>
	/// <param name="inotifyOutput">Mock inotify output payload.</param>
	/// <returns>Environment variable map.</returns>
	private static IReadOnlyDictionary<string, string> CreateEnvironmentVariables(string inotifyOutput)
	{
		return new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["PUID"] = "99",
			["PGID"] = "100",
			["PATH"] = "/ssm/mock-bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin",
			["INOTIFYWAIT_STDOUT"] = inotifyOutput
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
}
