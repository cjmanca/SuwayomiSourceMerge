using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Bootstraps configuration files and migrates legacy text configuration files into canonical YAML.
/// </summary>
public sealed class ConfigurationBootstrapService : IConfigurationBootstrapService
{
	private readonly LegacyMangaEquivalentsMigrator _legacyMangaEquivalentsMigrator;
	private readonly LegacySourcePriorityMigrator _legacySourcePriorityMigrator;
	private readonly ConfigurationSchemaService _schemaService;
	private readonly SettingsSelfHealingService _settingsSelfHealingService;
	private readonly YamlDocumentWriter _yamlDocumentWriter;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationBootstrapService"/> class.
	/// </summary>
	/// <param name="schemaService">Schema parser and validator service.</param>
	public ConfigurationBootstrapService(ConfigurationSchemaService schemaService)
		: this(
			schemaService,
			new LegacyMangaEquivalentsMigrator(),
			new LegacySourcePriorityMigrator(),
			new SettingsSelfHealingService(new YamlDocumentParser()),
			new YamlDocumentWriter())
	{
	}

	internal ConfigurationBootstrapService(
		ConfigurationSchemaService schemaService,
		LegacyMangaEquivalentsMigrator legacyMangaEquivalentsMigrator,
		LegacySourcePriorityMigrator legacySourcePriorityMigrator,
		SettingsSelfHealingService settingsSelfHealingService,
		YamlDocumentWriter yamlDocumentWriter)
	{
		_schemaService = schemaService ?? throw new ArgumentNullException(nameof(schemaService));
		_legacyMangaEquivalentsMigrator = legacyMangaEquivalentsMigrator ?? throw new ArgumentNullException(nameof(legacyMangaEquivalentsMigrator));
		_legacySourcePriorityMigrator = legacySourcePriorityMigrator ?? throw new ArgumentNullException(nameof(legacySourcePriorityMigrator));
		_settingsSelfHealingService = settingsSelfHealingService ?? throw new ArgumentNullException(nameof(settingsSelfHealingService));
		_yamlDocumentWriter = yamlDocumentWriter ?? throw new ArgumentNullException(nameof(yamlDocumentWriter));
	}

	/// <inheritdoc />
	public ConfigurationBootstrapResult Bootstrap(string configRootPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(configRootPath);

		ConfigurationPathSet paths = ConfigurationPathSet.FromRoot(configRootPath);
		Directory.CreateDirectory(paths.ConfigRootPath);

		List<ConfigurationBootstrapWarning> warnings = [];
		List<ConfigurationBootstrapFileState> files = [];

		EnsureSettingsYaml(paths, files);
		EnsureMangaEquivalentsYaml(paths, warnings, files);
		EnsureSceneTagsYaml(paths, files);
		EnsureSourcePriorityYaml(paths, warnings, files);

		ParsedDocument<SettingsDocument> settings = ParseDocument(paths.SettingsYamlPath, _schemaService.ParseSettings);
		ParsedDocument<MangaEquivalentsDocument> mangaEquivalents = ParseDocument(paths.MangaEquivalentsYamlPath, _schemaService.ParseMangaEquivalents);
		ParsedDocument<SceneTagsDocument> sceneTags = ParseDocument(paths.SceneTagsYamlPath, _schemaService.ParseSceneTags);
		ParsedDocument<SourcePriorityDocument> sourcePriority = ParseDocument(paths.SourcePriorityYamlPath, _schemaService.ParseSourcePriority);

		List<ValidationError> validationErrors = [];
		validationErrors.AddRange(settings.Validation.Errors);
		validationErrors.AddRange(mangaEquivalents.Validation.Errors);
		validationErrors.AddRange(sceneTags.Validation.Errors);
		validationErrors.AddRange(sourcePriority.Validation.Errors);

		if (validationErrors.Count > 0)
		{
			throw new ConfigurationBootstrapException(validationErrors);
		}

		return new ConfigurationBootstrapResult
		{
			Documents = new ConfigurationDocumentSet
			{
				Settings = settings.Document!,
				MangaEquivalents = mangaEquivalents.Document!,
				SceneTags = sceneTags.Document!,
				SourcePriority = sourcePriority.Document!
			},
			Files = files,
			Warnings = warnings
		};
	}

	private static ParsedDocument<TDocument> ParseDocument<TDocument>(
		string yamlPath,
		Func<string, string, ParsedDocument<TDocument>> parse)
		where TDocument : class
	{
		string yamlContent = File.ReadAllText(yamlPath);
		string fileName = Path.GetFileName(yamlPath);
		return parse(fileName, yamlContent);
	}

