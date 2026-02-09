namespace SuwayomiSourceMerge.UnitTests.Configuration.Validation;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;
using SuwayomiSourceMerge.UnitTests.Configuration;

public sealed class SettingsDocumentValidatorTests
{
    [Fact]
    public void Validate_ShouldPass_ForDefaultDocument()
    {
        SettingsDocumentValidator validator = new();
        SettingsDocument document = ConfigurationTestData.CreateValidSettingsDocument();

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_ShouldAllowBoundaryValues_ForNonNegativeFields()
    {
        SettingsDocumentValidator validator = new();
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();

        SettingsDocument document = new()
        {
            Paths = baseline.Paths,
            Scan = new SettingsScanSection
            {
                MergeIntervalSeconds = 1,
                MergeTriggerPollSeconds = 1,
                MergeMinSecondsBetweenScans = 0,
                MergeLockRetrySeconds = 1
            },
            Rename = new SettingsRenameSection
            {
                RenameDelaySeconds = 0,
                RenameQuietSeconds = 0,
                RenamePollSeconds = 1,
                RenameRescanSeconds = 1
            },
            Diagnostics = new SettingsDiagnosticsSection
            {
                DebugTiming = true,
                DebugTimingTopN = 1,
                DebugTimingMinItemMs = 0,
                DebugTimingSlowMs = 1,
                DebugTimingLive = true,
                DebugScanProgressEvery = 0,
                DebugScanProgressSeconds = 0,
                DebugComicInfo = false,
                TimeoutPollMs = 1,
                TimeoutPollMsFast = 1
            },
            Shutdown = new SettingsShutdownSection
            {
                UnmountOnExit = true,
                StopTimeoutSeconds = 1,
                ChildExitGraceSeconds = 1,
                UnmountCommandTimeoutSeconds = 1,
                UnmountDetachWaitSeconds = 1,
                CleanupHighPriority = true
            },
            Permissions = baseline.Permissions,
            Runtime = baseline.Runtime
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldReportDeterministicError_WhenExcludedSourcesContainDuplicates()
    {
        SettingsDocumentValidator validator = new();
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();

        SettingsDocument document = new()
        {
            Paths = baseline.Paths,
            Scan = baseline.Scan,
            Rename = baseline.Rename,
            Diagnostics = baseline.Diagnostics,
            Shutdown = baseline.Shutdown,
            Permissions = baseline.Permissions,
            Runtime = new SettingsRuntimeSection
            {
                LowPriority = baseline.Runtime!.LowPriority,
                StartupCleanup = baseline.Runtime.StartupCleanup,
                RescanNow = baseline.Runtime.RescanNow,
                EnableMountHealthcheck = baseline.Runtime.EnableMountHealthcheck,
                DetailsDescriptionMode = baseline.Runtime.DetailsDescriptionMode,
                MergerfsOptionsBase = baseline.Runtime.MergerfsOptionsBase,
                ExcludedSources = ["Source A", " source-a "]
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("settings.yml", error.File);
        Assert.Equal("$.runtime.excluded_sources[1]", error.Path);
        Assert.Equal("CFG-SET-006", error.Code);
    }

    [Fact]
    public void Validate_ShouldThrow_WhenDocumentIsNull()
    {
        SettingsDocumentValidator validator = new();

        Assert.Throws<ArgumentNullException>(() => validator.Validate(null!, "settings.yml"));
    }

    [Fact]
    public void Validate_ShouldThrow_WhenFileIsWhitespace()
    {
        SettingsDocumentValidator validator = new();

        Assert.Throws<ArgumentException>(() => validator.Validate(ConfigurationTestData.CreateValidSettingsDocument(), " "));
    }

    [Fact]
    public void Validate_ShouldReportMissingAndRangeErrors_WhenRequiredValuesAreMissing()
    {
        SettingsDocumentValidator validator = new();
        SettingsDocument document = new()
        {
            Paths = new SettingsPathsSection
            {
                ConfigRootPath = null,
                SourcesRootPath = "relative/path",
                OverrideRootPath = "/ssm/override",
                MergedRootPath = "/ssm/merged",
                StateRootPath = "/ssm/state",
                LogRootPath = "/ssm/config",
                BranchLinksRootPath = "/ssm/state/.branches",
                UnraidCachePoolName = string.Empty
            },
            Scan = new SettingsScanSection
            {
                MergeIntervalSeconds = null,
                MergeTriggerPollSeconds = 0,
                MergeMinSecondsBetweenScans = null,
                MergeLockRetrySeconds = 1
            },
            Rename = new SettingsRenameSection
            {
                RenameDelaySeconds = -1,
                RenameQuietSeconds = null,
                RenamePollSeconds = 1,
                RenameRescanSeconds = 1
            },
            Diagnostics = new SettingsDiagnosticsSection
            {
                DebugTiming = null,
                DebugTimingTopN = 1,
                DebugTimingMinItemMs = 0,
                DebugTimingSlowMs = 1,
                DebugTimingLive = true,
                DebugScanProgressEvery = 0,
                DebugScanProgressSeconds = 0,
                DebugComicInfo = false,
                TimeoutPollMs = 1,
                TimeoutPollMsFast = 1
            },
            Shutdown = new SettingsShutdownSection
            {
                UnmountOnExit = true,
                StopTimeoutSeconds = 1,
                ChildExitGraceSeconds = 1,
                UnmountCommandTimeoutSeconds = 1,
                UnmountDetachWaitSeconds = 1,
                CleanupHighPriority = true
            },
            Permissions = new SettingsPermissionsSection
            {
                InheritFromParent = true,
                EnforceExisting = null,
                ReferencePath = "/ssm/sources"
            },
            Runtime = new SettingsRuntimeSection
            {
                LowPriority = true,
                StartupCleanup = true,
                RescanNow = true,
                EnableMountHealthcheck = true,
                DetailsDescriptionMode = null,
                MergerfsOptionsBase = "allow_other",
                ExcludedSources = null
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.paths.config_root_path" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.paths.sources_root_path" && error.Code == "CFG-SET-003");
        Assert.Contains(result.Errors, error => error.Path == "$.scan.merge_interval_seconds" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.scan.merge_trigger_poll_seconds" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.rename.rename_delay_seconds" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.details_description_mode" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.excluded_sources" && error.Code == "CFG-SET-002");
    }

    [Fact]
    public void Validate_ShouldReportMissingSectionAndEmptyExcludedSourceItem()
    {
        SettingsDocumentValidator validator = new();
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
        SettingsDocument document = new()
        {
            Paths = null,
            Scan = baseline.Scan,
            Rename = baseline.Rename,
            Diagnostics = baseline.Diagnostics,
            Shutdown = baseline.Shutdown,
            Permissions = baseline.Permissions,
            Runtime = new SettingsRuntimeSection
            {
                LowPriority = baseline.Runtime!.LowPriority,
                StartupCleanup = baseline.Runtime.StartupCleanup,
                RescanNow = baseline.Runtime.RescanNow,
                EnableMountHealthcheck = baseline.Runtime.EnableMountHealthcheck,
                DetailsDescriptionMode = baseline.Runtime.DetailsDescriptionMode,
                MergerfsOptionsBase = baseline.Runtime.MergerfsOptionsBase,
                ExcludedSources = [" ", "Source A"]
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.paths" && error.Code == "CFG-SET-001");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.excluded_sources[0]" && error.Code == "CFG-SET-002");
    }
}
