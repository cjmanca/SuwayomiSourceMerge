namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal sealed record LogEvent
{
    public LogEvent(
        DateTimeOffset timestamp,
        LogLevel level,
        string eventId,
        string message,
        IReadOnlyDictionary<string, string>? context)
    {
        Timestamp = timestamp;
        Level = level;
        EventId = string.IsNullOrWhiteSpace(eventId)
            ? throw new ArgumentException("Event identifier is required.", nameof(eventId))
            : eventId;
        Message = string.IsNullOrWhiteSpace(message)
            ? throw new ArgumentException("Message is required.", nameof(message))
            : message;
        Context = context;
    }

    public DateTimeOffset Timestamp
    {
        get;
    }

    public LogLevel Level
    {
        get;
    }

    public string EventId
    {
        get;
    }

    public string Message
    {
        get;
    }

    public IReadOnlyDictionary<string, string>? Context
    {
        get;
    }
}
