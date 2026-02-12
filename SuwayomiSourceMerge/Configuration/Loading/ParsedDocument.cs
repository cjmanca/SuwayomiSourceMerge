using SuwayomiSourceMerge.Configuration.Validation;

namespace SuwayomiSourceMerge.Configuration.Loading;

/// <summary>
/// Represents the parse and validation state of a configuration document.
/// </summary>
/// <typeparam name="TDocument">Document type.</typeparam>
/// <remarks>
/// <see cref="Document"/> can be <see langword="null"/> when parsing fails. <see cref="Validation"/> is
/// always initialized and contains all deterministic error details collected so far.
/// </remarks>
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
