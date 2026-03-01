namespace SuwayomiSourceMerge.UnitTests.Application.Supervision;

using SuwayomiSourceMerge.Application.Supervision;
using SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="DaemonSupervisorOptions"/>.
/// </summary>
public sealed class DaemonSupervisorOptionsTests
{
	/// <summary>
	/// Verifies options map correctly from valid settings.
	/// </summary>
	[Fact]
	public void FromSettings_Expected_ShouldMapStatePathAndStopTimeout()
	{
		SettingsDocument settings = SettingsDocumentDefaults.Create();

		DaemonSupervisorOptions options = DaemonSupervisorOptions.FromSettings(settings);

		Assert.Equal(Path.GetFullPath("/ssm/state"), options.StatePaths.StateRootPath);
		Assert.Equal(Path.Combine(Path.GetFullPath("/ssm/state"), "daemon.pid"), options.StatePaths.DaemonPidFilePath);
		Assert.Equal(Path.Combine(Path.GetFullPath("/ssm/state"), "supervisor.lock"), options.StatePaths.SupervisorLockFilePath);
		Assert.Equal(TimeSpan.FromSeconds(120), options.StopTimeout);
	}

	/// <summary>
	/// Verifies constructor rejects non-positive timeout values.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenStopTimeoutInvalid()
	{
		SupervisorStatePaths statePaths = new("/ssm/state");

		ArgumentOutOfRangeException zeroStopTimeoutException = Assert.Throws<ArgumentOutOfRangeException>(() => new DaemonSupervisorOptions(statePaths, TimeSpan.Zero));
		ArgumentOutOfRangeException negativeStopTimeoutException = Assert.Throws<ArgumentOutOfRangeException>(() => new DaemonSupervisorOptions(statePaths, TimeSpan.FromSeconds(-1)));

		Assert.Equal("stopTimeout", zeroStopTimeoutException.ParamName);
		Assert.Equal("stopTimeout", negativeStopTimeoutException.ParamName);
	}

	/// <summary>
	/// Verifies settings mapping guards reject missing required values.
	/// </summary>
	[Fact]
	public void FromSettings_Failure_ShouldThrow_WhenRequiredFieldsMissing()
	{
		SettingsDocument missingStateRoot = new()
		{
			Paths = new SettingsPathsSection(),
			Shutdown = new SettingsShutdownSection
			{
				StopTimeoutSeconds = 120
			}
		};

		SettingsDocument missingStopTimeout = new()
		{
			Paths = new SettingsPathsSection
			{
				StateRootPath = "/ssm/state"
			},
			Shutdown = new SettingsShutdownSection()
		};

		ArgumentException missingStateRootException = Assert.Throws<ArgumentException>(() => DaemonSupervisorOptions.FromSettings(missingStateRoot));
		ArgumentException missingStopTimeoutException = Assert.Throws<ArgumentException>(() => DaemonSupervisorOptions.FromSettings(missingStopTimeout));
		ArgumentNullException nullSettingsException = Assert.Throws<ArgumentNullException>(() => DaemonSupervisorOptions.FromSettings(null!));

		Assert.Equal("settings", missingStateRootException.ParamName);
		Assert.Equal("settings", missingStopTimeoutException.ParamName);
		Assert.Equal("settings", nullSettingsException.ParamName);
	}
}
