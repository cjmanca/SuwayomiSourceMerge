namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Tests configuration bootstrap and legacy migration behavior.
/// </summary>
public sealed class ConfigurationBootstrapServiceTests
{
    [Fact]
    public void Bootstrap_ShouldCreateMissingYamlAndMigrateLegacyTxt_WhenLegacyFilesExist()
    {
        using TemporaryDirectory tempDirectory = new();

        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "manga_equivalents.txt"),
            "Manga One|Manga 1|The Manga One\nAnother Manga|Alt Name\n");
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "source_priority.txt"),
            "Source A\nSource B\n");

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Documents.MangaEquivalents.Groups!.Count);
        Assert.Equal("Manga One", result.Documents.MangaEquivalents.Groups[0].Canonical);
        Assert.Equal(["Manga 1", "The Manga One"], result.Documents.MangaEquivalents.Groups[0].Aliases);
        Assert.Equal(["Source A", "Source B"], result.Documents.SourcePriority.Sources);

        ConfigurationBootstrapFileState mangaState = Assert.Single(result.Files, file => file.FileName == "manga_equivalents.yml");
        Assert.True(mangaState.WasCreated);
        Assert.True(mangaState.WasMigrated);
        Assert.False(mangaState.UsedDefaults);

        ConfigurationBootstrapFileState sourceState = Assert.Single(result.Files, file => file.FileName == "source_priority.yml");
        Assert.True(sourceState.WasCreated);
        Assert.True(sourceState.WasMigrated);
        Assert.False(sourceState.UsedDefaults);

        ConfigurationBootstrapFileState settingsState = Assert.Single(result.Files, file => file.FileName == "settings.yml");
        Assert.True(settingsState.WasCreated);
        Assert.True(settingsState.UsedDefaults);
        Assert.False(settingsState.WasSelfHealed);

        ConfigurationBootstrapFileState tagsState = Assert.Single(result.Files, file => file.FileName == "scene_tags.yml");
        Assert.True(tagsState.WasCreated);
        Assert.True(tagsState.UsedDefaults);
        Assert.False(tagsState.WasSelfHealed);
    }

    [Fact]
    public void Bootstrap_ShouldOnlyCreateMissingYaml_WhenSomeYamlAlreadyExist()
    {
        using TemporaryDirectory tempDirectory = new();

        string existingSceneTags = "tags:\n  - custom\n";
        string existingSourcePriority = "sources:\n  - Existing Source\n";

        string sceneTagsPath = Path.Combine(tempDirectory.Path, "scene_tags.yml");
        string sourcePriorityPath = Path.Combine(tempDirectory.Path, "source_priority.yml");

        File.WriteAllText(sceneTagsPath, existingSceneTags);
        File.WriteAllText(sourcePriorityPath, existingSourcePriority);

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);

        Assert.Equal(existingSceneTags, File.ReadAllText(sceneTagsPath));
        Assert.Equal(existingSourcePriority, File.ReadAllText(sourcePriorityPath));

        Assert.Equal(["custom"], result.Documents.SceneTags.Tags);
        Assert.Equal(["Existing Source"], result.Documents.SourcePriority.Sources);
        Assert.Empty(result.Documents.MangaEquivalents.Groups!);
        Assert.NotNull(result.Documents.Settings.Paths);

        ConfigurationBootstrapFileState sceneState = Assert.Single(result.Files, file => file.FileName == "scene_tags.yml");
        Assert.False(sceneState.WasCreated);
        Assert.False(sceneState.WasMigrated);
        Assert.False(sceneState.UsedDefaults);

        ConfigurationBootstrapFileState sourceState = Assert.Single(result.Files, file => file.FileName == "source_priority.yml");
        Assert.False(sourceState.WasCreated);
        Assert.False(sourceState.WasMigrated);
        Assert.False(sourceState.UsedDefaults);

        ConfigurationBootstrapFileState settingsState = Assert.Single(result.Files, file => file.FileName == "settings.yml");
        Assert.True(settingsState.WasCreated);
        Assert.False(settingsState.WasMigrated);
        Assert.True(settingsState.UsedDefaults);
        Assert.False(settingsState.WasSelfHealed);

        ConfigurationBootstrapFileState mangaState = Assert.Single(result.Files, file => file.FileName == "manga_equivalents.yml");
        Assert.True(mangaState.WasCreated);
        Assert.False(mangaState.WasMigrated);
        Assert.True(mangaState.UsedDefaults);
        Assert.False(mangaState.WasSelfHealed);
    }

    [Fact]
    public void Bootstrap_ShouldThrow_WhenExistingYamlIsInvalid()
    {
        using TemporaryDirectory tempDirectory = new();

        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "settings.yml"),
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

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(() => service.Bootstrap(tempDirectory.Path));

        Assert.Contains(
            exception.ValidationErrors,
            error => error.Code == "CFG-SET-005");
    }

    [Fact]
    public void Bootstrap_ShouldSkipMalformedLegacyMangaLinesAndReturnWarnings()
    {
        using TemporaryDirectory tempDirectory = new();

        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "manga_equivalents.txt"),
            "# comment\n|Alias without canonical\nValid Canonical|Alias One\n");
        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "source_priority.txt"),
            "Source A\n");

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);

        ConfigurationBootstrapWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("CFG-MIG-001", warning.Code);
        Assert.Equal("manga_equivalents.txt", warning.File);
        Assert.Equal(2, warning.Line);

        List<SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentGroup> groups = result.Documents.MangaEquivalents.Groups!;
        Assert.Single(groups);
        Assert.Equal("Valid Canonical", groups[0].Canonical);
        Assert.Equal(["Alias One"], groups[0].Aliases);
    }

    [Fact]
    public void Bootstrap_ShouldThrow_WhenMigratedLegacyMangaMappingsConflict()
    {
        using TemporaryDirectory tempDirectory = new();

        File.WriteAllText(
            Path.Combine(tempDirectory.Path, "manga_equivalents.txt"),
            "Manga Alpha|Shared Alias\nManga Beta|Shared Alias\n");

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapException exception = Assert.Throws<ConfigurationBootstrapException>(() => service.Bootstrap(tempDirectory.Path));

        Assert.Contains(exception.ValidationErrors, error => error.Code == "CFG-MEQ-005");
    }

    [Fact]
    public void Bootstrap_ShouldNotModifyLegacyTxtFiles_WhenMigrationSucceeds()
    {
        using TemporaryDirectory tempDirectory = new();

        string mangaLegacyContent = "Canon A|Alias A\n";
        string sourceLegacyContent = "Source A\n";

        string mangaLegacyPath = Path.Combine(tempDirectory.Path, "manga_equivalents.txt");
        string sourceLegacyPath = Path.Combine(tempDirectory.Path, "source_priority.txt");

        File.WriteAllText(mangaLegacyPath, mangaLegacyContent);
        File.WriteAllText(sourceLegacyPath, sourceLegacyContent);

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        _ = service.Bootstrap(tempDirectory.Path);

        Assert.True(File.Exists(mangaLegacyPath));
        Assert.True(File.Exists(sourceLegacyPath));
        Assert.Equal(mangaLegacyContent, File.ReadAllText(mangaLegacyPath));
        Assert.Equal(sourceLegacyContent, File.ReadAllText(sourceLegacyPath));
    }

    [Fact]
    public void Bootstrap_ShouldIgnoreLegacyTxtOutsideConfigRoot()
    {
        using TemporaryDirectory tempDirectory = new();
        using TemporaryDirectory externalDirectory = new();

        File.WriteAllText(
            Path.Combine(externalDirectory.Path, "manga_equivalents.txt"),
            "External Canon|External Alias\n");
        File.WriteAllText(
            Path.Combine(externalDirectory.Path, "source_priority.txt"),
            "External Source\n");

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);

        Assert.Empty(result.Warnings);
        Assert.Empty(result.Documents.MangaEquivalents.Groups!);
        Assert.Empty(result.Documents.SourcePriority.Sources!);

        ConfigurationBootstrapFileState mangaState = Assert.Single(result.Files, file => file.FileName == "manga_equivalents.yml");
        Assert.True(mangaState.UsedDefaults);
        Assert.False(mangaState.WasMigrated);
        Assert.False(mangaState.WasSelfHealed);

        ConfigurationBootstrapFileState sourceState = Assert.Single(result.Files, file => file.FileName == "source_priority.yml");
        Assert.True(sourceState.UsedDefaults);
        Assert.False(sourceState.WasMigrated);
        Assert.False(sourceState.WasSelfHealed);
    }

    [Fact]
    public void Bootstrap_ShouldSelfHealMissingSettingsFields_AndPreserveExistingValidValues()
    {
        using TemporaryDirectory tempDirectory = new();

        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");
        string original = """
            paths:
              config_root_path: /custom/config
            scan:
              merge_interval_seconds: 900
            rename:
              rename_delay_seconds: 45
            diagnostics:
              debug_timing: true
            shutdown:
              unmount_on_exit: true
            permissions:
              inherit_from_parent: true
            runtime:
              low_priority: false
            """;
        File.WriteAllText(settingsPath, original);

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);
        string healedContent = File.ReadAllText(settingsPath);

        ConfigurationBootstrapFileState settingsState = Assert.Single(result.Files, file => file.FileName == "settings.yml");
        Assert.False(settingsState.WasCreated);
        Assert.True(settingsState.WasSelfHealed);
        Assert.True(settingsState.UsedDefaults);
        Assert.NotEqual(original, healedContent);

        Assert.Equal("/custom/config", result.Documents.Settings.Paths!.ConfigRootPath);
        Assert.Equal(900, result.Documents.Settings.Scan!.MergeIntervalSeconds);
        Assert.Equal(45, result.Documents.Settings.Rename!.RenameDelaySeconds);
        Assert.Equal(false, result.Documents.Settings.Runtime!.LowPriority);
        Assert.Equal("/ssm/sources", result.Documents.Settings.Paths!.SourcesRootPath);
        Assert.Equal(5, result.Documents.Settings.Scan!.MergeTriggerPollSeconds);
        Assert.Equal("warning", result.Documents.Settings.Logging!.Level);
    }

    [Fact]
    public void Bootstrap_ShouldSelfHealMissingSettingsSection_Runtime()
    {
        using TemporaryDirectory tempDirectory = new();

        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");
        File.WriteAllText(
            settingsPath,
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
            """);

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);

        ConfigurationBootstrapFileState settingsState = Assert.Single(result.Files, file => file.FileName == "settings.yml");
        Assert.True(settingsState.WasSelfHealed);
        Assert.NotNull(result.Documents.Settings.Runtime);
        Assert.Equal("text", result.Documents.Settings.Runtime!.DetailsDescriptionMode);
    }

    [Fact]
    public void Bootstrap_ShouldNotRewriteSettings_WhenExistingSettingsAlreadyCompleteAndValid()
    {
        using TemporaryDirectory tempDirectory = new();

        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");
        string settingsWithUnknownKey = """
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
              details_description_mode: text
              mergerfs_options_base: allow_other,default_permissions,use_ino,category.create=ff,cache.entry=0,cache.attr=0,cache.negative_entry=0
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            unexpected_runtime_hint: keep_me
            """;
        File.WriteAllText(settingsPath, settingsWithUnknownKey);

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);
        string after = File.ReadAllText(settingsPath);

        ConfigurationBootstrapFileState settingsState = Assert.Single(result.Files, file => file.FileName == "settings.yml");
        Assert.False(settingsState.WasSelfHealed);
        Assert.False(settingsState.UsedDefaults);
        Assert.Equal(settingsWithUnknownKey, after);
        Assert.Contains("unexpected_runtime_hint: keep_me", after);
    }

    [Fact]
    public void Bootstrap_ShouldDropUnknownSettingsKeys_WhenRewriteOccursForHealing()
    {
        using TemporaryDirectory tempDirectory = new();

        string settingsPath = Path.Combine(tempDirectory.Path, "settings.yml");
        File.WriteAllText(
            settingsPath,
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
              details_description_mode: text
              mergerfs_options_base: allow_other
              excluded_sources:
                - Local source
            logging:
              file_name: daemon.log
              max_file_size_mb: 10
              retained_file_count: 10
              level: warning
            unexpected_runtime_hint: drop_me
            """);

        ConfigurationBootstrapService service = ConfigurationSchemaServiceFactory.CreateBootstrapService();

        ConfigurationBootstrapResult result = service.Bootstrap(tempDirectory.Path);
        string after = File.ReadAllText(settingsPath);

        ConfigurationBootstrapFileState settingsState = Assert.Single(result.Files, file => file.FileName == "settings.yml");
        Assert.True(settingsState.WasSelfHealed);
        Assert.DoesNotContain("unexpected_runtime_hint", after);
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
