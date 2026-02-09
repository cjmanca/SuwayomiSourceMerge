namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class LegacySourcePriorityMigratorTests
{
    [Fact]
    public void Migrate_ShouldParseSourceOrder_WhenLegacyContentIsValid()
    {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "source_priority.txt");
        File.WriteAllText(filePath, "Source A\nSource B\n");

        LegacySourcePriorityMigrator migrator = new();

        LegacyMigrationResult<SuwayomiSourceMerge.Configuration.Documents.SourcePriorityDocument> result = migrator.Migrate(filePath);

        Assert.Empty(result.Warnings);
        Assert.Equal(["Source A", "Source B"], result.Document.Sources);
    }

    [Fact]
    public void Migrate_ShouldSkipBlankAndCommentLines()
    {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "source_priority.txt");
        File.WriteAllText(
            filePath,
            """
            # comment

            Source A
              Source B
            """);

        LegacySourcePriorityMigrator migrator = new();

        LegacyMigrationResult<SuwayomiSourceMerge.Configuration.Documents.SourcePriorityDocument> result = migrator.Migrate(filePath);

        Assert.Equal(["Source A", "Source B"], result.Document.Sources);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Migrate_ShouldThrow_WhenPathIsNullOrWhitespace(string? filePath)
    {
        LegacySourcePriorityMigrator migrator = new();

        Assert.ThrowsAny<ArgumentException>(() => migrator.Migrate(filePath!));
    }
}
