namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Represents the canonical settings document.
/// </summary>
public sealed class SettingsDocument
{
    /// <summary>
    /// Gets or sets path settings.
    /// </summary>
    public SettingsPathsSection? Paths
    {
        get; init;
    }

    /// <summary>
    /// Gets or sets merge scan settings.
    /// </summary>
    public SettingsScanSection? Scan
    {
        get; init;
    }

    /// <summary>
    /// Gets or sets chapter rename settings.
    /// </summary>
    public SettingsRenameSection? Rename
    {
        get; init;
    }

    /// <summary>
    /// Gets or sets diagnostics settings.
    /// </summary>
    public SettingsDiagnosticsSection? Diagnostics
    {
        get; init;
    }

    /// <summary>
    /// Gets or sets shutdown settings.
    /// </summary>
    public SettingsShutdownSection? Shutdown
    {
        get; init;
    }

    /// <summary>
    /// Gets or sets permission inheritance settings.
    /// </summary>
    public SettingsPermissionsSection? Permissions
    {
        get; init;
    }

    /// <summary>
    /// Gets or sets runtime behavior settings.
    /// </summary>
    public SettingsRuntimeSection? Runtime
    {
        get; init;
    }
}

/// <summary>
/// Contains root path settings.
/// </summary>
public sealed class SettingsPathsSection
{
    public string? ConfigRootPath
    {
        get; init;
    }

    public string? SourcesRootPath
    {
        get; init;
    }

    public string? OverrideRootPath
    {
        get; init;
    }

    public string? MergedRootPath
    {
        get; init;
    }

    public string? StateRootPath
    {
        get; init;
    }

    public string? LogRootPath
    {
        get; init;
    }

    public string? BranchLinksRootPath
    {
        get; init;
    }

    public string? UnraidCachePoolName
    {
        get; init;
    }
}

/// <summary>
/// Contains merge scan timing settings.
/// </summary>
public sealed class SettingsScanSection
{
    public int? MergeIntervalSeconds
    {
        get; init;
    }

    public int? MergeTriggerPollSeconds
    {
        get; init;
    }

    public int? MergeMinSecondsBetweenScans
    {
        get; init;
    }

    public int? MergeLockRetrySeconds
    {
        get; init;
    }
}

/// <summary>
/// Contains chapter rename timing settings.
/// </summary>
public sealed class SettingsRenameSection
{
    public int? RenameDelaySeconds
    {
        get; init;
    }

    public int? RenameQuietSeconds
    {
        get; init;
    }

    public int? RenamePollSeconds
    {
        get; init;
    }

    public int? RenameRescanSeconds
    {
        get; init;
    }
}

/// <summary>
/// Contains diagnostics and profiling settings.
/// </summary>
public sealed class SettingsDiagnosticsSection
{
    public bool? DebugTiming
    {
        get; init;
    }

    public int? DebugTimingTopN
    {
        get; init;
    }

    public int? DebugTimingMinItemMs
    {
        get; init;
    }

    public int? DebugTimingSlowMs
    {
        get; init;
    }

    public bool? DebugTimingLive
    {
        get; init;
    }

    public int? DebugScanProgressEvery
    {
        get; init;
    }

    public int? DebugScanProgressSeconds
    {
        get; init;
    }

    public bool? DebugComicInfo
    {
        get; init;
    }

    public int? TimeoutPollMs
    {
        get; init;
    }

    public int? TimeoutPollMsFast
    {
        get; init;
    }
}

/// <summary>
/// Contains shutdown and cleanup settings.
/// </summary>
public sealed class SettingsShutdownSection
{
    public bool? UnmountOnExit
    {
        get; init;
    }

    public int? StopTimeoutSeconds
    {
        get; init;
    }

    public int? ChildExitGraceSeconds
    {
        get; init;
    }

    public int? UnmountCommandTimeoutSeconds
    {
        get; init;
    }

    public int? UnmountDetachWaitSeconds
    {
        get; init;
    }

    public bool? CleanupHighPriority
    {
        get; init;
    }
}

/// <summary>
/// Contains permission inheritance settings.
/// </summary>
public sealed class SettingsPermissionsSection
{
    public bool? InheritFromParent
    {
        get; init;
    }

    public bool? EnforceExisting
    {
        get; init;
    }

    public string? ReferencePath
    {
        get; init;
    }
}

/// <summary>
/// Contains runtime behavior settings.
/// </summary>
public sealed class SettingsRuntimeSection
{
    public bool? LowPriority
    {
        get; init;
    }

    public bool? StartupCleanup
    {
        get; init;
    }

    public bool? RescanNow
    {
        get; init;
    }

    public bool? EnableMountHealthcheck
    {
        get; init;
    }

    public string? DetailsDescriptionMode
    {
        get; init;
    }

    public string? MergerfsOptionsBase
    {
        get; init;
    }

    public List<string>? ExcludedSources
    {
        get; init;
    }
}
