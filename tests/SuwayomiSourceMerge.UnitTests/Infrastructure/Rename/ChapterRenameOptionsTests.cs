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
		Assert.ThrowsAny<ArgumentException>(
			() => new ChapterRenameOptions("", 1, 1, 1, 1, []));
		Assert.Throws<ArgumentNullException>(
			() => new ChapterRenameOptions("/ssm/sources", 1, 1, 1, 1, null!));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", -1, 1, 1, 1, []));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", 0, -1, 1, 1, []));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", 0, 0, 0, 1, []));
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new ChapterRenameOptions("/ssm/sources", 0, 0, 1, 0, []));
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

		Assert.Throws<ArgumentException>(() => ChapterRenameOptions.FromSettings(settingsWithoutRename));
		Assert.Throws<ArgumentException>(() => ChapterRenameOptions.FromSettings(settingsWithoutSourcesPath));
		Assert.Throws<ArgumentNullException>(() => ChapterRenameOptions.FromSettings(null!));
	}
}

