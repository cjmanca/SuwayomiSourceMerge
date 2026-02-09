namespace SuwayomiSourceMerge.UnitTests.Configuration;

using SuwayomiSourceMerge.Configuration.Documents;

internal static class ConfigurationTestData
{
    public static MangaEquivalentsDocument CreateValidMangaEquivalentsDocument()
    {
        return new MangaEquivalentsDocument
        {
            Groups =
            [
                new MangaEquivalentGroup
                {
                    Canonical = "Manga Alpha",
                    Aliases = ["Manga A", "The Manga Alpha"]
                }
            ]
        };
    }

    public static SceneTagsDocument CreateValidSceneTagsDocument()
    {
        return new SceneTagsDocument
        {
            Tags = ["official", "asura scan"]
        };
    }

    public static SettingsDocument CreateValidSettingsDocument()
    {
        return SettingsDocumentDefaults.Create();
    }

    public static SourcePriorityDocument CreateValidSourcePriorityDocument()
    {
        return new SourcePriorityDocument
        {
            Sources = ["Source A", "Source B"]
        };
    }
}
