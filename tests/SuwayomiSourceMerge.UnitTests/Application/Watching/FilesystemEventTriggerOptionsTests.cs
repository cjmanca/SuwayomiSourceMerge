namespace SuwayomiSourceMerge.UnitTests.Application.Watching;

using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Infrastructure.Rename;

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
		Assert.True(options.StartupRenameRescanEnabled);
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
		Assert.False(options.StartupRenameRescanEnabled);
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

		Assert.Throws<ArgumentNullException>(() => new FilesystemEventTriggerOptions(null!, "/ssm/override", 1, 1, 0, 1, true));
		Assert.ThrowsAny<ArgumentException>(() => new FilesystemEventTriggerOptions(renameOptions, "", 1, 1, 0, 1, true));
		Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 0, 1, 0, 1, true));
		Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 1, 0, 0, 1, true));
		Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 1, 1, -1, 1, true));
		Assert.Throws<ArgumentOutOfRangeException>(() => new FilesystemEventTriggerOptions(renameOptions, "/ssm/override", 1, 1, 0, 0, true));

		SettingsDocument invalidSettings = new()
		{
			Paths = new SettingsPathsSection
			{
				SourcesRootPath = "/ssm/sources"
			}
		};

		Assert.Throws<ArgumentException>(() => FilesystemEventTriggerOptions.FromSettings(invalidSettings));
		Assert.Throws<ArgumentNullException>(() => FilesystemEventTriggerOptions.FromSettings(null!));
	}
}
