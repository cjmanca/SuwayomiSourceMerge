using System.IO;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

internal sealed class ConfigurationPathSet
{
    private const string MANGA_EQUIVALENTS_LEGACY_FILE_NAME = "manga_equivalents.txt";
    private const string MANGA_EQUIVALENTS_YAML_FILE_NAME = "manga_equivalents.yml";
    private const string SCENE_TAGS_YAML_FILE_NAME = "scene_tags.yml";
    private const string SETTINGS_YAML_FILE_NAME = "settings.yml";
    private const string SOURCE_PRIORITY_LEGACY_FILE_NAME = "source_priority.txt";
    private const string SOURCE_PRIORITY_YAML_FILE_NAME = "source_priority.yml";

    private ConfigurationPathSet(string configRootPath)
    {
        ConfigRootPath = Path.GetFullPath(configRootPath);
        SettingsYamlPath = Path.Combine(ConfigRootPath, SETTINGS_YAML_FILE_NAME);
        MangaEquivalentsYamlPath = Path.Combine(ConfigRootPath, MANGA_EQUIVALENTS_YAML_FILE_NAME);
        SceneTagsYamlPath = Path.Combine(ConfigRootPath, SCENE_TAGS_YAML_FILE_NAME);
        SourcePriorityYamlPath = Path.Combine(ConfigRootPath, SOURCE_PRIORITY_YAML_FILE_NAME);
        MangaEquivalentsLegacyPath = Path.Combine(ConfigRootPath, MANGA_EQUIVALENTS_LEGACY_FILE_NAME);
        SourcePriorityLegacyPath = Path.Combine(ConfigRootPath, SOURCE_PRIORITY_LEGACY_FILE_NAME);
    }

    public string ConfigRootPath
    {
        get;
    }

    public string MangaEquivalentsLegacyPath
    {
        get;
    }

    public string MangaEquivalentsYamlPath
    {
        get;
    }

    public string SceneTagsYamlPath
    {
        get;
    }

    public string SettingsYamlPath
    {
        get;
    }

    public string SourcePriorityLegacyPath
    {
        get;
    }

    public string SourcePriorityYamlPath
    {
        get;
    }

    public static ConfigurationPathSet FromRoot(string configRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configRootPath);
        return new ConfigurationPathSet(configRootPath);
    }
}
