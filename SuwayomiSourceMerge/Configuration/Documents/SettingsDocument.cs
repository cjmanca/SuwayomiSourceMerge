namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Root model for <c>settings.yml</c>.
/// </summary>
/// <remarks>
/// Each section maps directly to a top-level YAML key and is validated by
/// <c>SettingsDocumentValidator</c> during bootstrap.
/// Consumers should treat this as a data-transfer model and keep operational logic in dedicated
/// services/validators.
/// </remarks>
public sealed class SettingsDocument
{
	/// <summary>
	/// Gets or sets filesystem path configuration used by runtime services.
	/// </summary>
	public SettingsPathsSection? Paths
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets merge scan timing and scheduling values.
	/// </summary>
	public SettingsScanSection? Scan
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets chapter rename timing values.
	/// </summary>
	public SettingsRenameSection? Rename
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets diagnostics and debug instrumentation settings.
	/// </summary>
	public SettingsDiagnosticsSection? Diagnostics
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets shutdown and cleanup behavior.
	/// </summary>
	public SettingsShutdownSection? Shutdown
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets permission inheritance and enforcement behavior.
	/// </summary>
	public SettingsPermissionsSection? Permissions
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets runtime feature toggles and mergerfs options.
	/// </summary>
	public SettingsRuntimeSection? Runtime
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets logging output, retention, and level settings.
	/// </summary>
	public SettingsLoggingSection? Logging
	{
		get; init;
	}
}

/// <summary>
/// Path section for runtime directories and mount roots.
/// </summary>
public sealed class SettingsPathsSection
{
	/// <summary>
	/// Gets or sets the absolute root path containing configuration files.
	/// </summary>
	public string? ConfigRootPath
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the absolute root path containing source volumes.
	/// </summary>
	public string? SourcesRootPath
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the absolute root path containing override volumes.
	/// </summary>
	public string? OverrideRootPath
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the absolute root path for merged output mounts.
	/// </summary>
	public string? MergedRootPath
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the absolute root path used for runtime state files.
	/// </summary>
	public string? StateRootPath
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the absolute root path where log files are written.
	/// </summary>
	public string? LogRootPath
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the absolute root path for mergerfs branch-link staging.
	/// </summary>
	public string? BranchLinksRootPath
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the optional Unraid cache pool name used by shell parity features.
	/// </summary>
	public string? UnraidCachePoolName
	{
		get; init;
	}
}

/// <summary>
/// Scan section controlling merge trigger cadence.
/// </summary>
public sealed class SettingsScanSection
{
	/// <summary>
	/// Gets or sets the periodic full merge interval in seconds.
	/// </summary>
	public int? MergeIntervalSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the trigger polling interval in seconds.
	/// </summary>
	public int? MergeTriggerPollSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the minimum seconds between merge executions.
	/// </summary>
	public int? MergeMinSecondsBetweenScans
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the retry delay in seconds when a merge lock is unavailable.
	/// </summary>
	public int? MergeLockRetrySeconds
	{
		get; init;
	}
}

/// <summary>
/// Rename section controlling chapter rename queue behavior.
/// </summary>
public sealed class SettingsRenameSection
{
	/// <summary>
	/// Gets or sets seconds to delay before processing rename candidates.
	/// </summary>
	public int? RenameDelaySeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets required quiet period in seconds before renaming.
	/// </summary>
	public int? RenameQuietSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets polling interval in seconds for rename work.
	/// </summary>
	public int? RenamePollSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets fallback rescan interval in seconds for rename detection.
	/// </summary>
	public int? RenameRescanSeconds
	{
		get; init;
	}
}

