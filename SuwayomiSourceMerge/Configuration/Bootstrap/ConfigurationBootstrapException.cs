using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Represents a bootstrap failure caused by invalid configuration documents.
/// </summary>
public sealed class ConfigurationBootstrapException : Exception
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ConfigurationBootstrapException"/> class.
	/// </summary>
	/// <param name="validationErrors">Validation errors that caused bootstrap to fail.</param>
	public ConfigurationBootstrapException(IReadOnlyList<ValidationError> validationErrors)
		: base("Configuration bootstrap failed due to validation errors.")
	{
		ValidationErrors = validationErrors?.ToArray() ?? throw new ArgumentNullException(nameof(validationErrors));
	}

	/// <summary>
	/// Gets the validation errors that caused the failure.
	/// </summary>
	public IReadOnlyList<ValidationError> ValidationErrors
	{
		get;
	}
}
