namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal static class LogLevelParser
{
    public const string SupportedValuesDisplay = "trace, debug, warning, error, none";

    public static bool TryParse(string value, out LogLevel level)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = Normalize(value);
        switch (normalized)
        {
            case "trace":
                level = LogLevel.Trace;
                return true;
            case "debug":
                level = LogLevel.Debug;
                return true;
            case "warning":
                level = LogLevel.Warning;
                return true;
            case "error":
                level = LogLevel.Error;
                return true;
            case "none":
                level = LogLevel.None;
                return true;
            default:
                level = default;
                return false;
        }
    }

    public static LogLevel ParseOrThrow(string? value, string missingValueMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(missingValueMessage);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(missingValueMessage);
        }

        if (TryParse(value, out LogLevel level))
        {
            return level;
        }

        string normalized = Normalize(value);
        throw new InvalidOperationException(
            $"Unsupported logging level '{value}' (normalized: '{normalized}'). Supported values are: {SupportedValuesDisplay}.");
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
