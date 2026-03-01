namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FilesystemEventTriggerOptions"/>.
/// </summary>
public sealed class FilesystemEventTriggerOptionsTests
{
	/// <summary>
	/// Verifies options can be created from default settings.
	/// </summary>
	[Fact]
	public void FromSettings_Expected_ShouldMapConfiguredValues()
	{
		SettingsDocument settings = SettingsDocumentDefaults.Create();

		FilesystemEventTriggerOptions options = FilesystemEventTriggerOptions.FromSettings(settings);

		Assert.Equal(Path.GetFullPath("/ssm/sources"), options.SourcesRootPath);
		Assert.Equal(Path.GetFullPath("/ssm/override"), options.OverrideRootPath);
		Assert.Equal(5, options.InotifyPollSeconds);
		Assert.Equal(3600, options.MergeIntervalSeconds);
		Assert.Equal(15, options.MergeMinSecondsBetweenScans);
		Assert.Equal(30, options.MergeLockRetrySeconds);
		Assert.Equal(300, options.InotifyRequestTimeoutBufferSeconds);
		Assert.True(options.StartupRenameRescanEnabled);
		Assert.Equal(InotifyWatchStartupMode.Progressive, options.WatchStartupMode);
	}

	/// <summary>
	/// Verifies constructor normalizes paths and accepts zero min-between-scans.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldAllowZeroMinSecondsBetweenScans()
	{
		ChapterRenameOptions renameOptions = new(
			"/ssm/sources",
			renameDelaySeconds: 1,
			renameQuietSeconds: 0,
			renamePollSeconds: 1,
			renameRescanSeconds: 1,
			[]);
		FilesystemEventTriggerOptions options = new(
			renameOptions,
			"/ssm/override",
			inotifyPollSeconds: 1,
			mergeIntervalSeconds: 1,
			mergeMinSecondsBetweenScans: 0,
			mergeLockRetrySeconds: 1,
			startupRenameRescanEnabled: false);

		Assert.Equal(0, options.MergeMinSecondsBetweenScans);
		Assert.Equal(300, options.InotifyRequestTimeoutBufferSeconds);
		Assert.False(options.StartupRenameRescanEnabled);
		Assert.Equal(InotifyWatchStartupMode.Progressive, options.WatchStartupMode);
	}

	/// <summary>
	/// Verifies watch startup mode tokens map to typed options.
	/// </summary>
	[Fact]
	public void FromSettings_Expected_ShouldMapWatchStartupMode()
	{
		SettingsDocument defaults = SettingsDocumentDefaults.Create();
		SettingsDocument settings = new()
		{
			Paths = defaults.Paths,
			Scan = new SettingsScanSection
			{
				MergeIntervalSeconds = defaults.Scan!.MergeIntervalSeconds,
				MergeTriggerPollSeconds = defaults.Scan.MergeTriggerPollSeconds,
				MergeMinSecondsBetweenScans = defaults.Scan.MergeMinSecondsBetweenScans,
				MergeLockRetrySeconds = defaults.Scan.MergeLockRetrySeconds,
				MergeTriggerRequestTimeoutBufferSeconds = defaults.Scan.MergeTriggerRequestTimeoutBufferSeconds,
				WatchStartupMode = "full"
			},
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = defaults.Runtime,
			Logging = defaults.Logging
		};

		FilesystemEventTriggerOptions options = FilesystemEventTriggerOptions.FromSettings(settings);

		Assert.Equal(InotifyWatchStartupMode.Full, options.WatchStartupMode);
	}

