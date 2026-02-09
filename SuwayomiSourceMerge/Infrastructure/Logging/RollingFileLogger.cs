namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal sealed class RollingFileLogger : ISsmLogger
{
    private readonly Action<string> _fallbackErrorWriter;
    private readonly StructuredTextLogFormatter _formatter;
    private readonly LogLevel _minimumLevel;
    private readonly ILogSink _sink;

    public RollingFileLogger(
        LogLevel minimumLevel,
        ILogSink sink,
        StructuredTextLogFormatter formatter,
        Action<string> fallbackErrorWriter)
    {
        _minimumLevel = minimumLevel;
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _fallbackErrorWriter = fallbackErrorWriter ?? throw new ArgumentNullException(nameof(fallbackErrorWriter));
    }

    public bool IsEnabled(LogLevel level)
    {
        return _minimumLevel != LogLevel.None && level >= _minimumLevel;
    }

    public void Log(
        LogLevel level,
        string eventId,
        string message,
        IReadOnlyDictionary<string, string>? context = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        LogEvent logEvent = new(DateTimeOffset.UtcNow, level, eventId, message, context);
        string line = _formatter.Format(logEvent);

        try
        {
            _sink.WriteLine(line);
        }
        catch (Exception ex)
        {
            TryWriteFallbackError(ex, eventId);
        }
    }

    public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
    {
        Log(LogLevel.Trace, eventId, message, context);
    }

    public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
    {
        Log(LogLevel.Debug, eventId, message, context);
    }

    public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
    {
        Log(LogLevel.Warning, eventId, message, context);
    }

    public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
    {
        Log(LogLevel.Error, eventId, message, context);
    }

    private void TryWriteFallbackError(Exception exception, string eventId)
    {
        try
        {
            _fallbackErrorWriter(
                $"[{DateTimeOffset.UtcNow:O}] logging_failure event=\"{EscapeFallbackValue(eventId)}\" error_type=\"{EscapeFallbackValue(exception.GetType().Name)}\" error_message=\"{EscapeFallbackValue(exception.Message)}\"");
        }
        catch
        {
            // Fallback writes must never crash the process.
        }
    }

    private static string EscapeFallbackValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }
}
