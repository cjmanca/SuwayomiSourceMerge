namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Resolves canonical supervisor state-file paths under a configured state root.
/// </summary>
internal sealed class SupervisorStatePaths
{
	/// <summary>
	/// Canonical supervisor PID file name.
	/// </summary>
	public const string DaemonPidFileName = "daemon.pid";

	/// <summary>
	/// Canonical supervisor lock file name.
	/// </summary>
	public const string SupervisorLockFileName = "supervisor.lock";

	/// <summary>
	/// Initializes a new instance of the <see cref="SupervisorStatePaths"/> class.
	/// </summary>
	/// <param name="stateRootPath">State root directory path.</param>
	public SupervisorStatePaths(string stateRootPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(stateRootPath);

		StateRootPath = Path.GetFullPath(stateRootPath);
		DaemonPidFilePath = Path.Combine(StateRootPath, DaemonPidFileName);
		SupervisorLockFilePath = Path.Combine(StateRootPath, SupervisorLockFileName);
	}

	/// <summary>
	/// Gets the normalized state root path.
	/// </summary>
	public string StateRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the supervisor PID file path.
	/// </summary>
	public string DaemonPidFilePath
	{
		get;
	}

	/// <summary>
	/// Gets the supervisor lock file path.
	/// </summary>
	public string SupervisorLockFilePath
	{
		get;
	}
}
