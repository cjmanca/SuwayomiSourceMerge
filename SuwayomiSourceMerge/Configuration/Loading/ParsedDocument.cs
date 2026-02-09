using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Represents the parse and validation state of a configuration document.
/// </summary>
/// <typeparam name="TDocument">Document type.</typeparam>
public sealed class ParsedDocument<TDocument>
{
    /// <summary>
    /// Gets the parsed document when parsing succeeded.
    /// </summary>
    public TDocument? Document
    {
        get; init;
    }

    /// <summary>
    /// Gets validation errors produced during parsing or validation.
    /// </summary>
    public ValidationResult Validation { get; init; } = new();
}
