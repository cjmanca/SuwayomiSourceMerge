using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Signal-handling integration coverage for container runtime shutdown behavior.
/// </summary>
public sealed partial class ContainerRuntimeEndToEndTests
{
	/// <summary>
	/// Verifies SIGTERM triggers graceful stop cleanup (unmount sweep and runtime shutdown pass).
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldCleanupGracefully_WhenStoppedWithSigterm()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.CreateMockToolScripts();
		Directory.CreateDirectory(Path.Combine(workspace.SourcesRootPath, "disk1", "SourceA", "MangaA"));
		Directory.CreateDirectory(Path.Combine(workspace.OverrideRootPath, "priority", "MangaA"));
		workspace.WriteSettingsFile("/ssm/sources", "/ssm/override", "debug");

		string containerName = BuildContainerName("edge-sigterm-stop");
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
			DockerAssertions.WaitForFileContains(daemonLogPath, "event=\"merge.dispatch.completed\"", TimeSpan.FromSeconds(60));
			DockerAssertions.WaitForFileContains(commandLogPath, "mergerfs ", TimeSpan.FromSeconds(60));
			int unmountCommandCountBeforeStop = CountUnmountCommandLines(commandLogPath);

			_fixture.Runner.SendSignal(containerName, "SIGTERM");
			int exitCode = _fixture.Runner.WaitContainerExitCode(containerName, TimeSpan.FromSeconds(30));
			Assert.Equal(0, exitCode);

			DockerAssertions.WaitForCondition(
				() => CountUnmountCommandLines(commandLogPath) > unmountCommandCountBeforeStop,
				TimeSpan.FromSeconds(30),
				"Expected SIGTERM shutdown sweep to invoke at least one additional unmount command.");
			DockerAssertions.WaitForFileContains(daemonLogPath, "phase=\"shutdown\"", TimeSpan.FromSeconds(30));
		}
		finally
		{
			_fixture.Runner.RemoveContainerForce(containerName);
		}
	}
}
