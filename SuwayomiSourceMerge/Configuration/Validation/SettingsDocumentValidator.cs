using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="SettingsDocument"/> instances.
/// </summary>
public sealed class SettingsDocumentValidator : IConfigValidator<SettingsDocument>
{
    private const string MissingSectionCode = "CFG-SET-001";
    private const string MissingFieldCode = "CFG-SET-002";
    private const string EmptyPathCode = "CFG-SET-003";
    private const string InvalidRangeCode = "CFG-SET-004";
    private const string InvalidEnumCode = "CFG-SET-005";
    private const string DuplicateListCode = "CFG-SET-006";
    /// <inheritdoc />
    public ValidationResult Validate(SettingsDocument document, string file)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(file);

        ValidationResult result = new();

        ValidateRequiredSection(document.Paths, file, "$.paths", result);
        ValidateRequiredSection(document.Scan, file, "$.scan", result);
        ValidateRequiredSection(document.Rename, file, "$.rename", result);
        ValidateRequiredSection(document.Diagnostics, file, "$.diagnostics", result);
        ValidateRequiredSection(document.Shutdown, file, "$.shutdown", result);
        ValidateRequiredSection(document.Permissions, file, "$.permissions", result);
        ValidateRequiredSection(document.Runtime, file, "$.runtime", result);
        ValidateRequiredSection(document.Logging, file, "$.logging", result);

        if (document.Paths is not null)
        {
            ValidateRequiredPath(document.Paths.ConfigRootPath, file, "$.paths.config_root_path", result);
            ValidateRequiredPath(document.Paths.SourcesRootPath, file, "$.paths.sources_root_path", result);
            ValidateRequiredPath(document.Paths.OverrideRootPath, file, "$.paths.override_root_path", result);
            ValidateRequiredPath(document.Paths.MergedRootPath, file, "$.paths.merged_root_path", result);
            ValidateRequiredPath(document.Paths.StateRootPath, file, "$.paths.state_root_path", result);
            ValidateRequiredPath(document.Paths.LogRootPath, file, "$.paths.log_root_path", result);
            ValidateRequiredPath(document.Paths.BranchLinksRootPath, file, "$.paths.branch_links_root_path", result);
        }

        if (document.Scan is not null)
        {
            ValidatePositive(document.Scan.MergeIntervalSeconds, file, "$.scan.merge_interval_seconds", result);
            ValidatePositive(document.Scan.MergeTriggerPollSeconds, file, "$.scan.merge_trigger_poll_seconds", result);
            ValidateNonNegative(document.Scan.MergeMinSecondsBetweenScans, file, "$.scan.merge_min_seconds_between_scans", result);
            ValidatePositive(document.Scan.MergeLockRetrySeconds, file, "$.scan.merge_lock_retry_seconds", result);
        }

        if (document.Rename is not null)
        {
            ValidateNonNegative(document.Rename.RenameDelaySeconds, file, "$.rename.rename_delay_seconds", result);
            ValidateNonNegative(document.Rename.RenameQuietSeconds, file, "$.rename.rename_quiet_seconds", result);
            ValidatePositive(document.Rename.RenamePollSeconds, file, "$.rename.rename_poll_seconds", result);
            ValidatePositive(document.Rename.RenameRescanSeconds, file, "$.rename.rename_rescan_seconds", result);
        }

        if (document.Diagnostics is not null)
        {
            ValidateRequired(document.Diagnostics.DebugTiming, file, "$.diagnostics.debug_timing", result);
            ValidatePositive(document.Diagnostics.DebugTimingTopN, file, "$.diagnostics.debug_timing_top_n", result);
            ValidateNonNegative(document.Diagnostics.DebugTimingMinItemMs, file, "$.diagnostics.debug_timing_min_item_ms", result);
            ValidatePositive(document.Diagnostics.DebugTimingSlowMs, file, "$.diagnostics.debug_timing_slow_ms", result);
            ValidateRequired(document.Diagnostics.DebugTimingLive, file, "$.diagnostics.debug_timing_live", result);
            ValidateNonNegative(document.Diagnostics.DebugScanProgressEvery, file, "$.diagnostics.debug_scan_progress_every", result);
            ValidateNonNegative(document.Diagnostics.DebugScanProgressSeconds, file, "$.diagnostics.debug_scan_progress_seconds", result);
            ValidateRequired(document.Diagnostics.DebugComicInfo, file, "$.diagnostics.debug_comic_info", result);
            ValidatePositive(document.Diagnostics.TimeoutPollMs, file, "$.diagnostics.timeout_poll_ms", result);
            ValidatePositive(document.Diagnostics.TimeoutPollMsFast, file, "$.diagnostics.timeout_poll_ms_fast", result);
        }

