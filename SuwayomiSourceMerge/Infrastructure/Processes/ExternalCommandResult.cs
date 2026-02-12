namespace SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Represents the outcome category for an external command execution attempt.
/// </summary>
internal enum ExternalCommandOutcome
{
	/// <summary>
	/// The process exited successfully with exit code 0.
	/// </summary>
	Success,

	/// <summary>
	/// The process exited with a non-zero exit code.
	/// </summary>
	NonZeroExit,

	/// <summary>
	/// The process did not finish before the configured timeout expired.
	/// </summary>
	TimedOut,

	/// <summary>
	/// The process execution was canceled by the caller.
	/// </summary>
	Cancelled,

	/// <summary>
	/// The process could not be started.
	/// </summary>
	StartFailed
}

/// <summary>
/// Represents a classified failure reason for an external command execution attempt.
/// </summary>
internal enum ExternalCommandFailureKind
{
	/// <summary>
	/// No classified failure reason applies.
	/// </summary>
	None,

	/// <summary>
	/// The executable was not found on the host.
	/// </summary>
	ToolNotFound,

	/// <summary>
	/// The process failed to start for reasons other than missing executable.
	/// </summary>
	StartFailure
}

/// <summary>
/// Represents the result of an external command execution attempt.
/// </summary>
internal sealed class ExternalCommandResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExternalCommandResult"/> class.
	/// </summary>
	/// <param name="outcome">Outcome category for the command execution.</param>
	/// <param name="failureKind">Classified failure kind for startup failures.</param>
	/// <param name="exitCode">Process exit code when available.</param>
	/// <param name="standardOutput">Captured standard output text.</param>
	/// <param name="standardError">Captured standard error text.</param>
	/// <param name="isStandardOutputTruncated">Whether captured standard output exceeded the configured cap.</param>
	/// <param name="isStandardErrorTruncated">Whether captured standard error exceeded the configured cap.</param>
	/// <param name="elapsed">Elapsed execution duration.</param>
	public ExternalCommandResult(
		ExternalCommandOutcome outcome,
		ExternalCommandFailureKind failureKind,
		int? exitCode,
		string standardOutput,
		string standardError,
		bool isStandardOutputTruncated,
		bool isStandardErrorTruncated,
		TimeSpan elapsed)
	{
		ArgumentNullException.ThrowIfNull(standardOutput);
		ArgumentNullException.ThrowIfNull(standardError);

		Outcome = outcome;
		FailureKind = failureKind;
		ExitCode = exitCode;
		StandardOutput = standardOutput;
		StandardError = standardError;
		IsStandardOutputTruncated = isStandardOutputTruncated;
		IsStandardErrorTruncated = isStandardErrorTruncated;
		Elapsed = elapsed;
	}

	/// <summary>
	/// Gets the execution outcome.
	/// </summary>
	public ExternalCommandOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets the classified failure kind when a startup failure occurs.
	/// </summary>
	public ExternalCommandFailureKind FailureKind
	{
		get;
	}

	/// <summary>
	/// Gets the process exit code when available.
	/// </summary>
	public int? ExitCode
	{
		get;
	}

	/// <summary>
	/// Gets the captured standard output text.
	/// </summary>
	public string StandardOutput
	{
		get;
	}

	/// <summary>
	/// Gets the captured standard error text.
	/// </summary>
	public string StandardError
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether standard output was truncated due to configured capture limits.
	/// </summary>
	public bool IsStandardOutputTruncated
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether standard error was truncated due to configured capture limits.
	/// </summary>
	public bool IsStandardErrorTruncated
	{
		get;
	}

	/// <summary>
	/// Gets the elapsed execution duration.
	/// </summary>
	public TimeSpan Elapsed
	{
		get;
	}
}