	private void EnsureMangaEquivalentsYaml(
		ConfigurationPathSet paths,
		ICollection<ConfigurationBootstrapWarning> warnings,
		ICollection<ConfigurationBootstrapFileState> files)
	{
		if (File.Exists(paths.MangaEquivalentsYamlPath))
		{
			files.Add(new ConfigurationBootstrapFileState("manga_equivalents.yml", paths.MangaEquivalentsYamlPath, false, false, false, false));
			return;
		}

		if (File.Exists(paths.MangaEquivalentsLegacyPath))
		{
			LegacyMigrationResult<MangaEquivalentsDocument> migration = _legacyMangaEquivalentsMigrator.Migrate(paths.MangaEquivalentsLegacyPath);
			_yamlDocumentWriter.Write(paths.MangaEquivalentsYamlPath, migration.Document);

			foreach (ConfigurationBootstrapWarning warning in migration.Warnings)
			{
				warnings.Add(warning);
			}

			files.Add(new ConfigurationBootstrapFileState("manga_equivalents.yml", paths.MangaEquivalentsYamlPath, true, true, false, false));
			return;
		}

		_yamlDocumentWriter.Write(paths.MangaEquivalentsYamlPath, MangaEquivalentsDocumentDefaults.Create());
		files.Add(new ConfigurationBootstrapFileState("manga_equivalents.yml", paths.MangaEquivalentsYamlPath, true, false, true, false));
	}

	private void EnsureSceneTagsYaml(
		ConfigurationPathSet paths,
		ICollection<ConfigurationBootstrapFileState> files)
	{
		if (File.Exists(paths.SceneTagsYamlPath))
		{
			files.Add(new ConfigurationBootstrapFileState("scene_tags.yml", paths.SceneTagsYamlPath, false, false, false, false));
			return;
		}

		_yamlDocumentWriter.Write(paths.SceneTagsYamlPath, SceneTagsDocumentDefaults.Create());
		files.Add(new ConfigurationBootstrapFileState("scene_tags.yml", paths.SceneTagsYamlPath, true, false, true, false));
	}

	private void EnsureSettingsYaml(
		ConfigurationPathSet paths,
		ICollection<ConfigurationBootstrapFileState> files)
	{
		if (File.Exists(paths.SettingsYamlPath))
		{
			SettingsSelfHealingResult selfHealingResult = _settingsSelfHealingService.SelfHeal(paths.SettingsYamlPath);
			if (selfHealingResult.WasHealed)
			{
				_yamlDocumentWriter.Write(paths.SettingsYamlPath, selfHealingResult.Document);
			}

			files.Add(new ConfigurationBootstrapFileState("settings.yml", paths.SettingsYamlPath, false, false, selfHealingResult.WasHealed, selfHealingResult.WasHealed));
			return;
		}

		_yamlDocumentWriter.Write(paths.SettingsYamlPath, SettingsDocumentDefaults.Create());
		files.Add(new ConfigurationBootstrapFileState("settings.yml", paths.SettingsYamlPath, true, false, true, false));
	}

	private void EnsureSourcePriorityYaml(
		ConfigurationPathSet paths,
		ICollection<ConfigurationBootstrapWarning> warnings,
		ICollection<ConfigurationBootstrapFileState> files)
	{
		if (File.Exists(paths.SourcePriorityYamlPath))
		{
			files.Add(new ConfigurationBootstrapFileState("source_priority.yml", paths.SourcePriorityYamlPath, false, false, false, false));
			return;
		}

		if (File.Exists(paths.SourcePriorityLegacyPath))
		{
			LegacyMigrationResult<SourcePriorityDocument> migration = _legacySourcePriorityMigrator.Migrate(paths.SourcePriorityLegacyPath);
			_yamlDocumentWriter.Write(paths.SourcePriorityYamlPath, migration.Document);

			foreach (ConfigurationBootstrapWarning warning in migration.Warnings)
			{
				warnings.Add(warning);
			}

			files.Add(new ConfigurationBootstrapFileState("source_priority.yml", paths.SourcePriorityYamlPath, true, true, false, false));
			return;
		}

		_yamlDocumentWriter.Write(paths.SourcePriorityYamlPath, SourcePriorityDocumentDefaults.Create());
		files.Add(new ConfigurationBootstrapFileState("source_priority.yml", paths.SourcePriorityYamlPath, true, false, true, false));
	}
}
