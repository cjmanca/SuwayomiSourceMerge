using SuwayomiSourceMerge.Configuration.Validation;

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Parses YAML configuration documents into strongly typed models.
/// </summary>
public sealed class YamlDocumentParser
{
    private const string ParseFailureCode = "CFG-YAML-001";
    private const string EmptyDocumentCode = "CFG-YAML-002";

    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlDocumentParser"/> class.
    /// </summary>
    public YamlDocumentParser()
    {
        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Parses the YAML document into the target type.
    /// </summary>
    /// <typeparam name="TDocument">Target document type.</typeparam>
    /// <param name="file">Source file name.</param>
    /// <param name="yamlContent">YAML content.</param>
    /// <returns>A parsed document with parse errors when applicable.</returns>
    public ParsedDocument<TDocument> Parse<TDocument>(string file, string yamlContent)
        where TDocument : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(file);
        ArgumentNullException.ThrowIfNull(yamlContent);

        ValidationResult validation = new();

        try
        {
            TDocument? document = _deserializer.Deserialize<TDocument>(yamlContent);
            if (document is null)
            {
                validation.Add(new ValidationError(file, "$", EmptyDocumentCode, "YAML document is empty."));
                return new ParsedDocument<TDocument> { Validation = validation };
            }

            return new ParsedDocument<TDocument>
            {
                Document = document,
                Validation = validation
            };
        }
        catch (YamlException ex)
        {
            string path = $"line:{ex.Start.Line},col:{ex.Start.Column}";
            validation.Add(new ValidationError(file, path, ParseFailureCode, ex.Message));
            return new ParsedDocument<TDocument> { Validation = validation };
        }
    }
}
