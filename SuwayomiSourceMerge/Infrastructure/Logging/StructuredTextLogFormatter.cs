using System.Globalization;
using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Formats <see cref="LogEvent"/> values into deterministic single-line structured text.
/// </summary>
/// <remarks>
/// Output uses key/value pairs intended for human readability and machine parsing.
/// Context keys are sanitized and values are escaped to preserve line integrity.
/// </remarks>
internal sealed class StructuredTextLogFormatter
{
	/// <summary>
	/// Formats a log event into a structured text line.
	/// </summary>
	/// <param name="logEvent">Event payload to format.</param>
	/// <returns>Single-line text containing timestamp, level, event, message, and optional context fields.</returns>
	public string Format(LogEvent logEvent)
	{
		ArgumentNullException.ThrowIfNull(logEvent);

		StringBuilder builder = new();
		builder.Append("ts=")
			.Append(logEvent.Timestamp.ToString("O", CultureInfo.InvariantCulture))
			.Append(" level=")
			.Append(ToToken(logEvent.Level))
			.Append(" event=\"")
			.Append(Escape(logEvent.EventId))
			.Append("\" msg=\"")
			.Append(Escape(logEvent.Message))
			.Append('"');

		if (logEvent.Context is null || logEvent.Context.Count == 0)
		{
			return builder.ToString();
		}

		foreach (KeyValuePair<string, string> pair in logEvent.Context.OrderBy(entry => entry.Key, StringComparer.Ordinal))
		{
			builder.Append(' ')
				.Append(SanitizeKey(pair.Key))
				.Append("=\"")
				.Append(Escape(pair.Value))
				.Append('"');
		}

		return builder.ToString();
	}

	/// <summary>
	/// Converts a <see cref="LogLevel"/> enum value into its canonical token.
	/// </summary>
	/// <param name="level">Log level to convert.</param>
	/// <returns>Lowercase token used in formatted output.</returns>
	private static string ToToken(LogLevel level)
	{
		return level switch
		{
			LogLevel.Trace => "trace",
			LogLevel.Debug => "debug",
			LogLevel.Warning => "warning",
			LogLevel.Error => "error",
			LogLevel.None => "none",
			_ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unsupported log level.")
		};
	}

	/// <summary>
	/// Escapes value text for safe quoted, single-line output.
	/// </summary>
	/// <param name="value">Raw value to escape.</param>
	/// <returns>Escaped value with slash, quote, and newline characters encoded.</returns>
	private static string Escape(string value)
	{
		return value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal);
	}

	/// <summary>
	/// Normalizes a context key so it contains only safe identifier characters.
	/// </summary>
	/// <param name="value">Raw context key.</param>
	/// <returns>Sanitized key composed of letters, digits, underscore, and period.</returns>
	private static string SanitizeKey(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return "context";
		}

		StringBuilder builder = new(value.Length);
		foreach (char character in value)
		{
			if (char.IsLetterOrDigit(character) || character == '_' || character == '.')
			{
				builder.Append(character);
			}
			else
			{
				builder.Append('_');
			}
		}

		return builder.ToString();
	}
}
