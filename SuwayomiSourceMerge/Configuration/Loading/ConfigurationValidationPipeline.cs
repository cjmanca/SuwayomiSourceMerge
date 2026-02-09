using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Runs parse and validation stages for configuration documents.
/// </summary>
public sealed class ConfigurationValidationPipeline
{
    private readonly YamlDocumentParser _parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationValidationPipeline"/> class.
    /// </summary>
    /// <param name="parser">Parser dependency.</param>
    public ConfigurationValidationPipeline(YamlDocumentParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Parses and validates YAML content for a document type.
    /// </summary>
    /// <typeparam name="TDocument">Document type.</typeparam>
    /// <param name="file">Source file name.</param>
    /// <param name="yamlContent">YAML content.</param>
    /// <param name="validator">Document validator.</param>
    /// <returns>A parsed document with accumulated errors.</returns>
    public ParsedDocument<TDocument> ParseAndValidate<TDocument>(
        string file,
        string yamlContent,
        IConfigValidator<TDocument> validator)
        where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(yamlContent);
        ArgumentNullException.ThrowIfNull(validator);

        ParsedDocument<TDocument> parsed = _parser.Parse<TDocument>(file, yamlContent);
        if (!parsed.Validation.IsValid || parsed.Document is null)
        {
            return parsed;
        }

        ValidationResult validation = validator.Validate(parsed.Document, file);
        parsed.Validation.AddRange(validation);
        return parsed;
    }
}
