using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Executes parser and validator stages for a configuration document in deterministic order.
/// </summary>
/// <remarks>
/// Parsing always runs first. Validation runs only when parsing succeeds and returns a non-null document.
/// </remarks>
public sealed class ConfigurationValidationPipeline
{
	/// <summary>
	/// Typed YAML parser used as the first stage of the pipeline.
	/// </summary>
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
	/// <returns>A parsed document with accumulated parser and validator errors.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="file"/> is empty or whitespace.</exception>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="yamlContent"/> or <paramref name="validator"/> is <see langword="null"/>.
	/// </exception>
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
