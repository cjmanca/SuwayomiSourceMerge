namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal interface ISsmLogger
{
    bool IsEnabled(LogLevel level);

    void Log(
        LogLevel level,
        string eventId,
        string message,
        IReadOnlyDictionary<string, string>? context = null);

    void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);

    void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);

    void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);

    void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);
}
