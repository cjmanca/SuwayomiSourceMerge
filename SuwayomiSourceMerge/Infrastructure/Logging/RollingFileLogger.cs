namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Logger implementation that filters by level, formats structured events, and writes to a sink.
/// </summary>
/// <remarks>
/// Sink failures are handled in a fail-open manner: the logger reports the failure through a fallback
/// writer and never throws to application callers.
/// </remarks>
internal sealed class RollingFileLogger : ISsmLogger
{
	/// <summary>
	/// Fallback writer used when sink writes fail.
	/// </summary>
	private readonly Action<string> _fallbackErrorWriter;

	/// <summary>
	/// Formatter that converts <see cref="LogEvent"/> values into single-line payloads.
	/// </summary>
	private readonly StructuredTextLogFormatter _formatter;

	/// <summary>
	/// Minimum enabled logging level.
	/// </summary>
	private readonly LogLevel _minimumLevel;

	/// <summary>
	/// Destination sink for formatted log lines.
	/// </summary>
	private readonly ILogSink _sink;

	/// <summary>
	/// Initializes a rolling-file logger with filtering, formatting, and fallback behavior.
	/// </summary>
	/// <param name="minimumLevel">Minimum level that will be emitted.</param>
	/// <param name="sink">Sink that persists formatted lines.</param>
	/// <param name="formatter">Formatter that renders structured events.</param>
	/// <param name="fallbackErrorWriter">Writer used when sink persistence fails.</param>
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

	/// <summary>
	/// Determines whether a level is currently enabled.
	/// </summary>
	/// <param name="level">Level to evaluate.</param>
	/// <returns><see langword="true"/> when enabled; otherwise <see langword="false"/>.</returns>
	public bool IsEnabled(LogLevel level)
	{
		return _minimumLevel != LogLevel.None
			&& level != LogLevel.None
			&& level >= _minimumLevel;
	}

	/// <summary>
	/// Emits a structured log event when the level is enabled.
	/// </summary>
	/// <param name="level">Severity level for the event.</param>
	/// <param name="eventId">Stable event identifier.</param>
	/// <param name="message">Human-readable message.</param>
	/// <param name="context">Optional structured key/value context.</param>
	public void Log(
		LogLevel level,
		string eventId,
		string message,
		IReadOnlyDictionary<string, string>? context = null)
	{
		if (level == LogLevel.None)
		{
			return;
		}

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

	/// <summary>
	/// Emits a trace-level event.
	/// </summary>
	/// <param name="eventId">Stable event identifier.</param>
	/// <param name="message">Human-readable message.</param>
	/// <param name="context">Optional structured key/value context.</param>
	public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Trace, eventId, message, context);
	}

	/// <summary>
	/// Emits a debug-level event.
	/// </summary>
	/// <param name="eventId">Stable event identifier.</param>
	/// <param name="message">Human-readable message.</param>
	/// <param name="context">Optional structured key/value context.</param>
	public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Debug, eventId, message, context);
	}

	/// <summary>
	/// Emits a normal-level event.
	/// </summary>
	/// <param name="eventId">Stable event identifier.</param>
	/// <param name="message">Human-readable message.</param>
	/// <param name="context">Optional structured key/value context.</param>
	public void Normal(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Normal, eventId, message, context);
	}

	/// <summary>
	/// Emits a warning-level event.
	/// </summary>
	/// <param name="eventId">Stable event identifier.</param>
	/// <param name="message">Human-readable message.</param>
	/// <param name="context">Optional structured key/value context.</param>
	public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Warning, eventId, message, context);
	}

	/// <summary>
	/// Emits an error-level event.
	/// </summary>
	/// <param name="eventId">Stable event identifier.</param>
	/// <param name="message">Human-readable message.</param>
	/// <param name="context">Optional structured key/value context.</param>
	public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
		Log(LogLevel.Error, eventId, message, context);
	}

	/// <summary>
	/// Attempts to report a sink write failure through the fallback error writer.
	/// </summary>
	/// <param name="exception">Exception raised by sink persistence.</param>
	/// <param name="eventId">Event identifier associated with the failed write.</param>
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

	/// <summary>
	/// Escapes a fallback field value to preserve single-line stderr output.
	/// </summary>
	/// <param name="value">Raw field value.</param>
	/// <returns>Escaped value with quote, slash, and newline escaping applied.</returns>
	private static string EscapeFallbackValue(string value)
	{
		return value
			.Replace("\\", "\\\\", StringComparison.Ordinal)
			.Replace("\"", "\\\"", StringComparison.Ordinal)
			.Replace("\r", "\\r", StringComparison.Ordinal)
			.Replace("\n", "\\n", StringComparison.Ordinal);
	}
}
