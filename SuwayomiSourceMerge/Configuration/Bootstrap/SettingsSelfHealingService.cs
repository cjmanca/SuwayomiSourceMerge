using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Repairs partially populated <c>settings.yml</c> documents by filling missing values from defaults.
/// </summary>
/// <remarks>
/// Existing values are preserved verbatim. Missing values are populated from
/// <see cref="SettingsDocumentDefaults"/> and the caller can decide whether to persist the result.
/// </remarks>
internal sealed class SettingsSelfHealingService
{
	/// <summary>
	/// YAML parser used to load the current settings file into a typed document.
	/// </summary>
	private readonly YamlDocumentParser _parser;

	/// <summary>
	/// Creates a self-healing service.
	/// </summary>
	/// <param name="parser">Typed YAML parser used to read the existing settings file.</param>
	public SettingsSelfHealingService(YamlDocumentParser parser)
	{
		_parser = parser ?? throw new ArgumentNullException(nameof(parser));
	}

	/// <summary>
	/// Loads, validates, and self-heals a settings YAML file.
	/// </summary>
	/// <param name="settingsYamlPath">Absolute path to <c>settings.yml</c>.</param>
	/// <returns>A result containing the healed document and whether healing occurred.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="settingsYamlPath"/> is empty or whitespace.</exception>
	/// <exception cref="FileNotFoundException">Thrown when the target file does not exist.</exception>
	/// <exception cref="IOException">Thrown when the target file cannot be read.</exception>
	/// <exception cref="ConfigurationBootstrapException">
	/// Thrown when YAML parsing/validation fails and healing cannot proceed.
	/// </exception>
	public SettingsSelfHealingResult SelfHeal(string settingsYamlPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(settingsYamlPath);

		string fileName = Path.GetFileName(settingsYamlPath);
		string yamlContent = File.ReadAllText(settingsYamlPath);

		ParsedDocument<SettingsDocument> parsed = _parser.Parse<SettingsDocument>(fileName, yamlContent);
		if (!parsed.Validation.IsValid || parsed.Document is null)
		{
			throw new ConfigurationBootstrapException(parsed.Validation.Errors);
		}

		bool wasHealed = false;
		SettingsDocument defaults = SettingsDocumentDefaults.Create();
		SettingsDocument merged = Merge(parsed.Document, defaults, ref wasHealed);

		return new SettingsSelfHealingResult(merged, wasHealed);
	}

