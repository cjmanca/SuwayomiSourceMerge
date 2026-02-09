namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class YamlDocumentWriterTests
{
    [Fact]
    public void Write_ShouldPersistYamlDocument_WithDeterministicNewline()
    {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "scene_tags.yml");
        YamlDocumentWriter writer = new();

        writer.Write(
            filePath,
            new SceneTagsDocument
            {
                Tags = ["official"]
            });

        string yaml = File.ReadAllText(filePath);
        Assert.Equal("tags:\n- official\n", yaml);
    }

    [Fact]
    public void Write_ShouldCreateParentDirectory_WhenDirectoryMissing()
    {
        using TemporaryDirectory directory = new();
        string nestedPath = Path.Combine(directory.Path, "config", "generated", "source_priority.yml");
        YamlDocumentWriter writer = new();

        writer.Write(
            nestedPath,
            new SourcePriorityDocument
            {
                Sources = ["Source A"]
            });

        Assert.True(File.Exists(nestedPath));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Write_ShouldThrow_WhenOutputPathIsNullOrWhitespace(string? outputPath)
    {
        YamlDocumentWriter writer = new();

        Assert.ThrowsAny<ArgumentException>(
            () => writer.Write(
                outputPath!,
                new SceneTagsDocument
                {
                    Tags = ["official"]
                }));
    }

    [Fact]
    public void Write_ShouldThrow_WhenDocumentIsNull()
    {
        using TemporaryDirectory directory = new();
        string outputPath = Path.Combine(directory.Path, "scene_tags.yml");
        YamlDocumentWriter writer = new();

        Assert.Throws<ArgumentNullException>(() => writer.Write<SceneTagsDocument>(outputPath, null!));
    }
}
