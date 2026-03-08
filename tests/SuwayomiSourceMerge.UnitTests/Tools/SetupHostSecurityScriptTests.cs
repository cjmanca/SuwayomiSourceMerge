namespace SuwayomiSourceMerge.UnitTests.Tools;

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Regression coverage for <c>tools/setup-host-security.sh</c> bind-path ownership preflight behavior.
/// </summary>
public sealed class SetupHostSecurityScriptTests
{
	/// <summary>
	/// Verifies missing bind-path chain segments are created and repaired from peer disk metadata.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldRepairMissingBindPathChainFromPeerDiskMetadata()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;

		string diskOneMediaPath = Directory.CreateDirectory(Path.Combine(diskOneRootPath, "media")).FullName;
		string diskTwoMediaPath = Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "media")).FullName;
		string diskOneMangaPath = Directory.CreateDirectory(Path.Combine(diskOneMediaPath, "manga")).FullName;
		string diskTwoMangaPath = Directory.CreateDirectory(Path.Combine(diskTwoMediaPath, "manga")).FullName;

		SetUnixMode(diskOneMediaPath, "771");
		SetUnixMode(diskTwoMediaPath, "771");
		SetUnixMode(diskOneMangaPath, "773");
		SetUnixMode(diskTwoMangaPath, "773");

		string bindPath = Path.Combine(cacheRootPath, "media", "manga");
		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--bind-path",
				bindPath
			]);

		Assert.Equal(0, result.ExitCode);
		Assert.True(Directory.Exists(Path.Combine(cacheRootPath, "media")));
		Assert.True(Directory.Exists(bindPath));
		Assert.Equal("771", GetUnixMode(Path.Combine(cacheRootPath, "media")));
		Assert.Equal("773", GetUnixMode(bindPath));
		Assert.Contains("source=peer:", result.StandardOutput, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies tie resolution applies newest-mtime preference first, then lowest disk number.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldResolvePeerMetadataTies_ByNewestMtimeThenLowestDiskNumber()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;

		string newerByMtimeDiskOnePath = Directory.CreateDirectory(Path.Combine(diskOneRootPath, "manga-newest", "series")).FullName;
		string newerByMtimeDiskTwoPath = Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "manga-newest", "series")).FullName;
		SetUnixMode(Path.Combine(diskOneRootPath, "manga-newest"), "711");
		SetUnixMode(newerByMtimeDiskOnePath, "711");
		SetUnixMode(Path.Combine(diskTwoRootPath, "manga-newest"), "722");
		SetUnixMode(newerByMtimeDiskTwoPath, "722");
		Directory.SetLastWriteTimeUtc(Path.Combine(diskOneRootPath, "manga-newest"), new DateTime(2024, 01, 01, 0, 0, 1, DateTimeKind.Utc));
		Directory.SetLastWriteTimeUtc(newerByMtimeDiskOnePath, new DateTime(2024, 01, 01, 0, 0, 1, DateTimeKind.Utc));
		Directory.SetLastWriteTimeUtc(Path.Combine(diskTwoRootPath, "manga-newest"), new DateTime(2024, 01, 01, 0, 0, 2, DateTimeKind.Utc));
		Directory.SetLastWriteTimeUtc(newerByMtimeDiskTwoPath, new DateTime(2024, 01, 01, 0, 0, 2, DateTimeKind.Utc));

		string equalMtimeDiskOnePath = Directory.CreateDirectory(Path.Combine(diskOneRootPath, "manga-equal", "series")).FullName;
		string equalMtimeDiskTwoPath = Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "manga-equal", "series")).FullName;
		SetUnixMode(Path.Combine(diskOneRootPath, "manga-equal"), "701");
		SetUnixMode(equalMtimeDiskOnePath, "701");
		SetUnixMode(Path.Combine(diskTwoRootPath, "manga-equal"), "702");
		SetUnixMode(equalMtimeDiskTwoPath, "702");
		DateTime tieTimeUtc = new(2024, 01, 01, 0, 0, 10, DateTimeKind.Utc);
		Directory.SetLastWriteTimeUtc(Path.Combine(diskOneRootPath, "manga-equal"), tieTimeUtc);
		Directory.SetLastWriteTimeUtc(equalMtimeDiskOnePath, tieTimeUtc);
		Directory.SetLastWriteTimeUtc(Path.Combine(diskTwoRootPath, "manga-equal"), tieTimeUtc);
		Directory.SetLastWriteTimeUtc(equalMtimeDiskTwoPath, tieTimeUtc);

		string newestTargetPath = Path.Combine(cacheRootPath, "manga-newest", "series");
		string equalTargetPath = Path.Combine(cacheRootPath, "manga-equal", "series");

		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--bind-path",
				newestTargetPath,
				"--bind-path",
				equalTargetPath
			]);

		Assert.Equal(0, result.ExitCode);
		Assert.Equal("722", GetUnixMode(Path.Combine(cacheRootPath, "manga-newest")));
		Assert.Equal("722", GetUnixMode(newestTargetPath));
		Assert.Equal("701", GetUnixMode(Path.Combine(cacheRootPath, "manga-equal")));
		Assert.Equal("701", GetUnixMode(equalTargetPath));
	}

	/// <summary>
	/// Verifies inspect-mode discovery fails fast with actionable guidance when no usable binds are found.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldExitWithActionableError_WhenInspectReturnsNoUsableBindPaths()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		File.WriteAllText(fixture.DockerMountsOutputPath, "bind|/mnt/cache/appdata/ssm/config|/ssm/config\n");
		File.WriteAllText(fixture.DockerEnvironmentOutputPath, "PUID=99\nPGID=100\n");

		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--inspect-container",
				"ssm"
			]);

		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("did not expose usable bind mounts", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Provide one or more --bind-path values", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies running setup twice preserves equivalent host state and does not duplicate fuse configuration entries.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldRemainIdempotent_WhenExecutedTwice()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;

		string diskOneMediaPath = Directory.CreateDirectory(Path.Combine(diskOneRootPath, "media")).FullName;
		string diskTwoMediaPath = Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "media")).FullName;
		string diskOneMangaPath = Directory.CreateDirectory(Path.Combine(diskOneMediaPath, "manga")).FullName;
		string diskTwoMangaPath = Directory.CreateDirectory(Path.Combine(diskTwoMediaPath, "manga")).FullName;

		SetUnixMode(diskOneMediaPath, "771");
		SetUnixMode(diskTwoMediaPath, "771");
		SetUnixMode(diskOneMangaPath, "773");
		SetUnixMode(diskTwoMangaPath, "773");

		string bindPath = Path.Combine(cacheRootPath, "media", "manga");
		ScriptExecutionResult firstResult = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--bind-path",
				bindPath
			]);
		Assert.Equal(0, firstResult.ExitCode);

		ScriptExecutionResult secondResult = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--bind-path",
				bindPath
			]);
		Assert.Equal(0, secondResult.ExitCode);

		Assert.Equal("771", GetUnixMode(Path.Combine(cacheRootPath, "media")));
		Assert.Equal("773", GetUnixMode(bindPath));

		string fuseConfiguration = File.ReadAllText(fixture.FuseConfigPath);
		int userAllowOtherEntryCount = Regex.Matches(
			fuseConfiguration,
			@"(?m)^\s*user_allow_other(\s*#.*)?\s*$",
			RegexOptions.CultureInvariant).Count;
		Assert.Equal(1, userAllowOtherEntryCount);
	}

	/// <summary>
	/// Verifies bind-path repair creates one mover lock sentinel directory and file under every repaired bind path.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldCreateMoverLockSentinelForEachBindPath()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		const string lockDirectoryName = ".ssm-lock";
		const string lockSentinelFileName = ".nosync";

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;

		string diskOneSourcesPath = Directory.CreateDirectory(Path.Combine(diskOneRootPath, "sources", "manga")).FullName;
		string diskTwoSourcesPath = Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "sources", "manga")).FullName;
		string diskOneOverridesPath = Directory.CreateDirectory(Path.Combine(diskOneRootPath, "override", "manga")).FullName;
		string diskTwoOverridesPath = Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "override", "manga")).FullName;
		SetUnixMode(Path.Combine(diskOneRootPath, "sources"), "775");
		SetUnixMode(Path.Combine(diskTwoRootPath, "sources"), "775");
		SetUnixMode(diskOneSourcesPath, "775");
		SetUnixMode(diskTwoSourcesPath, "775");
		SetUnixMode(Path.Combine(diskOneRootPath, "override"), "775");
		SetUnixMode(Path.Combine(diskTwoRootPath, "override"), "775");
		SetUnixMode(diskOneOverridesPath, "775");
		SetUnixMode(diskTwoOverridesPath, "775");

		string sourceBindPath = Path.Combine(cacheRootPath, "sources", "manga");
		string overrideBindPath = Path.Combine(cacheRootPath, "override", "manga");
		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--bind-path",
				sourceBindPath,
				"--bind-path",
				overrideBindPath
			]);

		Assert.Equal(0, result.ExitCode);
		string sourceLockFilePath = Path.Combine(sourceBindPath, lockDirectoryName, lockSentinelFileName);
		string overrideLockFilePath = Path.Combine(overrideBindPath, lockDirectoryName, lockSentinelFileName);
		Assert.True(File.Exists(sourceLockFilePath));
		Assert.True(File.Exists(overrideLockFilePath));
		Assert.Equal(0, new FileInfo(sourceLockFilePath).Length);
		Assert.Equal(0, new FileInfo(overrideLockFilePath).Length);
	}

	/// <summary>
	/// Verifies existing lock sentinel files are normalized to empty files on repeated setup runs.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldNormalizeExistingMoverLockSentinelFileToEmpty()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		const string lockDirectoryName = ".ssm-lock";
		const string lockSentinelFileName = ".nosync";

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;

		Directory.CreateDirectory(Path.Combine(diskOneRootPath, "sources", "manga"));
		Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "sources", "manga"));

		string sourceBindPath = Directory.CreateDirectory(Path.Combine(cacheRootPath, "sources", "manga")).FullName;
		string lockDirectoryPath = Directory.CreateDirectory(Path.Combine(sourceBindPath, lockDirectoryName)).FullName;
		string lockFilePath = Path.Combine(lockDirectoryPath, lockSentinelFileName);
		File.WriteAllText(lockFilePath, "non-empty", Encoding.UTF8);

		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--bind-path",
				sourceBindPath
			]);

		Assert.Equal(0, result.ExitCode);
		Assert.True(File.Exists(lockFilePath));
		Assert.Equal(0, new FileInfo(lockFilePath).Length);
	}

	/// <summary>
	/// Verifies lock-directory type conflicts fail fast with actionable diagnostics.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldExitWithActionableError_WhenMoverLockPathIsNotDirectory()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		const string lockDirectoryName = ".ssm-lock";

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;

		Directory.CreateDirectory(Path.Combine(diskOneRootPath, "sources", "manga"));
		Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "sources", "manga"));
		string sourceBindPath = Directory.CreateDirectory(Path.Combine(cacheRootPath, "sources", "manga")).FullName;
		File.WriteAllText(Path.Combine(sourceBindPath, lockDirectoryName), "invalid", Encoding.UTF8);

		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--bind-path",
				sourceBindPath
			]);

		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("exists but is not a directory", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies inspect-mode derives merged root from the container bind mount when <c>--merged-root</c> is omitted.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldDeriveMergedRootFromInspectContainer_WhenMergedRootNotProvided()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;
		Directory.CreateDirectory(Path.Combine(diskOneRootPath, "sources", "disk1"));
		Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "sources", "disk1"));
		Directory.CreateDirectory(Path.Combine(diskOneRootPath, "override", "priority"));
		Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "override", "priority"));
		string inspectedSourcePath = Path.Combine(cacheRootPath, "sources", "disk1");
		string inspectedOverridePath = Path.Combine(cacheRootPath, "override", "priority");
		string inspectedMergedRootPath = Path.Combine(cacheRootPath, "merged-from-inspect");

		File.WriteAllText(
			fixture.DockerMountsOutputPath,
			$"bind|{inspectedSourcePath}|/ssm/sources/disk1\n" +
			$"bind|{inspectedOverridePath}|/ssm/override/priority\n" +
			$"bind|{inspectedMergedRootPath}|/ssm/merged\n",
			Encoding.UTF8);
		File.WriteAllText(fixture.DockerEnvironmentOutputPath, "PUID=99\nPGID=100\n", Encoding.UTF8);

		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--inspect-container",
				"ssm"
			],
			includeMergedRootArgument: false);

		Assert.Equal(0, result.ExitCode);
		Assert.True(Directory.Exists(inspectedMergedRootPath));
		Assert.False(Directory.Exists(fixture.MergedRootPath));
	}

	/// <summary>
	/// Verifies explicit <c>--merged-root</c> takes precedence over inspect-derived merged bind mounts.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldPreferExplicitMergedRootOverInspectDerivedValue()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string diskOneRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk1")).FullName;
		string diskTwoRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "disk2")).FullName;
		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;
		Directory.CreateDirectory(Path.Combine(diskOneRootPath, "sources", "disk1"));
		Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "sources", "disk1"));
		Directory.CreateDirectory(Path.Combine(diskOneRootPath, "override", "priority"));
		Directory.CreateDirectory(Path.Combine(diskTwoRootPath, "override", "priority"));
		string inspectedSourcePath = Path.Combine(cacheRootPath, "sources", "disk1");
		string inspectedOverridePath = Path.Combine(cacheRootPath, "override", "priority");
		string inspectedMergedRootPath = Path.Combine(cacheRootPath, "merged-from-inspect");
		string explicitMergedRootPath = Path.Combine(cacheRootPath, "merged-explicit");

		File.WriteAllText(
			fixture.DockerMountsOutputPath,
			$"bind|{inspectedSourcePath}|/ssm/sources/disk1\n" +
			$"bind|{inspectedOverridePath}|/ssm/override/priority\n" +
			$"bind|{inspectedMergedRootPath}|/ssm/merged\n",
			Encoding.UTF8);
		File.WriteAllText(fixture.DockerEnvironmentOutputPath, "PUID=99\nPGID=100\n", Encoding.UTF8);

		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--inspect-container",
				"ssm",
				"--merged-root",
				explicitMergedRootPath
			],
			includeMergedRootArgument: false);

		Assert.Equal(0, result.ExitCode);
		Assert.True(Directory.Exists(explicitMergedRootPath));
		Assert.False(Directory.Exists(inspectedMergedRootPath));
	}

	/// <summary>
	/// Verifies inspect-mode fails fast when no merged bind mount is discoverable and <c>--merged-root</c> is omitted.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldExitWithActionableError_WhenInspectDoesNotExposeMergedBindAndMergedRootNotProvided()
	{
		if (!OperatingSystem.IsLinux())
		{
			return;
		}

		string repositoryRoot = RepositoryRootLocator.FindRepositoryRoot();
		using TemporaryDirectory temporaryDirectory = new();
		ScriptFixture fixture = ScriptFixture.Create(temporaryDirectory.Path);

		string cacheRootPath = Directory.CreateDirectory(Path.Combine(fixture.MountRootPath, "cache")).FullName;
		string inspectedSourcePath = Path.Combine(cacheRootPath, "sources", "disk1");
		string inspectedOverridePath = Path.Combine(cacheRootPath, "override", "priority");

		File.WriteAllText(
			fixture.DockerMountsOutputPath,
			$"bind|{inspectedSourcePath}|/ssm/sources/disk1\n" +
			$"bind|{inspectedOverridePath}|/ssm/override/priority\n",
			Encoding.UTF8);
		File.WriteAllText(fixture.DockerEnvironmentOutputPath, "PUID=99\nPGID=100\n", Encoding.UTF8);

		ScriptExecutionResult result = ExecuteScript(
			repositoryRoot,
			fixture,
			[
				"--inspect-container",
				"ssm"
			],
			includeMergedRootArgument: false);

		Assert.NotEqual(0, result.ExitCode);
		Assert.Contains("did not expose a bind mount for '/ssm/merged'", result.StandardError, StringComparison.Ordinal);
		Assert.Contains("Provide --merged-root PATH", result.StandardError, StringComparison.Ordinal);
	}

	/// <summary>
	/// Executes the host setup script with deterministic temp-path arguments and mock toolchain.
	/// </summary>
	/// <param name="repositoryRoot">Absolute repository root path.</param>
	/// <param name="fixture">Script fixture paths and mock toolchain.</param>
	/// <param name="arguments">Command-line arguments passed to the script.</param>
	/// <returns>Execution result.</returns>
	private static ScriptExecutionResult ExecuteScript(
		string repositoryRoot,
		ScriptFixture fixture,
		IReadOnlyList<string> arguments,
		bool includeMergedRootArgument = true)
	{
		string scriptPath = Path.Combine(repositoryRoot, "tools", "setup-host-security.sh");
		ProcessStartInfo startInfo = new()
		{
			FileName = "bash",
			WorkingDirectory = repositoryRoot,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		startInfo.ArgumentList.Add(scriptPath);
		foreach (string argument in arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		startInfo.ArgumentList.Add("--mount-root");
		startInfo.ArgumentList.Add(fixture.MountRootPath);
		if (includeMergedRootArgument)
		{
			startInfo.ArgumentList.Add("--merged-root");
			startInfo.ArgumentList.Add(fixture.MergedRootPath);
		}
		startInfo.ArgumentList.Add("--fuse-conf");
		startInfo.ArgumentList.Add(fixture.FuseConfigPath);
		startInfo.ArgumentList.Add("--seccomp-dest");
		startInfo.ArgumentList.Add(fixture.SeccompDestinationPath);
		startInfo.ArgumentList.Add("--apparmor-dest");
		startInfo.ArgumentList.Add(fixture.AppArmorDestinationPath);
		startInfo.ArgumentList.Add("--fallback-puid");
		startInfo.ArgumentList.Add(Environment.GetEnvironmentVariable("UID") ?? "99");
		startInfo.ArgumentList.Add("--fallback-pgid");
		startInfo.ArgumentList.Add(Environment.GetEnvironmentVariable("GID") ?? "100");

		startInfo.Environment["PATH"] = $"{fixture.MockBinaryDirectoryPath}:{Environment.GetEnvironmentVariable("PATH")}";
		startInfo.Environment["SETUP_HOST_SECURITY_SKIP_ROOT_CHECK"] = "1";
		startInfo.Environment["MOCK_DOCKER_MOUNTS_FILE"] = fixture.DockerMountsOutputPath;
		startInfo.Environment["MOCK_DOCKER_ENV_FILE"] = fixture.DockerEnvironmentOutputPath;

		using Process? process = Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException("Failed to start setup-host-security.sh process.");
		}

		string standardOutput = process.StandardOutput.ReadToEnd();
		string standardError = process.StandardError.ReadToEnd();
		process.WaitForExit();

		return new ScriptExecutionResult(process.ExitCode, standardOutput, standardError);
	}

	/// <summary>
	/// Sets one Unix mode value on a directory.
	/// </summary>
	/// <param name="path">Directory path.</param>
	/// <param name="modeOctal">Octal mode string (for example <c>775</c>).</param>
	private static void SetUnixMode(string path, string modeOctal)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = "chmod",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		startInfo.ArgumentList.Add(modeOctal);
		startInfo.ArgumentList.Add(path);
		using Process? process = Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException($"Failed to start chmod for '{path}'.");
		}

		string error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"chmod failed for '{path}' with code {process.ExitCode}: {error}");
		}
	}

	/// <summary>
	/// Gets one Unix mode value for a directory.
	/// </summary>
	/// <param name="path">Directory path.</param>
	/// <returns>Octal mode string.</returns>
	private static string GetUnixMode(string path)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = "stat",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};

		startInfo.ArgumentList.Add("-c");
		startInfo.ArgumentList.Add("%a");
		startInfo.ArgumentList.Add(path);
		using Process? process = Process.Start(startInfo);
		if (process is null)
		{
			throw new InvalidOperationException($"Failed to start stat for '{path}'.");
		}

		string output = process.StandardOutput.ReadToEnd().Trim();
		string error = process.StandardError.ReadToEnd();
		process.WaitForExit();
		if (process.ExitCode != 0)
		{
			throw new InvalidOperationException($"stat failed for '{path}' with code {process.ExitCode}: {error}");
		}

		return output;
	}

	/// <summary>
	/// One script-execution fixture.
	/// </summary>
	/// <param name="MountRootPath">Mount root path for simulated host mounts.</param>
	/// <param name="MergedRootPath">Merged root path.</param>
	/// <param name="FuseConfigPath">Fuse config path.</param>
	/// <param name="SeccompDestinationPath">Seccomp destination path.</param>
	/// <param name="AppArmorDestinationPath">AppArmor destination path.</param>
	/// <param name="MockBinaryDirectoryPath">Mock command directory path.</param>
	/// <param name="DockerMountsOutputPath">Mock Docker mounts output file path.</param>
	/// <param name="DockerEnvironmentOutputPath">Mock Docker environment output file path.</param>
	private sealed record ScriptFixture(
		string MountRootPath,
		string MergedRootPath,
		string FuseConfigPath,
		string SeccompDestinationPath,
		string AppArmorDestinationPath,
		string MockBinaryDirectoryPath,
		string DockerMountsOutputPath,
		string DockerEnvironmentOutputPath)
	{
		/// <summary>
		/// Creates one deterministic script fixture rooted under a temporary directory.
		/// </summary>
		/// <param name="workspaceRootPath">Workspace root path.</param>
		/// <returns>Created fixture.</returns>
		public static ScriptFixture Create(string workspaceRootPath)
		{
			string mountRootPath = Directory.CreateDirectory(Path.Combine(workspaceRootPath, "mnt")).FullName;
			string mergedRootPath = Path.Combine(workspaceRootPath, "merged");
			string fuseConfigPath = Path.Combine(workspaceRootPath, "fuse.conf");
			string seccompDestinationPath = Path.Combine(workspaceRootPath, "security", "seccomp", "ssm-mergerfs.json");
			string appArmorDestinationPath = Path.Combine(workspaceRootPath, "security", "apparmor", "ssm-mergerfs");
			string mockBinaryDirectoryPath = Directory.CreateDirectory(Path.Combine(workspaceRootPath, "mock-bin")).FullName;
			string dockerMountsOutputPath = Path.Combine(workspaceRootPath, "docker-mounts.txt");
			string dockerEnvironmentOutputPath = Path.Combine(workspaceRootPath, "docker-env.txt");
			File.WriteAllText(dockerMountsOutputPath, string.Empty, Encoding.UTF8);
			File.WriteAllText(dockerEnvironmentOutputPath, string.Empty, Encoding.UTF8);
			CreateMockCommands(mockBinaryDirectoryPath);

			return new ScriptFixture(
				mountRootPath,
				mergedRootPath,
				fuseConfigPath,
				seccompDestinationPath,
				appArmorDestinationPath,
				mockBinaryDirectoryPath,
				dockerMountsOutputPath,
				dockerEnvironmentOutputPath);
		}

		/// <summary>
		/// Creates deterministic mock commands used by script tests.
		/// </summary>
		/// <param name="mockBinaryDirectoryPath">Mock command directory path.</param>
		private static void CreateMockCommands(string mockBinaryDirectoryPath)
		{
			WriteExecutableScript(
				Path.Combine(mockBinaryDirectoryPath, "mountpoint"),
				"""
				#!/usr/bin/env bash
				exit 1
				""");

			WriteExecutableScript(
				Path.Combine(mockBinaryDirectoryPath, "mount"),
				"""
				#!/usr/bin/env bash
				exit 0
				""");

			WriteExecutableScript(
				Path.Combine(mockBinaryDirectoryPath, "apparmor_parser"),
				"""
				#!/usr/bin/env bash
				exit 0
				""");

			WriteExecutableScript(
				Path.Combine(mockBinaryDirectoryPath, "docker"),
				"""
				#!/usr/bin/env bash
				set -euo pipefail
				if [[ "${1:-}" != "inspect" ]]; then
				  echo "Unsupported docker command: $*" >&2
				  exit 2
				fi

				if [[ "${2:-}" != "--format" ]]; then
				  echo "Unsupported docker inspect usage: $*" >&2
				  exit 2
				fi

				inspect_format="${3:-}"
				if [[ "$inspect_format" == *".Mounts"* ]]; then
				  if [[ -n "${MOCK_DOCKER_MOUNTS_FILE:-}" && -f "${MOCK_DOCKER_MOUNTS_FILE:-}" ]]; then
				    cat "$MOCK_DOCKER_MOUNTS_FILE"
				  fi
				  exit 0
				fi

				if [[ "$inspect_format" == *".Config.Env"* ]]; then
				  if [[ -n "${MOCK_DOCKER_ENV_FILE:-}" && -f "${MOCK_DOCKER_ENV_FILE:-}" ]]; then
				    cat "$MOCK_DOCKER_ENV_FILE"
				  fi
				  exit 0
				fi

				exit 0
				""");
		}

		/// <summary>
		/// Writes one executable shell script file.
		/// </summary>
		/// <param name="path">Script path.</param>
		/// <param name="contents">Script content.</param>
		private static void WriteExecutableScript(string path, string contents)
		{
			File.WriteAllText(path, contents, Encoding.UTF8);
			SetUnixMode(path, "755");
		}
	}

	/// <summary>
	/// Script execution result.
	/// </summary>
	/// <param name="ExitCode">Process exit code.</param>
	/// <param name="StandardOutput">Captured standard output.</param>
	/// <param name="StandardError">Captured standard error.</param>
	private sealed record ScriptExecutionResult(int ExitCode, string StandardOutput, string StandardError);
}
