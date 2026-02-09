namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Represents a deterministic configuration validation error.
/// </summary>
/// <param name="File">The source file name for the error.</param>
/// <param name="Path">The logical field path.</param>
/// <param name="Code">The stable error code.</param>
/// <param name="Message">The human-readable error description.</param>
public sealed record ValidationError(string File, string Path, string Code, string Message);
