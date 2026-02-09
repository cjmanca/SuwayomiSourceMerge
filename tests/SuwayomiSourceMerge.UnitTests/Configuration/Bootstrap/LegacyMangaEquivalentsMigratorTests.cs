namespace SuwayomiSourceMerge.UnitTests.Configuration.Bootstrap;

using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class LegacyMangaEquivalentsMigratorTests
{
    [Fact]
    public void Migrate_ShouldParseCanonicalAndAliases_WhenLegacyContentIsValid()
    {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "manga_equivalents.txt");
        File.WriteAllText(filePath, "Manga One|Alias One|Alias Two\n");

        LegacyMangaEquivalentsMigrator migrator = new();

        LegacyMigrationResult<SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentsDocument> result = migrator.Migrate(filePath);

        Assert.Empty(result.Warnings);
        SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentGroup group = Assert.Single(result.Document.Groups!);
        Assert.Equal("Manga One", group.Canonical);
        Assert.Equal(["Alias One", "Alias Two"], group.Aliases);
    }

    [Fact]
    public void Migrate_ShouldSkipBlankCommentsAndInvalidCanonicalLines()
    {
        using TemporaryDirectory directory = new();
        string filePath = Path.Combine(directory.Path, "manga_equivalents.txt");
        File.WriteAllText(
            filePath,
            """
            # comment

            |alias only
            Canonical A|Alias A|#ignore
            Canonical B|Alias B
            """);

        LegacyMangaEquivalentsMigrator migrator = new();

        LegacyMigrationResult<SuwayomiSourceMerge.Configuration.Documents.MangaEquivalentsDocument> result = migrator.Migrate(filePath);

        ConfigurationBootstrapWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("CFG-MIG-001", warning.Code);
        Assert.Equal(3, warning.Line);

        Assert.Equal(2, result.Document.Groups!.Count);
        Assert.Equal("Canonical A", result.Document.Groups[0].Canonical);
        Assert.Equal(["Alias A"], result.Document.Groups[0].Aliases);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Migrate_ShouldThrow_WhenPathIsNullOrWhitespace(string? filePath)
    {
        LegacyMangaEquivalentsMigrator migrator = new();

        Assert.ThrowsAny<ArgumentException>(() => migrator.Migrate(filePath!));
    }
}
