using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Provides parse-and-validate entrypoints for configuration documents.
/// </summary>
public sealed class ConfigurationSchemaService
{
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
    /// Parses and validates settings YAML.
    /// </summary>
    public ParsedDocument<SettingsDocument> ParseSettings(string file, string yamlContent)
    {
        return _pipeline.ParseAndValidate(file, yamlContent, new SettingsDocumentValidator());
    }

    /// <summary>
    /// Parses and validates manga equivalence YAML.
    /// </summary>
    public ParsedDocument<MangaEquivalentsDocument> ParseMangaEquivalents(string file, string yamlContent)
    {
        return _pipeline.ParseAndValidate(file, yamlContent, new MangaEquivalentsDocumentValidator());
    }

    /// <summary>
    /// Parses and validates scene tags YAML.
    /// </summary>
    public ParsedDocument<SceneTagsDocument> ParseSceneTags(string file, string yamlContent)
    {
        return _pipeline.ParseAndValidate(file, yamlContent, new SceneTagsDocumentValidator());
    }

    /// <summary>
    /// Parses and validates source priority YAML.
    /// </summary>
    public ParsedDocument<SourcePriorityDocument> ParseSourcePriority(string file, string yamlContent)
    {
        return _pipeline.ParseAndValidate(file, yamlContent, new SourcePriorityDocumentValidator());
    }
}
