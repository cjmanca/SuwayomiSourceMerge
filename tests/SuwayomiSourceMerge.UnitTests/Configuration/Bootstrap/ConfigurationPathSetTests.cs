namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;

public sealed class ConfigurationPathSetTests
{
    [Fact]
    public void FromRoot_ShouldCreateExpectedFilePaths_WhenRootIsValid()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "ssm-config-root");

        ConfigurationPathSet paths = ConfigurationPathSet.FromRoot(rootPath);

        string expectedRoot = Path.GetFullPath(rootPath);
        Assert.Equal(expectedRoot, paths.ConfigRootPath);
        Assert.Equal(Path.Combine(expectedRoot, "settings.yml"), paths.SettingsYamlPath);
        Assert.Equal(Path.Combine(expectedRoot, "manga_equivalents.yml"), paths.MangaEquivalentsYamlPath);
        Assert.Equal(Path.Combine(expectedRoot, "scene_tags.yml"), paths.SceneTagsYamlPath);
        Assert.Equal(Path.Combine(expectedRoot, "source_priority.yml"), paths.SourcePriorityYamlPath);
        Assert.Equal(Path.Combine(expectedRoot, "manga_equivalents.txt"), paths.MangaEquivalentsLegacyPath);
        Assert.Equal(Path.Combine(expectedRoot, "source_priority.txt"), paths.SourcePriorityLegacyPath);
    }

    [Fact]
    public void FromRoot_ShouldNormalizeRelativeSegments_WhenDotSegmentsProvided()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), "ssm-config-root", "..", "ssm-config-final");

        ConfigurationPathSet paths = ConfigurationPathSet.FromRoot(rootPath);

        Assert.Equal(Path.GetFullPath(rootPath), paths.ConfigRootPath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromRoot_ShouldThrow_WhenRootIsNullOrWhitespace(string? rootPath)
    {
        Assert.ThrowsAny<ArgumentException>(() => ConfigurationPathSet.FromRoot(rootPath!));
    }
}
