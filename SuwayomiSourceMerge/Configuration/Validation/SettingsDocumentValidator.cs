using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates <see cref="SettingsDocument"/> instances and returns deterministic
/// configuration errors suitable for startup diagnostics.
/// </summary>
/// <remarks>
/// This validator enforces required sections, required fields, numeric ranges, enum-like values,
/// and list uniqueness rules for <c>settings.yml</c>. It is intended to be called by configuration
/// bootstrap/loading flows before runtime services are created.
/// Errors are additive: validation continues after each failure so callers receive a complete list.
/// </remarks>
public sealed class SettingsDocumentValidator : IConfigValidator<SettingsDocument>
{
	/// <summary>
	/// Validation profile used by this validator instance.
	/// </summary>
	private readonly SettingsValidationProfile _profile;

	/// <summary>
	/// Initializes a new instance of the <see cref="SettingsDocumentValidator"/> class.
	/// </summary>
	/// <param name="profile">Validation profile controlling strictness for profile-gated fields.</param>
	public SettingsDocumentValidator(SettingsValidationProfile profile = SettingsValidationProfile.StrictRuntime)
	{
		if (!Enum.IsDefined(profile))
		{
			throw new ArgumentOutOfRangeException(nameof(profile), profile, "Validation profile must be a defined value.");
		}

		_profile = profile;
	}

	/// <summary>
	/// Validation code emitted when an entire required settings section is missing.
	/// </summary>
	private const string MissingSectionCode = "CFG-SET-001";

	/// <summary>
	/// Validation code emitted when a required scalar field is missing or empty.
	/// </summary>
	private const string MissingFieldCode = "CFG-SET-002";

	/// <summary>
	/// Validation code emitted when a path field is present but not absolute.
	/// </summary>
	private const string EmptyPathCode = "CFG-SET-003";

	/// <summary>
	/// Validation code emitted when numeric values violate configured bounds.
	/// </summary>
	private const string InvalidRangeCode = "CFG-SET-004";

	/// <summary>
	/// Validation code emitted when enum-like string values are not in the allowed set.
	/// </summary>
	private const string InvalidEnumCode = "CFG-SET-005";

	/// <summary>
	/// Validation code emitted when a normalized list item appears more than once.
	/// </summary>
	private const string DuplicateListCode = "CFG-SET-006";

	/// <summary>
	/// Validation code emitted when <c>logging.file_name</c> is not a safe single file name.
	/// </summary>
	private const string InvalidFileNameCode = "CFG-SET-007";
	/// <summary>
	/// Validates a settings document and accumulates all validation errors discovered in a single pass.
	/// </summary>
	/// <param name="document">
	/// Parsed <see cref="SettingsDocument"/> to validate. Must represent the full settings payload.
	/// </param>
	/// <param name="file">
	/// Logical file name used in emitted <see cref="ValidationError"/> values (for example, <c>settings.yml</c>).
	/// </param>
	/// <returns>
	/// A <see cref="ValidationResult"/> containing zero or more deterministic errors.
	/// <list type="bullet">
	/// <item><description><c>IsValid</c> is <see langword="true"/> when no errors are found.</description></item>
	/// <item><description>Each error includes file, path, code, and message for actionable diagnostics.</description></item>
	/// </list>
	/// </returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="document"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="file"/> is empty or whitespace.
	/// </exception>
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
			ValidatePositive(document.Scan.MergeTriggerRequestTimeoutBufferSeconds, file, "$.scan.merge_trigger_request_timeout_buffer_seconds", result);
			ValidateWatchStartupMode(document.Scan.WatchStartupMode, file, "$.scan.watch_startup_mode", result);
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