	/// <summary>
	/// Merges an existing settings document with defaults, filling only missing values.
	/// </summary>
	/// <param name="existing">Existing parsed settings document.</param>
	/// <param name="defaults">Default settings document used as fallback source.</param>
	/// <param name="wasHealed">Set to <see langword="true"/> when any fallback value is applied.</param>
	/// <returns>
	/// A merged settings document with all required fields populated while preserving existing values.
	/// </returns>
	private static SettingsDocument Merge(SettingsDocument existing, SettingsDocument defaults, ref bool wasHealed)
	{
		SettingsPathsSection? existingPaths = existing.Paths;
		SettingsScanSection? existingScan = existing.Scan;
		SettingsRenameSection? existingRename = existing.Rename;
		SettingsDiagnosticsSection? existingDiagnostics = existing.Diagnostics;
		SettingsShutdownSection? existingShutdown = existing.Shutdown;
		SettingsPermissionsSection? existingPermissions = existing.Permissions;
		SettingsRuntimeSection? existingRuntime = existing.Runtime;
		SettingsLoggingSection? existingLogging = existing.Logging;

		SettingsPathsSection defaultPaths = defaults.Paths!;
		SettingsScanSection defaultScan = defaults.Scan!;
		SettingsRenameSection defaultRename = defaults.Rename!;
		SettingsDiagnosticsSection defaultDiagnostics = defaults.Diagnostics!;
		SettingsShutdownSection defaultShutdown = defaults.Shutdown!;
		SettingsPermissionsSection defaultPermissions = defaults.Permissions!;
		SettingsRuntimeSection defaultRuntime = defaults.Runtime!;
		SettingsLoggingSection defaultLogging = defaults.Logging!;

		return new SettingsDocument
		{
			Paths = new SettingsPathsSection
			{
				ConfigRootPath = existingPaths?.ConfigRootPath ?? UseDefault(defaultPaths.ConfigRootPath, ref wasHealed),
				SourcesRootPath = existingPaths?.SourcesRootPath ?? UseDefault(defaultPaths.SourcesRootPath, ref wasHealed),
				OverrideRootPath = existingPaths?.OverrideRootPath ?? UseDefault(defaultPaths.OverrideRootPath, ref wasHealed),
				MergedRootPath = existingPaths?.MergedRootPath ?? UseDefault(defaultPaths.MergedRootPath, ref wasHealed),
				StateRootPath = existingPaths?.StateRootPath ?? UseDefault(defaultPaths.StateRootPath, ref wasHealed),
				LogRootPath = existingPaths?.LogRootPath ?? UseDefault(defaultPaths.LogRootPath, ref wasHealed),
				BranchLinksRootPath = existingPaths?.BranchLinksRootPath ?? UseDefault(defaultPaths.BranchLinksRootPath, ref wasHealed),
				UnraidCachePoolName = existingPaths?.UnraidCachePoolName ?? UseDefault(defaultPaths.UnraidCachePoolName, ref wasHealed)
			},
			Scan = new SettingsScanSection
			{
				MergeIntervalSeconds = existingScan?.MergeIntervalSeconds ?? UseDefault(defaultScan.MergeIntervalSeconds, ref wasHealed),
				MergeTriggerPollSeconds = existingScan?.MergeTriggerPollSeconds ?? UseDefault(defaultScan.MergeTriggerPollSeconds, ref wasHealed),
				MergeMinSecondsBetweenScans = existingScan?.MergeMinSecondsBetweenScans ?? UseDefault(defaultScan.MergeMinSecondsBetweenScans, ref wasHealed),
				MergeLockRetrySeconds = existingScan?.MergeLockRetrySeconds ?? UseDefault(defaultScan.MergeLockRetrySeconds, ref wasHealed),
				MergeTriggerRequestTimeoutBufferSeconds = existingScan?.MergeTriggerRequestTimeoutBufferSeconds ?? UseDefault(defaultScan.MergeTriggerRequestTimeoutBufferSeconds, ref wasHealed)
			},
			Rename = new SettingsRenameSection
			{
				RenameDelaySeconds = existingRename?.RenameDelaySeconds ?? UseDefault(defaultRename.RenameDelaySeconds, ref wasHealed),
				RenameQuietSeconds = existingRename?.RenameQuietSeconds ?? UseDefault(defaultRename.RenameQuietSeconds, ref wasHealed),
				RenamePollSeconds = existingRename?.RenamePollSeconds ?? UseDefault(defaultRename.RenamePollSeconds, ref wasHealed),
				RenameRescanSeconds = existingRename?.RenameRescanSeconds ?? UseDefault(defaultRename.RenameRescanSeconds, ref wasHealed)
			},
			Diagnostics = new SettingsDiagnosticsSection
			{
				DebugTiming = existingDiagnostics?.DebugTiming ?? UseDefault(defaultDiagnostics.DebugTiming, ref wasHealed),
				DebugTimingTopN = existingDiagnostics?.DebugTimingTopN ?? UseDefault(defaultDiagnostics.DebugTimingTopN, ref wasHealed),
				DebugTimingMinItemMs = existingDiagnostics?.DebugTimingMinItemMs ?? UseDefault(defaultDiagnostics.DebugTimingMinItemMs, ref wasHealed),
				DebugTimingSlowMs = existingDiagnostics?.DebugTimingSlowMs ?? UseDefault(defaultDiagnostics.DebugTimingSlowMs, ref wasHealed),
				DebugTimingLive = existingDiagnostics?.DebugTimingLive ?? UseDefault(defaultDiagnostics.DebugTimingLive, ref wasHealed),
				DebugScanProgressEvery = existingDiagnostics?.DebugScanProgressEvery ?? UseDefault(defaultDiagnostics.DebugScanProgressEvery, ref wasHealed),
				DebugScanProgressSeconds = existingDiagnostics?.DebugScanProgressSeconds ?? UseDefault(defaultDiagnostics.DebugScanProgressSeconds, ref wasHealed),
				DebugComicInfo = existingDiagnostics?.DebugComicInfo ?? UseDefault(defaultDiagnostics.DebugComicInfo, ref wasHealed),
				TimeoutPollMs = existingDiagnostics?.TimeoutPollMs ?? UseDefault(defaultDiagnostics.TimeoutPollMs, ref wasHealed),
				TimeoutPollMsFast = existingDiagnostics?.TimeoutPollMsFast ?? UseDefault(defaultDiagnostics.TimeoutPollMsFast, ref wasHealed)
			},
			Shutdown = new SettingsShutdownSection
			{
				UnmountOnExit = existingShutdown?.UnmountOnExit ?? UseDefault(defaultShutdown.UnmountOnExit, ref wasHealed),
				StopTimeoutSeconds = existingShutdown?.StopTimeoutSeconds ?? UseDefault(defaultShutdown.StopTimeoutSeconds, ref wasHealed),
				ChildExitGraceSeconds = existingShutdown?.ChildExitGraceSeconds ?? UseDefault(defaultShutdown.ChildExitGraceSeconds, ref wasHealed),
				UnmountCommandTimeoutSeconds = existingShutdown?.UnmountCommandTimeoutSeconds ?? UseDefault(defaultShutdown.UnmountCommandTimeoutSeconds, ref wasHealed),
				UnmountDetachWaitSeconds = existingShutdown?.UnmountDetachWaitSeconds ?? UseDefault(defaultShutdown.UnmountDetachWaitSeconds, ref wasHealed),
				CleanupHighPriority = existingShutdown?.CleanupHighPriority ?? UseDefault(defaultShutdown.CleanupHighPriority, ref wasHealed),
				CleanupApplyHighPriority = existingShutdown?.CleanupApplyHighPriority ?? UseDefault(defaultShutdown.CleanupApplyHighPriority, ref wasHealed),
				CleanupPriorityIoniceClass = existingShutdown?.CleanupPriorityIoniceClass ?? UseDefault(defaultShutdown.CleanupPriorityIoniceClass, ref wasHealed),
				CleanupPriorityNiceValue = existingShutdown?.CleanupPriorityNiceValue ?? UseDefault(defaultShutdown.CleanupPriorityNiceValue, ref wasHealed)
			},
			Permissions = new SettingsPermissionsSection
			{
				InheritFromParent = existingPermissions?.InheritFromParent ?? UseDefault(defaultPermissions.InheritFromParent, ref wasHealed),
				EnforceExisting = existingPermissions?.EnforceExisting ?? UseDefault(defaultPermissions.EnforceExisting, ref wasHealed),
				ReferencePath = existingPermissions?.ReferencePath ?? UseDefault(defaultPermissions.ReferencePath, ref wasHealed)
			},
			Runtime = new SettingsRuntimeSection
			{
				LowPriority = existingRuntime?.LowPriority ?? UseDefault(defaultRuntime.LowPriority, ref wasHealed),
				StartupCleanup = existingRuntime?.StartupCleanup ?? UseDefault(defaultRuntime.StartupCleanup, ref wasHealed),
				RescanNow = existingRuntime?.RescanNow ?? UseDefault(defaultRuntime.RescanNow, ref wasHealed),
				EnableMountHealthcheck = existingRuntime?.EnableMountHealthcheck ?? UseDefault(defaultRuntime.EnableMountHealthcheck, ref wasHealed),
				DetailsDescriptionMode = existingRuntime?.DetailsDescriptionMode ?? UseDefault(defaultRuntime.DetailsDescriptionMode, ref wasHealed),
				MergerfsOptionsBase = existingRuntime?.MergerfsOptionsBase ?? UseDefault(defaultRuntime.MergerfsOptionsBase, ref wasHealed),
				ExcludedSources = existingRuntime?.ExcludedSources ?? UseDefault(defaultRuntime.ExcludedSources, ref wasHealed)
			},
			Logging = new SettingsLoggingSection
			{
				FileName = existingLogging?.FileName ?? UseDefault(defaultLogging.FileName, ref wasHealed),
				MaxFileSizeMb = existingLogging?.MaxFileSizeMb ?? UseDefault(defaultLogging.MaxFileSizeMb, ref wasHealed),
				RetainedFileCount = existingLogging?.RetainedFileCount ?? UseDefault(defaultLogging.RetainedFileCount, ref wasHealed),
				Level = existingLogging?.Level ?? UseDefault(defaultLogging.Level, ref wasHealed)
			}
		};
	}

	/// <summary>
	/// Marks healing as applied and returns the supplied default value.
	/// </summary>
	/// <typeparam name="T">Type of value being returned.</typeparam>
	/// <param name="value">Default value to use.</param>
	/// <param name="wasHealed">Healing flag that will be set to <see langword="true"/>.</param>
	/// <returns>The provided <paramref name="value"/>.</returns>
	private static T UseDefault<T>(T value, ref bool wasHealed)
	{
		wasHealed = true;
		return value;
	}
}
