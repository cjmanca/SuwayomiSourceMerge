namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces canonical default settings values for first-run configuration and self-healing.
/// </summary>
/// <remarks>
/// Values returned here must stay aligned with documented schema defaults and are used by bootstrap
/// code paths whenever a settings file is missing fields.
/// Update this factory in lockstep with <c>settings.yml</c> schema additions to keep self-healing
/// deterministic.
/// </remarks>
public static class SettingsDocumentDefaults
{
	/// <summary>
	/// Creates a fully populated default settings document with container-oriented runtime values.
	/// </summary>
	/// <returns>
	/// A complete <see cref="SettingsDocument"/> instance containing defaults for every section and field.
	/// </returns>
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
				MergeLockRetrySeconds = 30,
				MergeTriggerRequestTimeoutBufferSeconds = 300,
				WatchStartupMode = "progressive"
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
				CleanupHighPriority = true,
				CleanupApplyHighPriority = false,
				CleanupPriorityIoniceClass = 3,
				CleanupPriorityNiceValue = -20
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
			},
			Logging = new SettingsLoggingSection
			{
				FileName = "daemon.log",
				MaxFileSizeMb = 10,
				RetainedFileCount = 10,
				Level = "warning"
			}
		};
	}
}
