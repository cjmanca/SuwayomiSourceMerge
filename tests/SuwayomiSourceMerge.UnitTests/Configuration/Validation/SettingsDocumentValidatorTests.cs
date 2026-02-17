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
                MergeLockRetrySeconds = 1,
                MergeTriggerRequestTimeoutBufferSeconds = 1
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
                CleanupHighPriority = true,
                CleanupApplyHighPriority = false,
                CleanupPriorityIoniceClass = 1,
                CleanupPriorityNiceValue = 19
            },
            Permissions = baseline.Permissions,
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = "daemon.log",
                MaxFileSizeMb = 1,
                RetainedFileCount = 1,
                Level = "trace"
            }
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
            },
            Logging = baseline.Logging
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
                MergeLockRetrySeconds = 1,
                MergeTriggerRequestTimeoutBufferSeconds = 0
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
                CleanupHighPriority = true,
                CleanupApplyHighPriority = null,
                CleanupPriorityIoniceClass = null,
                CleanupPriorityNiceValue = 25
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
            },
            Logging = new SettingsLoggingSection
            {
                FileName = "daemon.log",
                MaxFileSizeMb = 10,
                RetainedFileCount = 10,
                Level = "warning"
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.paths.config_root_path" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.paths.sources_root_path" && error.Code == "CFG-SET-003");
        Assert.Contains(result.Errors, error => error.Path == "$.scan.merge_interval_seconds" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.scan.merge_trigger_poll_seconds" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.scan.merge_trigger_request_timeout_buffer_seconds" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.rename.rename_delay_seconds" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_apply_high_priority" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_priority_ionice_class" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.shutdown.cleanup_priority_nice_value" && error.Code == "CFG-SET-004");
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
            },
            Logging = baseline.Logging
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.paths" && error.Code == "CFG-SET-001");
        Assert.Contains(result.Errors, error => error.Path == "$.runtime.excluded_sources[0]" && error.Code == "CFG-SET-002");
    }

    [Fact]
    public void Validate_ShouldReportDeterministicError_WhenLoggingLevelInvalid()
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
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = "daemon.log",
                MaxFileSizeMb = 10,
                RetainedFileCount = 10,
                Level = "information"
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("settings.yml", error.File);
        Assert.Equal("$.logging.level", error.Path);
        Assert.Equal("CFG-SET-005", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportDeterministicError_WhenWatchStartupModeInvalid()
    {
        SettingsDocumentValidator validator = new();
        SettingsDocument baseline = ConfigurationTestData.CreateValidSettingsDocument();
        SettingsDocument document = new()
        {
            Paths = baseline.Paths,
            Scan = new SettingsScanSection
            {
                MergeIntervalSeconds = baseline.Scan!.MergeIntervalSeconds,
                MergeTriggerPollSeconds = baseline.Scan.MergeTriggerPollSeconds,
                MergeMinSecondsBetweenScans = baseline.Scan.MergeMinSecondsBetweenScans,
                MergeLockRetrySeconds = baseline.Scan.MergeLockRetrySeconds,
                MergeTriggerRequestTimeoutBufferSeconds = baseline.Scan.MergeTriggerRequestTimeoutBufferSeconds,
                WatchStartupMode = "invalid"
            },
            Rename = baseline.Rename,
            Diagnostics = baseline.Diagnostics,
            Shutdown = baseline.Shutdown,
            Permissions = baseline.Permissions,
            Runtime = baseline.Runtime,
            Logging = baseline.Logging
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.scan.watch_startup_mode", error.Path);
        Assert.Equal("CFG-SET-005", error.Code);
    }

    [Fact]
    public void Validate_ShouldAllowLoggingLevelWithWhitespaceAndMixedCase()
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
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = "daemon.log",
                MaxFileSizeMb = 10,
                RetainedFileCount = 10,
                Level = "  WaRnInG "
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldAllowSimpleLoggingFileName()
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
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = "daemon.log",
                MaxFileSizeMb = 10,
                RetainedFileCount = 10,
                Level = "warning"
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ShouldAllowBorderlineValidLoggingFileName()
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
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = "daemon-1_2.v3.LOG",
                MaxFileSizeMb = 10,
                RetainedFileCount = 10,
                Level = "warning"
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("/tmp/daemon.log")]
    [InlineData("../daemon.log")]
    [InlineData("logs/daemon.log")]
    [InlineData(@"logs\daemon.log")]
    [InlineData("daemon.")]
    [InlineData("daemon ")]
    [InlineData("da:mon.log")]
    [InlineData("da*mon.log")]
    [InlineData("daemon\u0001.log")]
    public void Validate_ShouldReportDeterministicError_WhenLoggingFileNameInvalid(string fileName)
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
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = fileName,
                MaxFileSizeMb = 10,
                RetainedFileCount = 10,
                Level = "warning"
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("settings.yml", error.File);
        Assert.Equal("$.logging.file_name", error.Path);
        Assert.Equal("CFG-SET-007", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportDeterministicError_WhenLoggingFileNameIsReservedWindowsDeviceName()
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
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = "CON.log",
                MaxFileSizeMb = 10,
                RetainedFileCount = 10,
                Level = "warning"
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.logging.file_name", error.Path);
        Assert.Equal("CFG-SET-007", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportMissingSection_WhenLoggingSectionNull()
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
            Runtime = baseline.Runtime,
            Logging = null
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        ValidationError error = Assert.Single(result.Errors);
        Assert.Equal("$.logging", error.Path);
        Assert.Equal("CFG-SET-001", error.Code);
    }

    [Fact]
    public void Validate_ShouldReportRangeAndMissingErrors_WhenLoggingFieldsInvalid()
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
            Runtime = baseline.Runtime,
            Logging = new SettingsLoggingSection
            {
                FileName = " ",
                MaxFileSizeMb = 0,
                RetainedFileCount = null,
                Level = null
            }
        };

        ValidationResult result = validator.Validate(document, "settings.yml");

        Assert.Contains(result.Errors, error => error.Path == "$.logging.file_name" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.logging.max_file_size_mb" && error.Code == "CFG-SET-004");
        Assert.Contains(result.Errors, error => error.Path == "$.logging.retained_file_count" && error.Code == "CFG-SET-002");
        Assert.Contains(result.Errors, error => error.Path == "$.logging.level" && error.Code == "CFG-SET-002");
    }
}
