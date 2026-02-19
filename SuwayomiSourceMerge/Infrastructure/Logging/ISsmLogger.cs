namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Defines the application logging contract used by runtime services.
/// </summary>
/// <remarks>
/// This interface exposes both generic and convenience APIs.
/// Callers can use <see cref="Log"/> for dynamic levels or level-specific helpers for clarity.
/// </remarks>
internal interface ISsmLogger
{
	/// <summary>
	/// Determines whether the specified level would be emitted by the current logger configuration.
	/// </summary>
	/// <param name="level">Level to evaluate.</param>
	/// <returns><see langword="true"/> when events at <paramref name="level"/> are enabled; otherwise <see langword="false"/>.</returns>
	bool IsEnabled(LogLevel level);

	/// <summary>
	/// Emits a structured log event at the specified level.
	/// </summary>
	/// <param name="level">
	/// Severity level for the event. <see cref="LogLevel.None"/> is a sentinel threshold value and
	/// should not be used as an event level.
	/// </param>
	/// <param name="eventId">Stable event identifier used for correlation and filtering.</param>
	/// <param name="message">Human-readable message describing the event.</param>
	/// <param name="context">Optional key/value context data to include with the event.</param>
	void Log(
		LogLevel level,
		string eventId,
		string message,
		IReadOnlyDictionary<string, string>? context = null);

	/// <summary>
	/// Emits a <see cref="LogLevel.Trace"/> event.
	/// </summary>
	/// <param name="eventId">Stable event identifier used for correlation and filtering.</param>
	/// <param name="message">Human-readable message describing the event.</param>
	/// <param name="context">Optional key/value context data to include with the event.</param>
	void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);

	/// <summary>
	/// Emits a <see cref="LogLevel.Debug"/> event.
	/// </summary>
	/// <param name="eventId">Stable event identifier used for correlation and filtering.</param>
	/// <param name="message">Human-readable message describing the event.</param>
	/// <param name="context">Optional key/value context data to include with the event.</param>
	void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);

	/// <summary>
	/// Emits a <see cref="LogLevel.Normal"/> event.
	/// </summary>
	/// <param name="eventId">Stable event identifier used for correlation and filtering.</param>
	/// <param name="message">Human-readable message describing the event.</param>
	/// <param name="context">Optional key/value context data to include with the event.</param>
	void Normal(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);

	/// <summary>
	/// Emits a <see cref="LogLevel.Warning"/> event.
	/// </summary>
	/// <param name="eventId">Stable event identifier used for correlation and filtering.</param>
	/// <param name="message">Human-readable message describing the event.</param>
	/// <param name="context">Optional key/value context data to include with the event.</param>
	void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);

	/// <summary>
	/// Emits a <see cref="LogLevel.Error"/> event.
	/// </summary>
	/// <param name="eventId">Stable event identifier used for correlation and filtering.</param>
	/// <param name="message">Human-readable message describing the event.</param>
	/// <param name="context">Optional key/value context data to include with the event.</param>
	void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null);
}
