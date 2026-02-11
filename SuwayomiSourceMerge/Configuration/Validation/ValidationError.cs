namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Represents a deterministic configuration validation error.
/// </summary>
/// <remarks>
/// Error instances are intended for human-readable startup diagnostics and machine-stable assertions in
/// tests. <see cref="Code"/> should remain stable once released.
/// </remarks>
/// <param name="File">The source file name for the error.</param>
/// <param name="Path">The logical field path.</param>
/// <param name="Code">The stable error code.</param>
/// <param name="Message">The human-readable error description.</param>
public sealed record ValidationError(string File, string Path, string Code, string Message);
