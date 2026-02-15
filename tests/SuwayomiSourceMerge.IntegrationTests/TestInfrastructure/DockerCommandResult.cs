namespace SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

/// <summary>
/// Represents one Docker CLI invocation result.
/// </summary>
internal sealed class DockerCommandResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DockerCommandResult"/> class.
	/// </summary>
	/// <param name="command">Rendered command line.</param>
	/// <param name="exitCode">Process exit code.</param>
	/// <param name="standardOutput">Captured standard output.</param>
	/// <param name="standardError">Captured standard error.</param>
	/// <param name="timedOut">Whether process timeout was reached.</param>
	public DockerCommandResult(
		string command,
		int exitCode,
		string standardOutput,
		string standardError,
		bool timedOut)
	{
		Command = command ?? throw new ArgumentNullException(nameof(command));
		StandardOutput = standardOutput ?? throw new ArgumentNullException(nameof(standardOutput));
		StandardError = standardError ?? throw new ArgumentNullException(nameof(standardError));
		ExitCode = exitCode;
		TimedOut = timedOut;
	}

	/// <summary>
	/// Gets the rendered command line.
	/// </summary>
	public string Command
	{
		get;
	}

	/// <summary>
	/// Gets process exit code.
	/// </summary>
	public int ExitCode
	{
		get;
	}

	/// <summary>
	/// Gets captured standard output.
	/// </summary>
	public string StandardOutput
	{
		get;
	}

	/// <summary>
	/// Gets captured standard error.
	/// </summary>
	public string StandardError
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether the process timed out.
	/// </summary>
	public bool TimedOut
	{
		get;
	}
}
