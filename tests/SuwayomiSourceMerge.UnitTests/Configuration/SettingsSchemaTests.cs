namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Tests settings schema parsing and validation rules.
/// </summary>
public sealed class SettingsSchemaTests
{
    [Fact]
    public void ParseSettings_ShouldPassForValidDocument()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: text
              mergerfs_options_base: allow_other,default_permissions,use_ino,threads=1,category.create=ff,cache.entry=0,cache.attr=0,cache.negative_entry=0
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            """);

        Assert.True(parsed.Validation.IsValid);
        Assert.NotNull(parsed.Document);
    }

    [Fact]
    public void ParseSettings_ShouldAllowZeroForNonNegativeFields()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              merge_min_seconds_between_scans: 0
              merge_lock_retry_seconds: 30
              merge_trigger_request_timeout_buffer_seconds: 300
            rename:
              rename_delay_seconds: 0
              rename_quiet_seconds: 0
              rename_poll_seconds: 20
              rename_rescan_seconds: 172800
            diagnostics:
              debug_timing: true
              debug_timing_top_n: 15
              debug_timing_min_item_ms: 0
              debug_timing_slow_ms: 5000
              debug_timing_live: true
              debug_scan_progress_every: 0
              debug_scan_progress_seconds: 0
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: html
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 1
              retained_file_count: 1
              level: trace
            """);

        Assert.True(parsed.Validation.IsValid);
    }

    [Fact]
    public void ParseSettings_ShouldFailForInvalidDetailsMode()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: markdown
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-SET-005", error.Code);
        Assert.Equal("$.runtime.details_description_mode", error.Path);
    }

    [Fact]
    public void ParseSettings_ShouldFailForInvalidLoggingLevel()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: information
            """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-SET-005", error.Code);
        Assert.Equal("$.logging.level", error.Path);
    }

    [Fact]
    public void ParseSettings_ShouldFailForInvalidLoggingFileName()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: ../daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-SET-007", error.Code);
        Assert.Equal("$.logging.file_name", error.Path);
    }

    [Fact]
    public void ParseSettings_ShouldFailWhenLoggingSectionMissing()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-SET-001", error.Code);
        Assert.Equal("$.logging", error.Path);
    }

    [Fact]
    public void ParseSettings_ShouldAllowUnknownTopLevelKeys_WhenDocumentOtherwiseValid()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            unknown_top_level: true
            """);

        Assert.True(parsed.Validation.IsValid);
        Assert.NotNull(parsed.Document);
    }

    [Fact]
    public void ParseSettings_ShouldFailForInvalidLoggingShape()
    {
        ConfigurationSchemaService service = ConfigurationSchemaServiceFactory.Create();

        ParsedDocument<SuwayomiSourceMerge.Configuration.Documents.SettingsDocument> parsed = service.ParseSettings(
            "settings.yml",
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
              comick_metadata_cooldown_hours: 24
              flaresolverr_server_url: ''
              flaresolverr_direct_retry_minutes: 60
              preferred_language: en
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging: []
            """);

        var error = Assert.Single(parsed.Validation.Errors);
        Assert.Equal("CFG-YAML-001", error.Code);
    }
}
