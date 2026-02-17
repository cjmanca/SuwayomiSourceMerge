namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Verifies generated configuration defaults for non-settings documents.
/// </summary>
public sealed class ConfigurationDocumentDefaultsTests
{
    [Fact]
    public void MangaEquivalentsDefaults_Expected_ShouldIncludeStarterExampleGroup()
    {
        MangaEquivalentsDocument document = MangaEquivalentsDocumentDefaults.Create();

        MangaEquivalentGroup group = Assert.Single(document.Groups!);
        Assert.Equal("Example Manga Title", group.Canonical);
        Assert.Equal(["Example Manga Alt Title"], group.Aliases);
    }

    [Fact]
    public void SourcePriorityDefaults_Expected_ShouldIncludeStarterExampleSource()
    {
        SourcePriorityDocument document = SourcePriorityDocumentDefaults.Create();

        Assert.Equal(["Example Source Name"], document.Sources);
    }

    [Fact]
    public void MangaEquivalentsDefaults_Edge_ShouldReturnIndependentDocumentsAcrossCalls()
    {
        MangaEquivalentsDocument first = MangaEquivalentsDocumentDefaults.Create();
        MangaEquivalentsDocument second = MangaEquivalentsDocumentDefaults.Create();

        first.Groups!.Add(
            new MangaEquivalentGroup
            {
                Canonical = "Injected Canonical",
                Aliases = []
            });

        List<MangaEquivalentGroup> secondGroups = Assert.IsType<List<MangaEquivalentGroup>>(second.Groups);
        Assert.Single(secondGroups);
        Assert.Equal("Example Manga Title", secondGroups[0].Canonical);
    }

    [Fact]
    public void SourcePriorityDefaults_Edge_ShouldReturnIndependentListsAcrossCalls()
    {
        SourcePriorityDocument first = SourcePriorityDocumentDefaults.Create();
        SourcePriorityDocument second = SourcePriorityDocumentDefaults.Create();

        first.Sources!.Add("Injected Source");

        List<string> secondSources = Assert.IsType<List<string>>(second.Sources);
        Assert.Equal(["Example Source Name"], secondSources);
    }
}
