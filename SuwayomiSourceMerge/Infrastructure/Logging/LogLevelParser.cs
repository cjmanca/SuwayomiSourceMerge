namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal static class LogLevelParser
{
    private static readonly (string Token, LogLevel Level)[] SupportedTokens =
    [
        ("trace", LogLevel.Trace),
        ("debug", LogLevel.Debug),
        ("warning", LogLevel.Warning),
        ("error", LogLevel.Error),
        ("none", LogLevel.None)
    ];

    public static string SupportedValuesDisplay
    {
        get
        {
            return string.Join(", ", SupportedTokens.Select(static token => token.Token));
        }
    }

    public static bool TryParse(string? value, out LogLevel level)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            level = default;
            return false;
        }

        string normalized = Normalize(value);
        foreach ((string token, LogLevel parsedLevel) in SupportedTokens)
        {
            if (!string.Equals(token, normalized, StringComparison.Ordinal))
            {
                continue;
            }

            level = parsedLevel;
            return true;
        }

        level = default;
        return false;
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