        if (document.Shutdown is not null)
        {
            ValidateRequired(document.Shutdown.UnmountOnExit, file, "$.shutdown.unmount_on_exit", result);
            ValidatePositive(document.Shutdown.StopTimeoutSeconds, file, "$.shutdown.stop_timeout_seconds", result);
            ValidatePositive(document.Shutdown.ChildExitGraceSeconds, file, "$.shutdown.child_exit_grace_seconds", result);
            ValidatePositive(document.Shutdown.UnmountCommandTimeoutSeconds, file, "$.shutdown.unmount_command_timeout_seconds", result);
            ValidatePositive(document.Shutdown.UnmountDetachWaitSeconds, file, "$.shutdown.unmount_detach_wait_seconds", result);
            ValidateRequired(document.Shutdown.CleanupHighPriority, file, "$.shutdown.cleanup_high_priority", result);
        }

        if (document.Permissions is not null)
        {
            ValidateRequired(document.Permissions.InheritFromParent, file, "$.permissions.inherit_from_parent", result);
            ValidateRequired(document.Permissions.EnforceExisting, file, "$.permissions.enforce_existing", result);
            ValidateRequiredPath(document.Permissions.ReferencePath, file, "$.permissions.reference_path", result);
        }

        if (document.Runtime is not null)
        {
            ValidateRequired(document.Runtime.LowPriority, file, "$.runtime.low_priority", result);
            ValidateRequired(document.Runtime.StartupCleanup, file, "$.runtime.startup_cleanup", result);
            ValidateRequired(document.Runtime.RescanNow, file, "$.runtime.rescan_now", result);
            ValidateRequired(document.Runtime.EnableMountHealthcheck, file, "$.runtime.enable_mount_healthcheck", result);
            ValidateRequired(document.Runtime.MergerfsOptionsBase, file, "$.runtime.mergerfs_options_base", result);
            ValidateDetailsDescriptionMode(document.Runtime.DetailsDescriptionMode, file, "$.runtime.details_description_mode", result);
            ValidateExcludedSources(document.Runtime.ExcludedSources, file, "$.runtime.excluded_sources", result);
        }

        if (document.Logging is not null)
        {
            ValidateRequired(document.Logging.FileName, file, "$.logging.file_name", result);
            ValidatePositive(document.Logging.MaxFileSizeMb, file, "$.logging.max_file_size_mb", result);
            ValidatePositive(document.Logging.RetainedFileCount, file, "$.logging.retained_file_count", result);
            ValidateLogLevel(document.Logging.Level, file, "$.logging.level", result);
        }

        return result;
    }

    private static void ValidateRequiredSection(object? value, string file, string path, ValidationResult result)
    {
        if (value is null)
        {
            result.Add(new ValidationError(file, path, MissingSectionCode, "Required section is missing."));
        }
    }

    private static void ValidateRequired<T>(T? value, string file, string path, ValidationResult result)
        where T : struct
    {
        if (!value.HasValue)
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required field is missing."));
        }
    }

    private static void ValidateRequired(string? value, string file, string path, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required field is missing."));
        }
    }

    private static void ValidateRequiredPath(string? value, string file, string path, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required path is missing."));
            return;
        }

        if (!Path.IsPathRooted(value))
        {
            result.Add(new ValidationError(file, path, EmptyPathCode, "Path must be absolute."));
        }
    }

    private static void ValidatePositive(int? value, string file, string path, ValidationResult result)
    {
        if (!value.HasValue)
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required numeric field is missing."));
            return;
        }

        if (value.Value <= 0)
        {
            result.Add(new ValidationError(file, path, InvalidRangeCode, "Value must be greater than 0."));
        }
    }

    private static void ValidateNonNegative(int? value, string file, string path, ValidationResult result)
    {
        if (!value.HasValue)
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required numeric field is missing."));
            return;
        }

        if (value.Value < 0)
        {
            result.Add(new ValidationError(file, path, InvalidRangeCode, "Value must be greater than or equal to 0."));
        }
    }

    private static void ValidateDetailsDescriptionMode(string? value, string file, string path, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required field is missing."));
            return;
        }

        string mode = value.Trim().ToLowerInvariant();
        if (mode != "text" && mode != "br" && mode != "html")
        {
            result.Add(new ValidationError(file, path, InvalidEnumCode, "Allowed values: text, br, html."));
        }
    }

    private static void ValidateLogLevel(string? value, string file, string path, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required field is missing."));
            return;
        }

        if (!LogLevelParser.TryParse(value, out _))
        {
            result.Add(
                new ValidationError(
                    file,
                    path,
                    InvalidEnumCode,
                    $"Allowed values: {LogLevelParser.SupportedValuesDisplay}."));
        }
    }

    private static void ValidateExcludedSources(List<string>? values, string file, string path, ValidationResult result)
    {
        if (values is null)
        {
            result.Add(new ValidationError(file, path, MissingFieldCode, "Required list is missing."));
            return;
        }

        HashSet<string> seen = new(StringComparer.Ordinal);

        for (int i = 0; i < values.Count; i++)
        {
            string? value = values[i];
            string itemPath = $"{path}[{i}]";

            if (string.IsNullOrWhiteSpace(value))
            {
                result.Add(new ValidationError(file, itemPath, MissingFieldCode, "List item must not be empty."));
                continue;
            }

            string key = ValidationKeyNormalizer.NormalizeTokenKey(value);
            if (!seen.Add(key))
            {
                result.Add(new ValidationError(file, itemPath, DuplicateListCode, "Duplicate excluded source value."));
            }
        }
    }
}