	/// <summary>
	/// Verifies guard clauses reject invalid constructor and settings inputs.
	/// </summary>
	[Fact]
	public void ConstructorAndFromSettings_Failure_ShouldThrow_WhenInputsInvalid()
	{
		ChapterRenameOptions renameOptions = new(
			"/ssm/sources",
			renameDelaySeconds: 1,
			renameQuietSeconds: 0,
			renamePollSeconds: 1,
			renameRescanSeconds: 1,
			[]);

		ArgumentNullException nullRenameOptionsException = Assert.Throws<ArgumentNullException>(() => new FilesystemEventTriggerOptions(null!, "/ssm/override", 1, 1, 0, 1, true));
		ArgumentException overrideRootPathException = Assert.ThrowsAny<ArgumentException>(() => new FilesystemEventTriggerOptions(renameOptions, "", 1, 1, 0, 1, true));
		ArgumentOutOfRangeException inotifyPollSecondsException = Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 0, 1, 0, 1, true));
		ArgumentOutOfRangeException mergeIntervalSecondsException = Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 1, 0, 0, 1, true));
		ArgumentOutOfRangeException mergeMinSecondsBetweenScansException = Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 1, 1, -1, 1, true));
		ArgumentOutOfRangeException mergeLockRetrySecondsException = Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 1, 1, 0, 0, true));
		ArgumentOutOfRangeException inotifyRequestTimeoutBufferSecondsException = Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 1, 1, 0, 1, true, 0));

		Assert.Equal("renameOptions", nullRenameOptionsException.ParamName);
		Assert.Equal("overrideRootPath", overrideRootPathException.ParamName);
		Assert.Equal("inotifyPollSeconds", inotifyPollSecondsException.ParamName);
		Assert.Equal("mergeIntervalSeconds", mergeIntervalSecondsException.ParamName);
		Assert.Equal("mergeMinSecondsBetweenScans", mergeMinSecondsBetweenScansException.ParamName);
		Assert.Equal("mergeLockRetrySeconds", mergeLockRetrySecondsException.ParamName);
		Assert.Equal("inotifyRequestTimeoutBufferSeconds", inotifyRequestTimeoutBufferSecondsException.ParamName);

		SettingsDocument invalidSettings = new()
		{
			Paths = new SettingsPathsSection
			{
				SourcesRootPath = "/ssm/sources"
			}
		};

		SettingsDocument defaults = SettingsDocumentDefaults.Create();
		SettingsDocument settingsMissingTimeoutBuffer = new()
		{
			Paths = defaults.Paths,
			Scan = new SettingsScanSection
			{
				MergeIntervalSeconds = 3600,
				MergeTriggerPollSeconds = 5,
				MergeMinSecondsBetweenScans = 15,
				MergeLockRetrySeconds = 30,
				MergeTriggerRequestTimeoutBufferSeconds = null
			},
			Rename = defaults.Rename,
			Diagnostics = defaults.Diagnostics,
			Shutdown = defaults.Shutdown,
			Permissions = defaults.Permissions,
			Runtime = defaults.Runtime,
			Logging = defaults.Logging
		};

		ArgumentException invalidSettingsException = Assert.Throws<ArgumentException>(() => FilesystemEventTriggerOptions.FromSettings(invalidSettings));
		FilesystemEventTriggerOptions options = FilesystemEventTriggerOptions.FromSettings(settingsMissingTimeoutBuffer);
		Assert.Equal(300, options.InotifyRequestTimeoutBufferSeconds);
		SettingsDocument validDefaults = SettingsDocumentDefaults.Create();
		SettingsDocument invalidSettingsWatchMode = new()
		{
			Paths = validDefaults.Paths,
			Scan = new SettingsScanSection
			{
				MergeIntervalSeconds = 3600,
				MergeTriggerPollSeconds = 5,
				MergeMinSecondsBetweenScans = 15,
				MergeLockRetrySeconds = 30,
				MergeTriggerRequestTimeoutBufferSeconds = 300,
				WatchStartupMode = "unsupported"
			},
			Rename = validDefaults.Rename,
			Diagnostics = validDefaults.Diagnostics,
			Shutdown = validDefaults.Shutdown,
			Permissions = validDefaults.Permissions,
			Runtime = validDefaults.Runtime,
			Logging = validDefaults.Logging
		};
		ArgumentException invalidSettingsWatchModeException = Assert.Throws<ArgumentException>(() => FilesystemEventTriggerOptions.FromSettings(invalidSettingsWatchMode));
		ArgumentNullException nullSettingsException = Assert.Throws<ArgumentNullException>(() => FilesystemEventTriggerOptions.FromSettings(null!));

		Assert.Equal("settings", invalidSettingsException.ParamName);
		Assert.Equal("watchStartupMode", invalidSettingsWatchModeException.ParamName);
		Assert.Equal("settings", nullSettingsException.ParamName);
	}
}
