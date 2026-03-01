namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Rename;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Verifies chapter rename option construction and normalization behavior.
/// </summary>
public sealed class ChapterRenameOptionsTests
{
	/// <summary>
	/// Verifies options can be built from validated settings defaults.
	/// </summary>
	[Fact]
	public void FromSettings_Expected_ShouldMapRenameAndSourceSettings()
	{
		SettingsDocument settings = SettingsDocumentDefaults.Create();

		ChapterRenameOptions options = ChapterRenameOptions.FromSettings(settings);

		Assert.Equal(Path.GetFullPath("/ssm/sources"), options.SourcesRootPath);
		Assert.Equal(300, options.RenameDelaySeconds);
		Assert.Equal(120, options.RenameQuietSeconds);
		Assert.Equal(20, options.RenamePollSeconds);
		Assert.Equal(172800, options.RenameRescanSeconds);
		Assert.Single(options.ExcludedSources);
		Assert.True(options.IsExcludedSource("local source"));
	}

	/// <summary>
	/// Verifies excluded source matching is trim and case-insensitive.
	/// </summary>
	[Fact]
	public void IsExcludedSource_Edge_ShouldNormalizeWhitespaceAndCase()
	{
		ChapterRenameOptions options = new(
			"/ssm/sources",
			1,
			0,
			1,
			1,
			["  Local Source ", "Other"]);

		Assert.True(options.IsExcludedSource("local source"));
		Assert.True(options.IsExcludedSource(" OTHER "));
		Assert.False(options.IsExcludedSource("unknown"));
	}

	/// <summary>
	/// Verifies constructor guard clauses reject invalid values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenArgumentsAreInvalid()
	{
		ArgumentException sourcesRootPathException = Assert.ThrowsAny<ArgumentException>(
			() => new ChapterRenameOptions("", 1, 1, 1, 1, []));
		ArgumentNullException excludedSourcesException = Assert.Throws<ArgumentNullException>(
			() => new ChapterRenameOptions("/ssm/sources", 1, 1, 1, 1, null!));
		ArgumentOutOfRangeException renameDelaySecondsException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", -1, 1, 1, 1, []));
		ArgumentOutOfRangeException renameQuietSecondsException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", 0, -1, 1, 1, []));
		ArgumentOutOfRangeException renamePollSecondsException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", 0, 0, 0, 1, []));
		ArgumentOutOfRangeException renameRescanSecondsException = Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", 0, 0, 1, 0, []));

		Assert.Equal("sourcesRootPath", sourcesRootPathException.ParamName);
		Assert.Equal("excludedSources", excludedSourcesException.ParamName);
		Assert.Equal("renameDelaySeconds", renameDelaySecondsException.ParamName);
		Assert.Equal("renameQuietSeconds", renameQuietSecondsException.ParamName);
		Assert.Equal("renamePollSeconds", renamePollSecondsException.ParamName);
		Assert.Equal("renameRescanSeconds", renameRescanSecondsException.ParamName);
	}

	/// <summary>
	/// Verifies missing settings sections/values fail fast.
	/// </summary>
	[Fact]
	public void FromSettings_Failure_ShouldThrow_WhenSettingsAreIncomplete()
	{
		SettingsDocument settingsWithoutRename = new()
		{
			Paths = new SettingsPathsSection
			{
				SourcesRootPath = "/ssm/sources"
			}
		};

		SettingsDocument settingsWithoutSourcesPath = new()
		{
			Paths = new SettingsPathsSection(),
			Rename = new SettingsRenameSection
			{
				RenameDelaySeconds = 1,
				RenameQuietSeconds = 0,
				RenamePollSeconds = 1,
				RenameRescanSeconds = 1
			}
		};

		ArgumentException settingsWithoutRenameException = Assert.Throws<ArgumentException>(() => ChapterRenameOptions.FromSettings(settingsWithoutRename));
		ArgumentException settingsWithoutSourcesPathException = Assert.Throws<ArgumentException>(() => ChapterRenameOptions.FromSettings(settingsWithoutSourcesPath));
		ArgumentNullException nullSettingsException = Assert.Throws<ArgumentNullException>(() => ChapterRenameOptions.FromSettings(null!));

		Assert.Equal("settings", settingsWithoutRenameException.ParamName);
		Assert.Equal("settings", settingsWithoutSourcesPathException.ParamName);
		Assert.Equal("settings", nullSettingsException.ParamName);
	}
}

