using System.Globalization;
using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Entrypoint log-path safety and parsing regression coverage for container runtime startup behavior.
/// </summary>
public sealed partial class ContainerRuntimeEndToEndTests
{
	/// <summary>
	/// Verifies section headers and scalar inline comments do not break entrypoint log mirroring.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldMirrorEntrypointDiagnosticsToDaemonLog_WhenSettingsContainSectionAndInlineComments()
	{
		using ContainerFixtureWorkspace workspace = new();
		workspace.WriteConfigFile(
			"settings.yml",
			"""
			paths: # section comment
			  log_root_path: /ssm/config # inline comment
			logging: # section comment
			  file_name: daemon.log # inline comment
			""");

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			CreateEntrypointOnlyEnvironment("invalid", "100"));

		Assert.False(result.TimedOut);
		Assert.Equal(64, result.ExitCode);
		Assert.Contains("Invalid PUID value", result.StandardError, StringComparison.Ordinal);

		string daemonLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
		DockerAssertions.WaitForFileContains(daemonLogPath, "Invalid PUID value", TimeSpan.FromSeconds(30));
		Assert.False(File.Exists(Path.Combine(workspace.ConfigRootPath, "daemon.log # inline comment")));
	}

	/// <summary>
	/// Verifies embedded hash characters in plain and quoted scalars are preserved for log file names.
	/// </summary>
	[Theory]
	[InlineData("foo#bar.log", "foo#bar.log")]
	[InlineData("\"foo#bar.log\"", "foo#bar.log")]
	public void Run_Edge_ShouldPreserveEmbeddedHashInLoggingFileNameScalar_WhenHashIsNotCommentStart(string fileNameScalar, string expectedLogFileName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileNameScalar);
		ArgumentException.ThrowIfNullOrWhiteSpace(expectedLogFileName);

		using ContainerFixtureWorkspace workspace = new();
		workspace.WriteConfigFile(
			"settings.yml",
			string.Format(
				CultureInfo.InvariantCulture,
				"""
				paths:
				  log_root_path: /ssm/config
				logging:
				  file_name: {0}
				""",
				fileNameScalar));

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			CreateEntrypointOnlyEnvironment("invalid", "100"));

		Assert.False(result.TimedOut);
		Assert.Equal(64, result.ExitCode);
		Assert.DoesNotContain("Ignoring unsafe logging.file_name", result.StandardError, StringComparison.Ordinal);

		string resolvedLogPath = Path.Combine(workspace.ConfigRootPath, expectedLogFileName);
		DockerAssertions.WaitForFileContains(resolvedLogPath, "Invalid PUID value", TimeSpan.FromSeconds(30));
	}

	/// <summary>
	/// Verifies rooted <c>logging.file_name</c> falls back to defaults with warning.
	/// </summary>
	[Theory]
	[InlineData("/tmp/daemon.log")]
	[InlineData("logs/daemon.log")]
	[InlineData(@"logs\daemon.log")]
	public void Run_Edge_ShouldFallbackToDefaultDaemonLog_WhenLoggingFileNameIsUnsafe(string unsafeFileName)
	{
		AssertUnsafeLoggingFileNameScalarFallsBack(unsafeFileName);
	}

	/// <summary>
	/// Verifies reserved Windows device names are rejected for <c>logging.file_name</c>.
	/// </summary>
	[Theory]
	[InlineData("CON")]
	[InlineData("nul.txt")]
	public void Run_Edge_ShouldFallbackToDefaultDaemonLog_WhenLoggingFileNameIsReservedDeviceName(string reservedFileName)
	{
		AssertUnsafeLoggingFileNameScalarFallsBack(reservedFileName);
	}

	/// <summary>
	/// Verifies invalid characters are rejected for <c>logging.file_name</c>.
	/// </summary>
	[Theory]
	[InlineData("bad?.log")]
	[InlineData("bad|name.log")]
	public void Run_Edge_ShouldFallbackToDefaultDaemonLog_WhenLoggingFileNameContainsInvalidCharacters(string invalidFileName)
	{
		AssertUnsafeLoggingFileNameScalarFallsBack(invalidFileName);
	}

	/// <summary>
	/// Verifies trailing dots and whitespace are rejected for <c>logging.file_name</c>.
	/// </summary>
	[Theory]
	[InlineData("daemon.")]
	[InlineData("\"daemon.log \"")]
	public void Run_Edge_ShouldFallbackToDefaultDaemonLog_WhenLoggingFileNameHasTrailingDotOrWhitespace(string trailingInvalidFileNameScalar)
	{
		AssertUnsafeLoggingFileNameScalarFallsBack(trailingInvalidFileNameScalar);
	}

	/// <summary>
	/// Builds one environment map for entrypoint-only container command validation.
	/// </summary>
	/// <param name="puid">PUID value.</param>
	/// <param name="pgid">PGID value.</param>
	/// <returns>Environment map.</returns>
	private static IReadOnlyDictionary<string, string> CreateEntrypointOnlyEnvironment(string puid, string pgid)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(puid);
		ArgumentException.ThrowIfNullOrWhiteSpace(pgid);

		return new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["PUID"] = puid,
			["PGID"] = pgid
		};
	}

	/// <summary>
	/// Runs a container command that exercises only entrypoint startup logic.
	/// </summary>
	/// <param name="workspace">Workspace providing bind mounts.</param>
	/// <param name="environmentVariables">Environment variable map.</param>
	/// <returns>Command result.</returns>
	private DockerCommandResult RunEntrypointOnlyContainer(
		ContainerFixtureWorkspace workspace,
		IReadOnlyDictionary<string, string> environmentVariables)
	{
		ArgumentNullException.ThrowIfNull(workspace);
		ArgumentNullException.ThrowIfNull(environmentVariables);

		List<string> arguments = ["run", "--rm"];
		foreach ((string hostPath, string containerPath, bool readOnly) in CreateBindMounts(workspace))
		{
			string suffix = readOnly ? ":ro" : string.Empty;
			arguments.Add("--volume");
			arguments.Add($"{hostPath}:{containerPath}{suffix}");
		}

		foreach ((string key, string value) in environmentVariables.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
		{
			arguments.Add("--env");
			arguments.Add($"{key}={value}");
		}

		arguments.Add(_fixture.ImageTag);
		arguments.Add("bash");
		arguments.Add("-lc");
		arguments.Add("true");
		return _fixture.Runner.Execute(arguments, timeout: TimeSpan.FromMinutes(2));
	}

	/// <summary>
	/// Asserts one unsafe logging-file-name scalar falls back to default daemon log output.
	/// </summary>
	/// <param name="fileNameScalar">Raw YAML scalar value assigned to <c>logging.file_name</c>.</param>
	private void AssertUnsafeLoggingFileNameScalarFallsBack(string fileNameScalar)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileNameScalar);
		using ContainerFixtureWorkspace workspace = new();
		workspace.WriteConfigFile(
			"settings.yml",
			string.Format(
				CultureInfo.InvariantCulture,
				"""
				paths:
				  log_root_path: /ssm/config
				logging:
				  file_name: {0}
				""",
				fileNameScalar));

		DockerCommandResult result = RunEntrypointOnlyContainer(
			workspace,
			CreateEntrypointOnlyEnvironment("invalid", "100"));

		Assert.False(result.TimedOut);
		Assert.Equal(64, result.ExitCode);
		Assert.Contains("Ignoring unsafe logging.file_name", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("falling back to default 'daemon.log'", result.StandardError, StringComparison.Ordinal);

		string daemonLogPath = Path.Combine(workspace.ConfigRootPath, "daemon.log");
		DockerAssertions.WaitForFileContains(daemonLogPath, "Ignoring unsafe logging.file_name", TimeSpan.FromSeconds(30));
		DockerAssertions.WaitForFileContains(daemonLogPath, "Invalid PUID value", TimeSpan.FromSeconds(30));
	}
}
