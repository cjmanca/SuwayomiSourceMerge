namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Verifies strict-runtime and relaxed-tooling schema parse entrypoints for settings.
/// </summary>
public sealed class SettingsSchemaToolingProfileTests
{
    [Fact]
    public void ParseSettingsForRuntime_Failure_ShouldRequireShutdownCleanupProfileFields_WhenUsingStrictRuntimeEntryPoint()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettingsForRuntime(
            "settings.yml",
            CreateSettingsYamlWithoutProfileFields());

        Assert.Contains(parsed.Validation.Errors, error => error.Path == "$.shutdown.cleanup_apply_high_priority" && error.Code == "CFG-SET-002");
        Assert.Contains(parsed.Validation.Errors, error => error.Path == "$.shutdown.cleanup_priority_ionice_class" && error.Code == "CFG-SET-002");
        Assert.Contains(parsed.Validation.Errors, error => error.Path == "$.shutdown.cleanup_priority_nice_value" && error.Code == "CFG-SET-002");
    }

    [Fact]
    public void ParseSettingsForTooling_Expected_ShouldAllowMissingShutdownCleanupProfileFields_WhenOtherwiseValid()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettingsForTooling(
            "settings.yml",
            CreateSettingsYamlWithoutProfileFields());

        Assert.True(parsed.Validation.IsValid);
        Assert.NotNull(parsed.Document);
    }

    [Fact]
    public void ParseSettingsForTooling_Failure_ShouldValidateRanges_WhenOptionalProfileFieldsArePresent()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettingsForTooling(
            "settings.yml",
            CreateSettingsYamlWithInvalidOptionalProfileRanges());

        Assert.Contains(parsed.Validation.Errors, error => error.Path == "$.shutdown.cleanup_priority_ionice_class" && error.Code == "CFG-SET-004");
        Assert.Contains(parsed.Validation.Errors, error => error.Path == "$.shutdown.cleanup_priority_nice_value" && error.Code == "CFG-SET-004");
        Assert.DoesNotContain(parsed.Validation.Errors, error => error.Path == "$.shutdown.cleanup_apply_high_priority" && error.Code == "CFG-SET-002");
    }

    private static string CreateSettingsYamlWithoutProfileFields()
    {
        return """
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
            permissions:
              inherit_from_parent: true
              enforce_existing: false
              reference_path: /ssm/sources
            runtime:
              low_priority: true
              startup_cleanup: true
              rescan_now: true
              enable_mount_healthcheck: false
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            """;
    }

    private static string CreateSettingsYamlWithInvalidOptionalProfileRanges()
    {
        return """
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
              cleanup_priority_ionice_class: 9
              cleanup_priority_nice_value: 25
            permissions:
              inherit_from_parent: true
              enforce_existing: false
              reference_path: /ssm/sources
            runtime:
              low_priority: true
              startup_cleanup: true
              rescan_now: true
              enable_mount_healthcheck: false
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            """;
    }
}
