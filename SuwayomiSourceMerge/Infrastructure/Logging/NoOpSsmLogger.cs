namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Provides a singleton no-op logger used for optional logging dependencies.
/// </summary>
internal sealed class NoOpSsmLogger : ISsmLogger
{
	/// <summary>
	/// Shared no-op logger instance.
	/// </summary>
	public static NoOpSsmLogger Instance
	{
		get;
	} = new();

	/// <summary>
	/// Prevents external construction.
	/// </summary>
	private NoOpSsmLogger()
	{
	}

	/// <inheritdoc />
	public bool IsEnabled(LogLevel level)
	{
		return false;
	}

	/// <inheritdoc />
	public void Log(
		LogLevel level,
		string eventId,
		string message,
		IReadOnlyDictionary<string, string>? context = null)
	{
	}

	/// <inheritdoc />
	public void Trace(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
	}

	/// <inheritdoc />
	public void Debug(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
	}

	/// <inheritdoc />
	public void Normal(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
	}

	/// <inheritdoc />
	public void Warning(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
	}

	/// <inheritdoc />
	public void Error(string eventId, string message, IReadOnlyDictionary<string, string>? context = null)
	{
	}
}