			if (_profile == SettingsValidationProfile.StrictRuntime)
			{
				ValidateRequired(document.Shutdown.CleanupApplyHighPriority, file, "$.shutdown.cleanup_apply_high_priority", result);
				ValidateRange(document.Shutdown.CleanupPriorityIoniceClass, 1, 3, file, "$.shutdown.cleanup_priority_ionice_class", result);
				ValidateRange(document.Shutdown.CleanupPriorityNiceValue, -20, 19, file, "$.shutdown.cleanup_priority_nice_value", result);
			}
			else
			{
				ValidateOptionalRange(document.Shutdown.CleanupPriorityIoniceClass, 1, 3, file, "$.shutdown.cleanup_priority_ionice_class", result);
				ValidateOptionalRange(document.Shutdown.CleanupPriorityNiceValue, -20, 19, file, "$.shutdown.cleanup_priority_nice_value", result);
			}
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
			ValidatePositive(document.Runtime.MaxConsecutiveMountFailures, file, "$.runtime.max_consecutive_mount_failures", result);
			ValidateRequired(document.Runtime.MergerfsOptionsBase, file, "$.runtime.mergerfs_options_base", result);
			ValidateDetailsDescriptionMode(document.Runtime.DetailsDescriptionMode, file, "$.runtime.details_description_mode", result);
			ValidateExcludedSources(document.Runtime.ExcludedSources, file, "$.runtime.excluded_sources", result);
		}

		if (document.Logging is not null)
		{
			ValidateLogFileName(document.Logging.FileName, file, "$.logging.file_name", result);
			ValidatePositive(document.Logging.MaxFileSizeMb, file, "$.logging.max_file_size_mb", result);
			ValidatePositive(document.Logging.RetainedFileCount, file, "$.logging.retained_file_count", result);
			ValidateLogLevel(document.Logging.Level, file, "$.logging.level", result);
		}

		return result;
	}

	/// <summary>
	/// Ensures a required settings section exists.
	/// </summary>
	/// <param name="value">Section instance to inspect.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the section in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateRequiredSection(object? value, string file, string path, ValidationResult result)
	{
		if (value is null)
		{
			result.Add(new ValidationError(file, path, MissingSectionCode, "Required section is missing."));
		}
	}

	/// <summary>
	/// Ensures a required nullable value type field has a value.
	/// </summary>
	/// <typeparam name="T">Value type being validated.</typeparam>
	/// <param name="value">Nullable value to validate.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateRequired<T>(T? value, string file, string path, ValidationResult result)
		where T : struct
	{
		if (!value.HasValue)
		{
			result.Add(new ValidationError(file, path, MissingFieldCode, "Required field is missing."));
		}
	}

	/// <summary>
	/// Ensures a required string field is not null, empty, or whitespace.
	/// </summary>
	/// <param name="value">String value to validate.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateRequired(string? value, string file, string path, ValidationResult result)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			result.Add(new ValidationError(file, path, MissingFieldCode, "Required field is missing."));
		}
	}

	/// <summary>
	/// Ensures a required path field exists and is absolute.
	/// </summary>
	/// <param name="value">Path value from settings.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
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

	/// <summary>
	/// Ensures a numeric field exists and is greater than zero.
	/// </summary>
	/// <param name="value">Numeric value to validate.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
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

	/// <summary>
	/// Ensures a numeric field exists and is zero or greater.
	/// </summary>
	/// <param name="value">Numeric value to validate.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
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

	/// <summary>
	/// Ensures a numeric field exists and is within the inclusive range.
	/// </summary>
	/// <param name="value">Numeric value to validate.</param>
	/// <param name="minimum">Inclusive minimum allowed value.</param>
	/// <param name="maximum">Inclusive maximum allowed value.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateRange(int? value, int minimum, int maximum, string file, string path, ValidationResult result)
	{
		if (!value.HasValue)
		{
			result.Add(new ValidationError(file, path, MissingFieldCode, "Required numeric field is missing."));
			return;
		}

		if (value.Value < minimum || value.Value > maximum)
		{
			result.Add(new ValidationError(file, path, InvalidRangeCode, $"Value must be between {minimum} and {maximum}."));
		}
	}

	/// <summary>
	/// Ensures an optional numeric field, when present, is within the inclusive range.
	/// </summary>
	/// <param name="value">Numeric value to validate.</param>
	/// <param name="minimum">Inclusive minimum allowed value.</param>
	/// <param name="maximum">Inclusive maximum allowed value.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateOptionalRange(int? value, int minimum, int maximum, string file, string path, ValidationResult result)
	{
		if (!value.HasValue)
		{
			return;
		}

		if (value.Value < minimum || value.Value > maximum)
		{
			result.Add(new ValidationError(file, path, InvalidRangeCode, $"Value must be between {minimum} and {maximum}."));
		}
	}
	/// <summary>
	/// Validates <c>runtime.details_description_mode</c> against the supported rendering modes.
	/// </summary>
	/// <param name="value">Mode value read from settings.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
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

	/// <summary>
	/// Validates <c>logging.level</c> using the shared logging level parser.
	/// </summary>
	/// <param name="value">Log level token from settings.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
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

	/// <summary>
	/// Validates optional <c>scan.watch_startup_mode</c> values.
	/// </summary>
	/// <param name="value">Watch startup mode value from settings.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateWatchStartupMode(string? value, string file, string path, ValidationResult result)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return;
		}

		string mode = value.Trim().ToLowerInvariant();
		if (mode != "full" && mode != "progressive")
		{
			result.Add(new ValidationError(file, path, InvalidEnumCode, "Allowed values: full, progressive."));
		}
	}

	/// <summary>
	/// Validates <c>logging.file_name</c> against the log file path safety policy.
	/// </summary>
	/// <param name="value">File name value from settings.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the field in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
	private static void ValidateLogFileName(string? value, string file, string path, ValidationResult result)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			result.Add(new ValidationError(file, path, MissingFieldCode, "Required field is missing."));
			return;
		}

		if (!LogFilePathPolicy.TryValidateFileName(value, out _))
		{
			result.Add(new ValidationError(file, path, InvalidFileNameCode, LogFilePathPolicy.InvalidFileNameMessage));
		}
	}

	/// <summary>
	/// Validates the <c>runtime.excluded_sources</c> list for required values and normalized uniqueness.
	/// </summary>
	/// <param name="values">Source names excluded from processing.</param>
	/// <param name="file">File name associated with the validation result.</param>
	/// <param name="path">JSON path-like location for the list in validation output.</param>
	/// <param name="result">Collector that receives validation errors.</param>
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

/// <summary>
/// Selects settings validation strictness for runtime and tooling call sites.
/// </summary>
public enum SettingsValidationProfile
{
	/// <summary>
	/// Enforces all runtime-required fields as mandatory.
	/// </summary>
	StrictRuntime,

	/// <summary>
	/// Allows self-healed runtime fields to be omitted for tooling/schema-only parsing.
	/// </summary>
	RelaxedTooling
}
