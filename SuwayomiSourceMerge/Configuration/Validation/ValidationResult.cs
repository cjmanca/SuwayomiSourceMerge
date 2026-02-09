namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Aggregates validation errors for a document.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = new();

    /// <summary>
    /// Gets all validation errors in deterministic insertion order.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors => _errors;

    /// <summary>
    /// Gets a value indicating whether the result contains no validation errors.
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Adds a validation error to the result.
    /// </summary>
    /// <param name="error">The error to add.</param>
    public void Add(ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        _errors.Add(error);
    }

    /// <summary>
    /// Adds all validation errors from another result.
    /// </summary>
    /// <param name="other">The source result.</param>
    public void AddRange(ValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _errors.AddRange(other._errors);
    }
}
