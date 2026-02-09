namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class SsmLoggerFactoryTests
{
    [Theory]
    [InlineData("trace")]
    [InlineData("debug")]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("none")]
    public void Create_ShouldAcceptAllSupportedLogLevels(string level)
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, level);
        SsmLoggerFactory factory = new();

        ISsmLogger logger = factory.Create(settings, _ => { });
        logger.Error("event.error", "error");

        if (level == "none")
        {
            Assert.False(File.Exists(Path.Combine(temporaryDirectory.Path, "daemon.log")));
            return;
        }

        Assert.True(File.Exists(Path.Combine(temporaryDirectory.Path, "daemon.log")));
    }

    [Fact]
    public void Create_ShouldBuildWorkingLogger_ForConfiguredPathAndLevel()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, "debug");
        SsmLoggerFactory factory = new();

        ISsmLogger logger = factory.Create(settings, _ => { });
        logger.Debug("host.startup", "Host started.");

        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        Assert.True(File.Exists(logPath));
        string content = File.ReadAllText(logPath);
        Assert.Contains("event=\"host.startup\"", content);
    }

    [Fact]
    public void Create_ShouldAllowLogLevelWithWhitespaceAndMixedCase()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, "  WaRnInG ");
        SsmLoggerFactory factory = new();

        ISsmLogger logger = factory.Create(settings, _ => { });
        logger.Warning("host.warning", "warning");

        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        Assert.True(File.Exists(logPath));
        string content = File.ReadAllText(logPath);
        Assert.Contains("level=warning", content);
    }

    [Fact]
    public void Create_ShouldSuppressFileLogs_WhenLevelIsNone()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, "none");
        SsmLoggerFactory factory = new();

        ISsmLogger logger = factory.Create(settings, _ => { });
        logger.Error("host.unhandled_exception", "failure");

        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        Assert.False(File.Exists(logPath));
    }

    [Fact]
    public void Create_ShouldThrow_WhenLevelIsInvalid()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, "information");
        SsmLoggerFactory factory = new();

        Assert.Throws<InvalidOperationException>(() => factory.Create(settings, _ => { }));
    }

    [Fact]
    public void Create_ShouldThrow_WhenLevelIsWhitespace()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, " ");
        SsmLoggerFactory factory = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => factory.Create(settings, _ => { }));
        Assert.Equal("Settings logging.level is required for logger creation.", exception.Message);
    }

    [Fact]
    public void Create_ShouldThrow_WhenSettingsIsNull()
    {
        SsmLoggerFactory factory = new();

        Assert.Throws<ArgumentNullException>(() => factory.Create(null!, _ => { }));
    }

    [Fact]
    public void Create_ShouldThrow_WhenFallbackWriterIsNull()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, "debug");
        SsmLoggerFactory factory = new();

        Assert.Throws<ArgumentNullException>(() => factory.Create(settings, null!));
    }

    [Fact]
    public void Create_ShouldThrow_WhenLoggingSectionMissing()
    {
        using TemporaryDirectory temporaryDirectory = new();
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
                LogRootPath = temporaryDirectory.Path,
                BranchLinksRootPath = defaults.Paths.BranchLinksRootPath,
                UnraidCachePoolName = defaults.Paths.UnraidCachePoolName
            },
            Scan = defaults.Scan,
            Rename = defaults.Rename,
            Diagnostics = defaults.Diagnostics,
            Shutdown = defaults.Shutdown,
            Permissions = defaults.Permissions,
            Runtime = defaults.Runtime,
            Logging = null
        };
        SsmLoggerFactory factory = new();

        Assert.Throws<InvalidOperationException>(() => factory.Create(settings, _ => { }));
    }

    [Fact]
    public void Create_ShouldThrow_WhenRetainedCountInvalid()
    {
        using TemporaryDirectory temporaryDirectory = new();
        SettingsDocument settings = CreateSettings(temporaryDirectory.Path, "debug");
        SettingsDocument invalidSettings = new()
        {
            Paths = settings.Paths,
            Scan = settings.Scan,
            Rename = settings.Rename,
            Diagnostics = settings.Diagnostics,
            Shutdown = settings.Shutdown,
            Permissions = settings.Permissions,
            Runtime = settings.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = settings.Logging!.FileName,
                MaxFileSizeMb = settings.Logging.MaxFileSizeMb,
                RetainedFileCount = 0,
                Level = settings.Logging.Level
            }
        };
        SsmLoggerFactory factory = new();

        Assert.Throws<InvalidOperationException>(() => factory.Create(invalidSettings, _ => { }));
    }

    [Fact]
    public void Create_ShouldThrow_WhenLogRootPathMissing()
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
                LogRootPath = " ",
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
                RetainedFileCount = 1,
                Level = "debug"
            }
        };
        SsmLoggerFactory factory = new();

        Assert.Throws<InvalidOperationException>(() => factory.Create(settings, _ => { }));
    }

    private static SettingsDocument CreateSettings(string logRootPath, string level)
    {
        SettingsDocument defaults = SettingsDocumentDefaults.Create();
        return new SettingsDocument
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
                Level = level
            }
        };
    }
}
