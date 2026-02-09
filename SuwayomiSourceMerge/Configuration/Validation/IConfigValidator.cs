namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Validates a configuration document.
/// </summary>
/// <typeparam name="TDocument">The document type.</typeparam>
public interface IConfigValidator<in TDocument>
{
    /// <summary>
    /// Validates the provided document.
    /// </summary>
    /// <param name="document">The deserialized document to validate.</param>
    /// <param name="file">The source file name.</param>
    /// <returns>A validation result with deterministic errors.</returns>
    ValidationResult Validate(TDocument document, string file);
}
