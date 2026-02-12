using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Provides typed parse-and-validate entrypoints for each canonical configuration document.
/// </summary>
/// <remarks>
/// Callers should use this service instead of invoking parser/validators directly so document loading
/// behavior stays consistent across bootstrap, runtime startup, and tests.
/// </remarks>
public sealed class ConfigurationSchemaService
{
	/// <summary>
	/// Shared pipeline that executes YAML parsing followed by validator execution.
	/// </summary>
	private readonly ConfigurationValidationPipeline _pipeline;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationSchemaService"/> class.
	/// </summary>
	/// <param name="pipeline">Validation pipeline.</param>
	public ConfigurationSchemaService(ConfigurationValidationPipeline pipeline)
	{
		_pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
	}

	/// <summary>
	/// Parses and validates <c>settings.yml</c> content.
	/// </summary>
	/// <param name="file">Logical file name to include in validation errors.</param>
	/// <param name="yamlContent">Raw YAML content for the settings document.</param>
	/// <returns>A parsed settings document and any parse/validation errors.</returns>
	public ParsedDocument<SettingsDocument> ParseSettings(string file, string yamlContent)
	{
		return _pipeline.ParseAndValidate(file, yamlContent, new SettingsDocumentValidator());
	}

	/// <summary>
	/// Parses and validates <c>manga_equivalents.yml</c> content.
	/// </summary>
	/// <param name="file">Logical file name to include in validation errors.</param>
	/// <param name="yamlContent">Raw YAML content for the manga equivalents document.</param>
	/// <returns>A parsed manga equivalents document and any parse/validation errors.</returns>
	public ParsedDocument<MangaEquivalentsDocument> ParseMangaEquivalents(string file, string yamlContent)
	{
		return _pipeline.ParseAndValidate(file, yamlContent, new MangaEquivalentsDocumentValidator());
	}

	/// <summary>
	/// Parses and validates <c>scene_tags.yml</c> content.
	/// </summary>
	/// <param name="file">Logical file name to include in validation errors.</param>
	/// <param name="yamlContent">Raw YAML content for the scene tags document.</param>
	/// <returns>A parsed scene tags document and any parse/validation errors.</returns>
	public ParsedDocument<SceneTagsDocument> ParseSceneTags(string file, string yamlContent)
	{
		return _pipeline.ParseAndValidate(file, yamlContent, new SceneTagsDocumentValidator());
	}

	/// <summary>
	/// Parses and validates <c>source_priority.yml</c> content.
	/// </summary>
	/// <param name="file">Logical file name to include in validation errors.</param>
	/// <param name="yamlContent">Raw YAML content for the source priority document.</param>
	/// <returns>A parsed source priority document and any parse/validation errors.</returns>
	public ParsedDocument<SourcePriorityDocument> ParseSourcePriority(string file, string yamlContent)
	{
		return _pipeline.ParseAndValidate(file, yamlContent, new SourcePriorityDocumentValidator());
	}
}
