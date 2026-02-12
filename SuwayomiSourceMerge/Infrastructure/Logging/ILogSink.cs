namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Represents a low-level destination that accepts fully formatted log lines.
/// </summary>
/// <remarks>
/// Implementations are responsible only for persistence/transport of the final line payload.
/// They should not perform level filtering or message formatting.
/// </remarks>
internal interface ILogSink
{
	/// <summary>
	/// Writes a single formatted line to the sink.
	/// </summary>
	/// <param name="line">Pre-formatted log line text without assumptions about trailing newline handling.</param>
	void WriteLine(string line);
}
