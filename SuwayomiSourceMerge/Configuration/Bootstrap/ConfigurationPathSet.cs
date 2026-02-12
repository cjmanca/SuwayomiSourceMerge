using System.IO;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Holds canonical absolute paths for configuration YAML files and their legacy text counterparts.
/// </summary>
/// <remarks>
/// Bootstrap code should construct this once from the configured root and use the exposed properties
/// instead of duplicating file-name constants in multiple locations.
/// </remarks>
internal sealed class ConfigurationPathSet
{
	/// <summary>
	/// Canonical legacy file name used for manga equivalence migration input.
	/// </summary>
	private const string MANGA_EQUIVALENTS_LEGACY_FILE_NAME = "manga_equivalents.txt";

	/// <summary>
	/// Canonical YAML file name used for manga equivalence configuration.
	/// </summary>
	private const string MANGA_EQUIVALENTS_YAML_FILE_NAME = "manga_equivalents.yml";

	/// <summary>
	/// Canonical YAML file name used for scene tag configuration.
	/// </summary>
	private const string SCENE_TAGS_YAML_FILE_NAME = "scene_tags.yml";

	/// <summary>
	/// Canonical YAML file name used for settings configuration.
	/// </summary>
	private const string SETTINGS_YAML_FILE_NAME = "settings.yml";

	/// <summary>
	/// Canonical legacy file name used for source priority migration input.
	/// </summary>
	private const string SOURCE_PRIORITY_LEGACY_FILE_NAME = "source_priority.txt";

	/// <summary>
	/// Canonical YAML file name used for source priority configuration.
	/// </summary>
	private const string SOURCE_PRIORITY_YAML_FILE_NAME = "source_priority.yml";

	/// <summary>
	/// Initializes a new <see cref="ConfigurationPathSet"/> and expands all canonical paths.
	/// </summary>
	/// <param name="configRootPath">User-configured configuration root directory.</param>
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

	/// <summary>
	/// Gets the normalized absolute root directory used for configuration files.
	/// </summary>
	public string ConfigRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the legacy <c>manga_equivalents.txt</c> file.
	/// </summary>
	public string MangaEquivalentsLegacyPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the canonical <c>manga_equivalents.yml</c> file.
	/// </summary>
	public string MangaEquivalentsYamlPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the canonical <c>scene_tags.yml</c> file.
	/// </summary>
	public string SceneTagsYamlPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the canonical <c>settings.yml</c> file.
	/// </summary>
	public string SettingsYamlPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the legacy <c>source_priority.txt</c> file.
	/// </summary>
	public string SourcePriorityLegacyPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the canonical <c>source_priority.yml</c> file.
	/// </summary>
	public string SourcePriorityYamlPath
	{
		get;
	}

	/// <summary>
	/// Creates a path set for a configuration root.
	/// </summary>
	/// <param name="configRootPath">Configuration directory root.</param>
	/// <returns>A fully populated <see cref="ConfigurationPathSet"/>.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="configRootPath"/> is empty or whitespace.</exception>
	public static ConfigurationPathSet FromRoot(string configRootPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(configRootPath);
		return new ConfigurationPathSet(configRootPath);
	}
}
