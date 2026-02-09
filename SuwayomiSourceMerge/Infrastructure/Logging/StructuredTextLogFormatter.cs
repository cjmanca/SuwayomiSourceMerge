using System.Globalization;
using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal sealed class StructuredTextLogFormatter
{
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

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

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
