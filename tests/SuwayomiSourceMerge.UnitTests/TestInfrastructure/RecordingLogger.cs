namespace SuwayomiSourceMerge.UnitTests.TestInfrastructure;

using SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Captures log events for deterministic test assertions.
/// </summary>
internal sealed class RecordingLogger : ISsmLogger
{
	/// <summary>
	/// Gets captured log events.
	/// </summary>
	public List<(LogLevel Level, string EventId, string Message)> Events
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
		Events.Add((level, eventId, message));
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
