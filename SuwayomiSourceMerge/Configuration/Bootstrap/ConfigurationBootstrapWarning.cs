namespace SuwayomiSourceMerge.Configuration.Bootstrap;

/// <summary>
/// Represents a non-fatal bootstrap or migration warning.
/// </summary>
/// <param name="Code">Stable warning code.</param>
/// <param name="File">Source file name associated with the warning.</param>
/// <param name="Line">1-based source line number associated with the warning.</param>
/// <param name="Message">Human-readable warning detail.</param>
public sealed record ConfigurationBootstrapWarning(string Code, string File, int Line, string Message);
