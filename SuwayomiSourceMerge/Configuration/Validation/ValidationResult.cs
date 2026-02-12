namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Aggregates validation errors for a document.
/// </summary>
/// <remarks>
/// Instances are mutable collectors used during validation passes. Errors are preserved in insertion
/// order to keep startup diagnostics and tests deterministic.
/// </remarks>
public sealed class ValidationResult
{
	/// <summary>
	/// Internal mutable error storage.
	/// </summary>
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
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is <see langword="null"/>.</exception>
	public void Add(ValidationError error)
	{
		ArgumentNullException.ThrowIfNull(error);
		_errors.Add(error);
	}

	/// <summary>
	/// Adds all validation errors from another result.
	/// </summary>
	/// <param name="other">The source result.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
	public void AddRange(ValidationResult other)
	{
		ArgumentNullException.ThrowIfNull(other);
		_errors.AddRange(other._errors);
	}
}
