namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Tests settings self-healing behavior and guard paths.
/// </summary>
public sealed class SettingsSelfHealingServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenParserIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new SettingsSelfHealingService(null!));
    }

    [Fact]
    public void SelfHeal_ShouldPatchMissingValues_WhenDocumentIsIncomplete()
    {
        using TemporaryDirectory tempDirectory = new();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");

        File.WriteAllText(
            settingsPath,
            """
            paths:
              config_root_path: /custom/config
            scan:
              merge_interval_seconds: 60
            """);

        SettingsSelfHealingService service = new(new YamlDocumentParser());

        SettingsSelfHealingResult result = service.SelfHeal(settingsPath);

        Assert.True(result.WasHealed);
        Assert.Equal("/custom/config", result.Document.Paths!.ConfigRootPath);
        Assert.Equal("/ssm/sources", result.Document.Paths.SourcesRootPath);
        Assert.Equal(60, result.Document.Scan!.MergeIntervalSeconds);
        Assert.Equal(5, result.Document.Scan.MergeTriggerPollSeconds);
        Assert.Equal(300, result.Document.Scan.MergeTriggerRequestTimeoutBufferSeconds);
        Assert.Equal("progressive", result.Document.Scan.WatchStartupMode);
        Assert.NotNull(result.Document.Runtime);
        Assert.NotNull(result.Document.Shutdown);
        Assert.Equal("daemon.log", result.Document.Logging!.FileName);
        Assert.False(result.Document.Shutdown!.CleanupApplyHighPriority);
        Assert.Equal(3, result.Document.Shutdown!.CleanupPriorityIoniceClass);
        Assert.Equal(-20, result.Document.Shutdown.CleanupPriorityNiceValue);
    }

    [Fact]
    public void SelfHeal_ShouldNotPatch_WhenDocumentAlreadyComplete()
    {
        using TemporaryDirectory tempDirectory = new();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");

        File.WriteAllText(settingsPath, CreateValidSettingsYaml(includeUnknownKey: true));

        SettingsSelfHealingService service = new(new YamlDocumentParser());

        SettingsSelfHealingResult result = service.SelfHeal(settingsPath);

        Assert.False(result.WasHealed);
        Assert.Equal("text", result.Document.Runtime!.DetailsDescriptionMode);
        Assert.Equal("/ssm/config", result.Document.Paths!.ConfigRootPath);
        Assert.Equal("warning", result.Document.Logging!.Level);
    }

    [Fact]
    public void SelfHeal_ShouldThrowConfigurationBootstrapException_WhenYamlIsMalformed()
    {
        using TemporaryDirectory tempDirectory = new();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");

        File.WriteAllText(settingsPath, "paths: [");

        SettingsSelfHealingService service = new(new YamlDocumentParser());

        ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(() => service.SelfHeal(settingsPath));

        Assert.Contains(exception.ValidationErrors, error => error.Code == "CFG-YAML-001");
    }

    [Fact]
    public void SelfHeal_ShouldThrowConfigurationBootstrapException_WhenYamlIsEmptyDocument()
    {
        using TemporaryDirectory tempDirectory = new();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");
        File.WriteAllText(settingsPath, string.Empty);

        SettingsSelfHealingService service = new(new YamlDocumentParser());

        ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(() => service.SelfHeal(settingsPath));

        Assert.Contains(exception.ValidationErrors, error => error.Code == "CFG-YAML-002");
    }

    [Fact]
    public void SelfHeal_ShouldThrowFileNotFoundException_WhenFileDoesNotExist()
    {
        SettingsSelfHealingService service = new(new YamlDocumentParser());

        Assert.Throws<FileNotFoundException>(() => service.SelfHeal(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.yml")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SelfHeal_ShouldThrowArgumentException_WhenPathIsNullOrWhitespace(string? path)
    {
        SettingsSelfHealingService service = new(new YamlDocumentParser());

        Assert.ThrowsAny<ArgumentException>(() => service.SelfHeal(path!));
    }

    private static string CreateValidSettingsYaml(bool includeUnknownKey)
    {
        string unknown = includeUnknownKey
            ? "\nunexpected_runtime_hint: keep_me\n"
            : "\n";

        return
            """
            paths:
              config_root_path: /ssm/config
              sources_root_path: /ssm/sources
              override_root_path: /ssm/override
              merged_root_path: /ssm/merged
              state_root_path: /ssm/state
              log_root_path: /ssm/config
              branch_links_root_path: /ssm/state/.mergerfs-branches
              unraid_cache_pool_name: ""
            scan:
              merge_interval_seconds: 3600
              merge_trigger_poll_seconds: 5
              merge_min_seconds_between_scans: 15
              merge_lock_retry_seconds: 30
              merge_trigger_request_timeout_buffer_seconds: 300
              watch_startup_mode: progressive
            rename:
              rename_delay_seconds: 300
              rename_quiet_seconds: 120
              rename_poll_seconds: 20
              rename_rescan_seconds: 172800
            diagnostics:
              debug_timing: true
              debug_timing_top_n: 15
              debug_timing_min_item_ms: 250
              debug_timing_slow_ms: 5000
              debug_timing_live: true
              debug_scan_progress_every: 250
              debug_scan_progress_seconds: 60
              debug_comic_info: false
              timeout_poll_ms: 100
              timeout_poll_ms_fast: 10
            shutdown:
              unmount_on_exit: true
              stop_timeout_seconds: 120
              child_exit_grace_seconds: 5
              unmount_command_timeout_seconds: 8
              unmount_detach_wait_seconds: 5
              cleanup_high_priority: true
              cleanup_apply_high_priority: false
              cleanup_priority_ionice_class: 3
              cleanup_priority_nice_value: -20
            permissions:
              inherit_from_parent: true
              enforce_existing: false
              reference_path: /ssm/sources
            runtime:
              low_priority: true
              startup_cleanup: true
              rescan_now: true
              enable_mount_healthcheck: false
              max_consecutive_mount_failures: 5
              details_description_mode: text
              mergerfs_options_base: allow_other,default_permissions,use_ino,category.create=ff,cache.entry=0,cache.attr=0,cache.negative_entry=0
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            """ + unknown;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ssm-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path
        {
            get;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
