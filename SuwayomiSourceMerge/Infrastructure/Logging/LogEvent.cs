namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Immutable structured representation of a single log event.
/// </summary>
internal sealed record LogEvent
{
	/// <summary>
	/// Creates a log event instance.
	/// </summary>
	/// <param name="timestamp">UTC timestamp when the event occurred.</param>
	/// <param name="level">Severity level for the event.</param>
	/// <param name="eventId">Stable event identifier used for correlation.</param>
	/// <param name="message">Human-readable message for the event.</param>
	/// <param name="context">Optional structured key/value context data.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="eventId"/> or <paramref name="message"/> is null, empty, or whitespace.
	/// </exception>
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

	/// <summary>
	/// Gets the UTC timestamp associated with the event.
	/// </summary>
	public DateTimeOffset Timestamp
	{
		get;
	}

	/// <summary>
	/// Gets the severity level for the event.
	/// </summary>
	public LogLevel Level
	{
		get;
	}

	/// <summary>
	/// Gets the stable event identifier.
	/// </summary>
	public string EventId
	{
		get;
	}

	/// <summary>
	/// Gets the human-readable event message.
	/// </summary>
	public string Message
	{
		get;
	}

	/// <summary>
	/// Gets optional structured key/value context values.
	/// </summary>
	public IReadOnlyDictionary<string, string>? Context
	{
		get;
	}
}