/// <summary>
/// Diagnostics section controlling debug logging and timing telemetry.
/// </summary>
public sealed class SettingsDiagnosticsSection
{
	/// <summary>
	/// Gets or sets whether timing diagnostics are enabled.
	/// </summary>
	public bool? DebugTiming
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the number of top timing entries to include in reports.
	/// </summary>
	public int? DebugTimingTopN
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the minimum item duration in milliseconds to include in timing output.
	/// </summary>
	public int? DebugTimingMinItemMs
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the threshold in milliseconds used to classify operations as slow.
	/// </summary>
	public int? DebugTimingSlowMs
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets whether live timing output is emitted while operations are running.
	/// </summary>
	public bool? DebugTimingLive
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets progress logging cadence by processed item count.
	/// </summary>
	public int? DebugScanProgressEvery
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets progress logging cadence in seconds.
	/// </summary>
	public int? DebugScanProgressSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets whether ComicInfo parsing diagnostics are enabled.
	/// </summary>
	public bool? DebugComicInfo
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets standard timeout polling interval in milliseconds.
	/// </summary>
	public int? TimeoutPollMs
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets fast timeout polling interval in milliseconds for latency-sensitive paths.
	/// </summary>
	public int? TimeoutPollMsFast
	{
		get; init;
	}
}

/// <summary>
/// Shutdown section controlling process stop and unmount behavior.
/// </summary>
public sealed class SettingsShutdownSection
{
	/// <summary>
	/// Gets or sets whether mounted paths should be unmounted on exit.
	/// </summary>
	public bool? UnmountOnExit
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets overall shutdown timeout in seconds.
	/// </summary>
	public int? StopTimeoutSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets grace period in seconds to wait for child process exit.
	/// </summary>
	public int? ChildExitGraceSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets timeout in seconds for unmount command execution.
	/// </summary>
	public int? UnmountCommandTimeoutSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets wait time in seconds after detach-style unmount operations.
	/// </summary>
	public int? UnmountDetachWaitSeconds
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets whether cleanup commands should run at high priority.
	/// </summary>
	public bool? CleanupHighPriority
	{
		get; init;
	}
}

/// <summary>
/// Permissions section controlling inheritance and enforcement logic.
/// </summary>
public sealed class SettingsPermissionsSection
{
	/// <summary>
	/// Gets or sets whether permission values should be inherited from parent directories.
	/// </summary>
	public bool? InheritFromParent
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets whether permissions should be enforced on existing files.
	/// </summary>
	public bool? EnforceExisting
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the absolute reference path used to derive permission baselines.
	/// </summary>
	public string? ReferencePath
	{
		get; init;
	}
}

/// <summary>
/// Runtime section for toggles and mergerfs composition options.
/// </summary>
public sealed class SettingsRuntimeSection
{
	/// <summary>
	/// Gets or sets whether runtime processing should prefer lower OS scheduling priority.
	/// </summary>
	public bool? LowPriority
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets whether startup cleanup should run before normal processing.
	/// </summary>
	public bool? StartupCleanup
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets whether an immediate rescan should be triggered at startup.
	/// </summary>
	public bool? RescanNow
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets whether periodic mount health checks are enabled.
	/// </summary>
	public bool? EnableMountHealthcheck
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the rendering mode for details description output.
	/// </summary>
	public string? DetailsDescriptionMode
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the base mergerfs options string applied when mounts are created.
	/// </summary>
	public string? MergerfsOptionsBase
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets source names to exclude from processing.
	/// </summary>
	public List<string>? ExcludedSources
	{
		get; init;
	}
}

/// <summary>
/// Logging section controlling log destination, retention, and verbosity.
/// </summary>
public sealed class SettingsLoggingSection
{
	/// <summary>
	/// Gets or sets the active log file name relative to <c>paths.log_root_path</c>.
	/// </summary>
	public string? FileName
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the active log file size limit in MiB before rotation.
	/// </summary>
	public int? MaxFileSizeMb
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the number of rotated log archives to retain.
	/// </summary>
	public int? RetainedFileCount
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets the minimum enabled log level token.
	/// </summary>
	public string? Level
	{
		get; init;
	}
}
