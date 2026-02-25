using System.Globalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Formats deterministic exception diagnostics for configuration-resolution outcome mapping.
/// </summary>
internal static class ResolutionExceptionDiagnosticFormatter
{
	/// <summary>
	/// Creates a compact deterministic exception diagnostic string.
	/// </summary>
	/// <param name="exception">Exception to format.</param>
	/// <returns>Formatted diagnostic string.</returns>
	public static string Format(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return string.Create(
			CultureInfo.InvariantCulture,
			$"{exception.GetType().Name}: {exception.Message}");
	}
}
