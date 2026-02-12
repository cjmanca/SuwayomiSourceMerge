using System.Diagnostics;

namespace SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Adapts <see cref="Process"/> to the <see cref="IProcessFacade"/> contract.
/// </summary>
internal sealed class SystemProcessFacade : IProcessFacade
{
	/// <summary>
	/// Backing process instance used for one execution attempt.
	/// </summary>
	private readonly Process _process = new();

	/// <inheritdoc />
	public bool HasExited
	{
		get
		{
			return _process.HasExited;
		}
	}

	/// <inheritdoc />
	public int ExitCode
	{
		get
		{
			return _process.ExitCode;
		}
	}

	/// <inheritdoc />
	public TextReader StandardOutputReader
	{
		get
		{
			return _process.StandardOutput;
		}
	}

	/// <inheritdoc />
	public TextReader StandardErrorReader
	{
		get
		{
			return _process.StandardError;
		}
	}

	/// <inheritdoc />
	public void ConfigureStartInfo(ProcessStartInfo startInfo)
	{
		ArgumentNullException.ThrowIfNull(startInfo);

		_process.StartInfo = startInfo;
	}

	/// <inheritdoc />
	public bool Start()
	{
		return _process.Start();
	}

	/// <inheritdoc />
	public bool WaitForExit(int milliseconds)
	{
		return _process.WaitForExit(milliseconds);
	}

	/// <inheritdoc />
	public void Kill(bool entireProcessTree)
	{
		_process.Kill(entireProcessTree);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		_process.Dispose();
	}
}
