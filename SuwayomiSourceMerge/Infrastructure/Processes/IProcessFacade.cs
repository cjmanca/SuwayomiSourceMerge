using System.Diagnostics;

namespace SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Defines process operations used by the command executor for deterministic testing.
/// </summary>
internal interface IProcessFacade : IDisposable
{
	/// <summary>
	/// Configures process startup settings for a single execution attempt.
	/// </summary>
	/// <param name="startInfo">Process startup settings.</param>
	void ConfigureStartInfo(ProcessStartInfo startInfo);

	/// <summary>
	/// Starts the configured process.
	/// </summary>
	/// <returns><see langword="true"/> when the process starts successfully; otherwise <see langword="false"/>.</returns>
	bool Start();

	/// <summary>
	/// Waits for process exit for the specified interval.
	/// </summary>
	/// <param name="milliseconds">Maximum number of milliseconds to wait.</param>
	/// <returns><see langword="true"/> when the process exits within the interval; otherwise <see langword="false"/>.</returns>
	bool WaitForExit(int milliseconds);

	/// <summary>
	/// Gets a value indicating whether the process has exited.
	/// </summary>
	bool HasExited
	{
		get;
	}

	/// <summary>
	/// Gets the process exit code.
	/// </summary>
	int ExitCode
	{
		get;
	}

	/// <summary>
	/// Gets the process standard output reader.
	/// </summary>
	TextReader StandardOutputReader
	{
		get;
	}

	/// <summary>
	/// Gets the process standard error reader.
	/// </summary>
	TextReader StandardErrorReader
	{
		get;
	}

	/// <summary>
	/// Terminates the process.
	/// </summary>
	/// <param name="entireProcessTree">When <see langword="true"/>, also terminates child processes.</param>
	void Kill(bool entireProcessTree);
}
