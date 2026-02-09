namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default settings values for first-run configuration.
/// </summary>
public static class SettingsDocumentDefaults
{
    /// <summary>
    /// Creates the default settings document with container-oriented paths.
    /// </summary>
    /// <returns>A fully populated default settings document.</returns>
    public static SettingsDocument Create()
    {
        return new SettingsDocument
        {
            Paths = new SettingsPathsSection
            {
                ConfigRootPath = "/ssm/config",
                SourcesRootPath = "/ssm/sources",
                OverrideRootPath = "/ssm/override",
                MergedRootPath = "/ssm/merged",
                StateRootPath = "/ssm/state",
                LogRootPath = "/ssm/config",
                BranchLinksRootPath = "/ssm/state/.mergerfs-branches",
                UnraidCachePoolName = string.Empty
            },
            Scan = new SettingsScanSection
            {
                MergeIntervalSeconds = 3600,
                MergeTriggerPollSeconds = 5,
                MergeMinSecondsBetweenScans = 15,
                MergeLockRetrySeconds = 30
            },
            Rename = new SettingsRenameSection
            {
                RenameDelaySeconds = 300,
                RenameQuietSeconds = 120,
                RenamePollSeconds = 20,
                RenameRescanSeconds = 172800
            },
            Diagnostics = new SettingsDiagnosticsSection
            {
                DebugTiming = true,
                DebugTimingTopN = 15,
                DebugTimingMinItemMs = 250,
                DebugTimingSlowMs = 5000,
                DebugTimingLive = true,
                DebugScanProgressEvery = 250,
                DebugScanProgressSeconds = 60,
                DebugComicInfo = false,
                TimeoutPollMs = 100,
                TimeoutPollMsFast = 10
            },
            Shutdown = new SettingsShutdownSection
            {
                UnmountOnExit = true,
                StopTimeoutSeconds = 120,
                ChildExitGraceSeconds = 5,
                UnmountCommandTimeoutSeconds = 8,
                UnmountDetachWaitSeconds = 5,
                CleanupHighPriority = true
            },
            Permissions = new SettingsPermissionsSection
            {
                InheritFromParent = true,
                EnforceExisting = false,
                ReferencePath = "/ssm/sources"
            },
            Runtime = new SettingsRuntimeSection
            {
                LowPriority = true,
                StartupCleanup = true,
                RescanNow = true,
                EnableMountHealthcheck = false,
                DetailsDescriptionMode = "text",
                MergerfsOptionsBase = "allow_other,default_permissions,use_ino,category.create=ff,cache.entry=0,cache.attr=0,cache.negative_entry=0",
                ExcludedSources = ["Local source"]
            }
        };
    }
}
