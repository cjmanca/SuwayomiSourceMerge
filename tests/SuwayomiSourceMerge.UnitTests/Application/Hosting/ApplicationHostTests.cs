namespace SuwayomiSourceMerge.UnitTests.Application.Hosting;

using SuwayomiSourceMerge.Application.Hosting;
using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class ApplicationHostTests
{
	[Fact]
	public void CreateDefault_ShouldReturnHostInstance()
	{
		ApplicationHost host = ApplicationHost.CreateDefault();

		Assert.NotNull(host);
	}

	[Fact]
	public void CreateDefault_ShouldReturnNewInstanceEachCall()
	{
		ApplicationHost first = ApplicationHost.CreateDefault();
		ApplicationHost second = ApplicationHost.CreateDefault();

		Assert.NotSame(first, second);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenBootstrapServiceNull()
	{
		Assert.Throws<ArgumentNullException>(
			() => new ApplicationHost(
				null!,
				new SsmLoggerFactory(),
				new StubRuntimeSupervisorRunner((_, _) => 0)));
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenLoggerFactoryNull()
	{
		Assert.Throws<ArgumentNullException>(
			() => new ApplicationHost(
				new StubBootstrapService(_ => throw new InvalidOperationException("unused")),
				null!,
				new StubRuntimeSupervisorRunner((_, _) => 0)));
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenRuntimeSupervisorRunnerNull()
	{
		Assert.Throws<ArgumentNullException>(
			() => new ApplicationHost(
				new StubBootstrapService(_ => throw new InvalidOperationException("unused")),
				new SsmLoggerFactory(),
				null!));
	}

	[Fact]
	public void StubRuntimeSupervisorRunnerConstructor_ShouldThrow_WhenCallbackNull()
	{
		Assert.Throws<ArgumentNullException>(() => new StubRuntimeSupervisorRunner(null!));
	}

	[Fact]
	public void Run_ShouldWriteLifecycleAndBootstrapLogs_WhenBootstrapSucceeds()
	{
		using TemporaryDirectory temporaryDirectory = new();
		ConfigurationBootstrapWarning warning = new(
			"CFG-MIG-001",
			"manga_equivalents.txt",
			3,
			"Skipped malformed legacy mapping line.");

		ApplicationHost host = new(
			new StubBootstrapService(_ => CreateBootstrapResult(temporaryDirectory.Path, "debug", [warning])),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 0));

		using StringWriter stderr = new();

		int exitCode = host.Run(temporaryDirectory.Path, stderr);

		Assert.Equal(0, exitCode);
		Assert.Equal(string.Empty, stderr.ToString());

		string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
		Assert.True(File.Exists(logPath));

		string logContent = File.ReadAllText(logPath);
		Assert.Contains("event=\"host.startup\"", logContent);
		Assert.Contains("event=\"bootstrap.completed\"", logContent);
		Assert.Contains("event=\"bootstrap.warning\"", logContent);
		Assert.Contains("event=\"host.shutdown\"", logContent);
	}

	[Fact]
	public void Run_ShouldLogAllWarnings_WhenBootstrapReturnsMultipleWarnings()
	{
		using TemporaryDirectory temporaryDirectory = new();
		ConfigurationBootstrapWarning[] warnings =
		[
			new("CFG-MIG-001", "manga_equivalents.txt", 3, "warning-one"),
			new("CFG-MIG-002", "source_priority.txt", 7, "warning-two")
		];

		ApplicationHost host = new(
			new StubBootstrapService(_ => CreateBootstrapResult(temporaryDirectory.Path, "warning", warnings)),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 0));

		using StringWriter stderr = new();
		int exitCode = host.Run(temporaryDirectory.Path, stderr);

		Assert.Equal(0, exitCode);
		string logContent = File.ReadAllText(Path.Combine(temporaryDirectory.Path, "daemon.log"));
		Assert.Equal(2, CountOccurrences(logContent, "event=\"bootstrap.warning\""));
	}

	[Fact]
	public void Run_ShouldReturnFailureAndWriteStderr_WhenBootstrapFails()
	{
		ConfigurationBootstrapException bootstrapException = new(
		[
			new ValidationError("settings.yml", "$.logging.level", "CFG-SET-005", "Allowed values: trace, debug, normal, warning, error, none.")
		]);

		ApplicationHost host = new(
			new StubBootstrapService(_ => throw bootstrapException),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 0));

		using StringWriter stderr = new();

		int exitCode = host.Run("/unused", stderr);

		Assert.Equal(1, exitCode);
		string error = stderr.ToString();
		Assert.Contains("Configuration bootstrap failed.", error);
		Assert.Contains("CFG-SET-005", error);
	}

	[Fact]
	public void Run_ShouldWriteStderr_WhenUnhandledExceptionOccursAfterLoggerCreation()
	{
		using TemporaryDirectory temporaryDirectory = new();

		ApplicationHost host = new(
			new StubBootstrapService(_ => CreateBootstrapResult(temporaryDirectory.Path, "trace", [])),
			new ThrowingLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 0));

		using StringWriter stderr = new();

		int exitCode = host.Run(temporaryDirectory.Path, stderr);

		Assert.Equal(1, exitCode);
		Assert.Contains("Unhandled host exception", stderr.ToString());
	}

	[Fact]
	public void Run_ShouldSuppressFileLogs_WhenLevelIsNone()
	{
		using TemporaryDirectory temporaryDirectory = new();
		ApplicationHost host = new(
			new StubBootstrapService(_ => CreateBootstrapResult(temporaryDirectory.Path, "none", [])),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 0));

		using StringWriter stderr = new();

		int exitCode = host.Run(temporaryDirectory.Path, stderr);

		Assert.Equal(0, exitCode);
		Assert.Equal(string.Empty, stderr.ToString());
		Assert.False(File.Exists(Path.Combine(temporaryDirectory.Path, "daemon.log")));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void Run_ShouldThrow_WhenConfigRootPathInvalid(string? configRootPath)
	{
		ApplicationHost host = new(
			new StubBootstrapService(_ => throw new InvalidOperationException("unused")),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 0));

		using StringWriter stderr = new();

		Assert.ThrowsAny<ArgumentException>(() => host.Run(configRootPath!, stderr));
	}

	[Fact]
	public void Run_ShouldThrow_WhenStandardErrorNull()
	{
		ApplicationHost host = new(
			new StubBootstrapService(_ => throw new InvalidOperationException("unused")),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 0));

		Assert.Throws<ArgumentNullException>(() => host.Run("/config", null!));
	}

	[Fact]
	public void Run_ShouldInvokeRuntimeSupervisorRunner_WhenBootstrapSucceeds()
	{
		using TemporaryDirectory temporaryDirectory = new();
		int runtimeCalls = 0;
		StubRuntimeSupervisorRunner runtimeRunner = new(
			(documents, _) =>
			{
				runtimeCalls++;
				Assert.NotNull(documents.Settings);
				return 0;
			});

		ApplicationHost host = new(
			new StubBootstrapService(_ => CreateBootstrapResult(temporaryDirectory.Path, "warning", [])),
			new SsmLoggerFactory(),
			runtimeRunner);

		using StringWriter stderr = new();

		int exitCode = host.Run(temporaryDirectory.Path, stderr);

		Assert.Equal(0, exitCode);
		Assert.Equal(1, runtimeCalls);
	}

	[Fact]
	public void Run_ShouldReturnFailure_WhenRuntimeSupervisorRunnerReturnsNonZero()
	{
		using TemporaryDirectory temporaryDirectory = new();
		ApplicationHost host = new(
			new StubBootstrapService(_ => CreateBootstrapResult(temporaryDirectory.Path, "warning", [])),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => 1));

		using StringWriter stderr = new();

		int exitCode = host.Run(temporaryDirectory.Path, stderr);

		Assert.Equal(1, exitCode);
		string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
		Assert.True(File.Exists(logPath));
		string logContent = File.ReadAllText(logPath);
		Assert.Contains("event=\"host.shutdown_failed\"", logContent);
		Assert.DoesNotContain("event=\"host.shutdown\"", logContent);
	}

	[Fact]
	public void Run_ShouldLogFullExceptionStackTrace_WhenRuntimeSupervisorThrows()
	{
		using TemporaryDirectory temporaryDirectory = new();
		ApplicationHost host = new(
			new StubBootstrapService(_ => CreateBootstrapResult(temporaryDirectory.Path, "debug", [])),
			new SsmLoggerFactory(),
			new StubRuntimeSupervisorRunner((_, _) => throw new InvalidOperationException("runtime-failure")));

		using StringWriter stderr = new();
		int exitCode = host.Run(temporaryDirectory.Path, stderr);

		Assert.Equal(1, exitCode);
		string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
		Assert.True(File.Exists(logPath));

		string logContent = File.ReadAllText(logPath);
		Assert.Contains("event=\"host.unhandled_exception\"", logContent);
		Assert.Contains("exception_type=\"System.InvalidOperationException\"", logContent);
		Assert.Contains("exception=\"System.InvalidOperationException: runtime-failure", logContent);
		Assert.Contains("\\n", logContent);
	}

	private static ConfigurationBootstrapResult CreateBootstrapResult(
		string logRootPath,
		string loggingLevel,
		IReadOnlyList<ConfigurationBootstrapWarning> warnings)
	{
		SettingsDocument defaults = SettingsDocumentDefaults.Create();
		SettingsDocument settings = new()
		{
			Paths = new SettingsPathsSection
			{
				ConfigRootPath = defaults.Paths!.ConfigRootPath,
				SourcesRootPath = defaults.Paths.SourcesRootPath,
				OverrideRootPath = defaults.Paths.OverrideRootPath,
				MergedRootPath = defaults.Paths.MergedRootPath,
				StateRootPath = defaults.Paths.StateRootPath,
				LogRootPath = logRootPath,
				BranchLinksRootPath = defaults.Paths.BranchLinksRootPath,
				UnraidCachePoolName = defaults.Paths.UnraidCachePoolName
			},
			Scan = defaults.Scan,
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = defaults.Runtime,
			Logging = new SettingsLoggingSection
			{
				FileName = "daemon.log",
				MaxFileSizeMb = 1,
				RetainedFileCount = 2,
				Level = loggingLevel
			}
		};

		return new ConfigurationBootstrapResult
		{
			Documents = new ConfigurationDocumentSet
			{
				Settings = settings,
				MangaEquivalents = MangaEquivalentsDocumentDefaults.Create(),
				SceneTags = SceneTagsDocumentDefaults.Create(),
				SourcePriority = SourcePriorityDocumentDefaults.Create()
			},
			Files =
			[
				new ConfigurationBootstrapFileState(
					"settings.yml",
					Path.Combine(logRootPath, "settings.yml"),
					wasCreated: false,
					wasMigrated: false,
					usedDefaults: false,
					wasSelfHealed: false)
			],
			Warnings = warnings
		};
	}

	private sealed class StubBootstrapService : IConfigurationBootstrapService
	{
		private readonly Func<string, ConfigurationBootstrapResult> _bootstrap;

		public StubBootstrapService(Func<string, ConfigurationBootstrapResult> bootstrap)
		{
			_bootstrap = bootstrap;
		}

		public ConfigurationBootstrapResult Bootstrap(string configRootPath)
		{
			return _bootstrap(configRootPath);
		}
	}

	private sealed class StubRuntimeSupervisorRunner : IRuntimeSupervisorRunner
	{
		private readonly Func<ConfigurationDocumentSet, ISsmLogger, int> _run;

		public StubRuntimeSupervisorRunner(Func<ConfigurationDocumentSet, ISsmLogger, int> run)
		{
			ArgumentNullException.ThrowIfNull(run);
			_run = run;
		}

		public int Run(ConfigurationDocumentSet documents, ISsmLogger logger)
		{
			return _run(documents, logger);
		}
	}

	private static int CountOccurrences(string input, string token)
	{
		int count = 0;
		int index = 0;

		while ((index = input.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
		{
			count++;
			index += token.Length;
		}

		return count;
	}

	private sealed class ThrowingLoggerFactory : ISsmLoggerFactory
	{
		public ISsmLogger Create(SettingsDocument settings, Action<string> fallbackErrorWriter)
		{
			return new ThrowingDebugLogger();
		}
	}

	private sealed class ThrowingDebugLogger : ISsmLogger
	{
		public bool IsEnabled(LogLevel level)
		{
			return true;
		}

		public void Log(LogLevel level, string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			if (level == LogLevel.Debug || level == LogLevel.Normal)
			{
				throw new InvalidOperationException("simulated logger failure");
			}
		}

		public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Log(LogLevel.Trace, eventId, message, context);
		}

		public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Log(LogLevel.Debug, eventId, message, context);
		}

		public void Normal(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Log(LogLevel.Normal, eventId, message, context);
		}

		public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Log(LogLevel.Warning, eventId, message, context);
		}

		public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
		{
			Log(LogLevel.Error, eventId, message, context);
		}
	}
}
