using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Carries validated supervisor lifecycle configuration.
/// </summary>
internal sealed class DaemonSupervisorOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DaemonSupervisorOptions"/> class.
	/// </summary>
	/// <param name="statePaths">Resolved supervisor state paths.</param>
	/// <param name="stopTimeout">Graceful stop timeout.</param>
	public DaemonSupervisorOptions(
		SupervisorStatePaths statePaths,
		TimeSpan stopTimeout)
	{
		StatePaths = statePaths ?? throw new ArgumentNullException(nameof(statePaths));
		if (stopTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(stopTimeout), stopTimeout, "Stop timeout must be > 0.");
		}

		StopTimeout = stopTimeout;
	}

	/// <summary>
	/// Gets resolved supervisor state-file paths.
	/// </summary>
	public SupervisorStatePaths StatePaths
	{
		get;
	}

	/// <summary>
	/// Gets graceful stop timeout.
	/// </summary>
	public TimeSpan StopTimeout
	{
		get;
	}

	/// <summary>
	/// Builds supervisor options from a validated settings document.
	/// </summary>
	/// <param name="settings">Settings document.</param>
	/// <returns>Resolved supervisor options.</returns>
	public static DaemonSupervisorOptions FromSettings(SettingsDocument settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		if (settings.Paths?.StateRootPath is null)
		{
			throw new ArgumentException("Settings paths.state_root_path is required.", nameof(settings));
		}

		if (settings.Shutdown?.StopTimeoutSeconds is null)
		{
			throw new ArgumentException("Settings shutdown.stop_timeout_seconds is required.", nameof(settings));
		}

		return new DaemonSupervisorOptions(
			new SupervisorStatePaths(settings.Paths.StateRootPath),
			TimeSpan.FromSeconds(settings.Shutdown.StopTimeoutSeconds.Value));
	}
}
