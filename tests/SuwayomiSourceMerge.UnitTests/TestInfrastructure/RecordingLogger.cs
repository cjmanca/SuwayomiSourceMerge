namespace SuwayomiSourceMerge.UnitTests.TestInfrastructure;

using SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Captures log events for deterministic test assertions.
/// </summary>
internal sealed class RecordingLogger : ISsmLogger
{
	/// <summary>
	/// Captured log event model.
	/// </summary>
	/// <param name="Level">Event level.</param>
	/// <param name="EventId">Event id.</param>
	/// <param name="Message">Event message.</param>
	/// <param name="Context">Optional structured context captured at emit time.</param>
	internal sealed record CapturedLogEvent(
		LogLevel Level,
		string EventId,
		string Message,
		IReadOnlyDictionary<string, string>? Context);

	/// <summary>
	/// Gets captured log events.
	/// </summary>
	public List<CapturedLogEvent> Events
	{
		get;
	} = [];

	/// <inheritdoc />
	public bool IsEnabled(LogLevel level)
	{
		return true;
	}

	/// <inheritdoc />
	public void Log(LogLevel level, string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		IReadOnlyDictionary<string, string>? capturedContext = null;
		if (context is not null)
		{
			capturedContext = new Dictionary<string, string>(context, StringComparer.Ordinal);
		}

		Events.Add(new CapturedLogEvent(level, eventId, message, capturedContext));
	}

	/// <inheritdoc />
	public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Trace, eventId, message, context);
	}

	/// <inheritdoc />
	public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Debug, eventId, message, context);
	}

	/// <inheritdoc />
	public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Warning, eventId, message, context);
	}

	/// <inheritdoc />
	public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Error, eventId, message, context);
	}
}
