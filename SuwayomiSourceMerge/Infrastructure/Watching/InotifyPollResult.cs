namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Classifies one inotify polling attempt.
/// </summary>
internal enum InotifyPollOutcome
{
	/// <summary>
	/// Poll completed and output was parsed.
	/// </summary>
	Success,

	/// <summary>
	/// Poll completed without events due to timeout.
	/// </summary>
	TimedOut,

	/// <summary>
	/// Poll failed because <c>inotifywait</c> was unavailable.
	/// </summary>
	ToolNotFound,

	/// <summary>
	/// Poll failed for reasons other than timeout or missing tool.
	/// </summary>
	CommandFailed
}

/// <summary>
/// Represents one inotify polling result with parsed events and non-fatal warnings.
/// </summary>
internal sealed class InotifyPollResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InotifyPollResult"/> class.
	/// </summary>
	/// <param name="outcome">Polling outcome classification.</param>
	/// <param name="events">Parsed event records.</param>
	/// <param name="warnings">Non-fatal parse or execution warnings.</param>
	public InotifyPollResult(
		InotifyPollOutcome outcome,
		IReadOnlyList<InotifyEventRecord> events,
		IReadOnlyList<string> warnings)
	{
		ArgumentNullException.ThrowIfNull(events);
		ArgumentNullException.ThrowIfNull(warnings);

		Outcome = outcome;
		Events = events;
		Warnings = warnings;
	}

	/// <summary>
	/// Gets polling outcome classification.
	/// </summary>
	public InotifyPollOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets parsed inotify events.
	/// </summary>
	public IReadOnlyList<InotifyEventRecord> Events
	{
		get;
	}

	/// <summary>
	/// Gets non-fatal warnings emitted while polling or parsing.
	/// </summary>
	public IReadOnlyList<string> Warnings
	{
		get;
	}
}
