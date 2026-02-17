using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Container runtime capability regression tests.
/// </summary>
public sealed partial class ContainerRuntimeEndToEndTests
{
	private const string ExpectedPinnedMergerfsUpstreamVersion = "2.41.1";
	private const string ExpectedPinnedMergerfsUpperBound = "2.41.2~";

	/// <summary>
	/// Verifies the distributed image bakes <c>cap_sys_admin</c> onto both mergerfs and fusermount3 binaries.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldReportCapSysAdminOnMergerfsAndFusermount3()
	{
		DockerCommandResult result = ExecuteCapabilityAssertion(_fixture.ImageTag);

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
	}

	/// <summary>
	/// Verifies the distributed image ships mergerfs in the exact pinned upstream version series.
	/// </summary>
	[Fact]
	public void Run_Expected_ShouldReportMergerfsVersionWithinPinnedUpstreamSeries()
	{
		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			_fixture.ImageTag,
			"bash",
			"-lc",
			$"actual_version=\"$(dpkg-query -W -f='${{Version}}' mergerfs)\" && printf '%s\\n' \"$actual_version\" && dpkg --compare-versions \"$actual_version\" ge \"{ExpectedPinnedMergerfsUpstreamVersion}~\" && dpkg --compare-versions \"$actual_version\" lt \"{ExpectedPinnedMergerfsUpperBound}\""
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.True(
			result.ExitCode == 0,
			$"Expected mergerfs Debian package version to remain within upstream pinned series '{ExpectedPinnedMergerfsUpstreamVersion}'.{Environment.NewLine}" +
			$"Command: {result.Command}{Environment.NewLine}" +
			$"ExitCode: {result.ExitCode}{Environment.NewLine}" +
			$"Stdout:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}" +
			$"Stderr:{Environment.NewLine}{result.StandardError}");
	}

	/// <summary>
	/// Verifies mergerfs capability checks resolve the binary path through <c>command -v</c>.
	/// </summary>
	[Fact]
	public void Run_Edge_ShouldResolveMergerfsPathViaCommandV_ForCapabilityCheck()
	{
		DockerCommandResult result = _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			_fixture.ImageTag,
			"bash",
			"-lc",
			"MERGERFS_PATH=\"$(command -v mergerfs)\" && test -n \"$MERGERFS_PATH\" && /sbin/getcap \"$MERGERFS_PATH\" | grep -F \"cap_sys_admin=ep\" >/dev/null"
		],
		timeout: TimeSpan.FromMinutes(2));

		Assert.False(result.TimedOut);
		Assert.Equal(0, result.ExitCode);
	}

	/// <summary>
	/// Verifies capability assertions fail for a derived image that strips both file capabilities.
	/// </summary>
	[Fact]
	public void Run_Failure_ShouldFailCapabilityAssertion_WhenDerivedImageStripsCapabilities()
	{
		string strippedCapabilitiesImageTag = BuildDerivedCapabilitiesRemovedImageTag();
		try
		{
			BuildDerivedCapabilitiesRemovedImage(strippedCapabilitiesImageTag);
			DockerCommandResult result = ExecuteCapabilityAssertion(strippedCapabilitiesImageTag);

			Assert.False(result.TimedOut);
			Assert.NotEqual(0, result.ExitCode);
		}
		finally
		{
			RemoveImageBestEffort(strippedCapabilitiesImageTag);
		}
	}

	/// <summary>
	/// Executes the canonical capability assertion command for one image.
	/// </summary>
	/// <param name="imageTag">Image tag.</param>
	/// <returns>Docker command result.</returns>
	private DockerCommandResult ExecuteCapabilityAssertion(string imageTag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(imageTag);

		return _fixture.Runner.Execute(
		[
			"run",
			"--rm",
			imageTag,
			"bash",
			"-lc",
			"/sbin/getcap /usr/bin/fusermount3 | grep -F 'cap_sys_admin=ep' >/dev/null && /sbin/getcap \"$(command -v mergerfs)\" | grep -F 'cap_sys_admin=ep' >/dev/null"
		],
		timeout: TimeSpan.FromMinutes(2));
	}

	/// <summary>
	/// Builds a unique derived image tag for capability-stripping scenarios.
	/// </summary>
	/// <returns>Derived image tag.</returns>
	private static string BuildDerivedCapabilitiesRemovedImageTag()
	{
		return $"ssm-integration-cap-strip:{Guid.NewGuid():N}";
	}

	/// <summary>
	/// Builds one derived image that strips file capabilities from mergerfs and fusermount3.
	/// </summary>
	/// <param name="imageTag">Derived image tag.</param>
	private void BuildDerivedCapabilitiesRemovedImage(string imageTag)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(imageTag);

		string buildContextPath = Path.Combine(Path.GetTempPath(), $"ssm-integration-cap-strip-{Guid.NewGuid():N}");
		Directory.CreateDirectory(buildContextPath);

		try
		{
			string dockerfilePath = Path.Combine(buildContextPath, "Dockerfile");
			string dockerfile = $$"""
			FROM {{_fixture.ImageTag}}
			RUN setcap -r /usr/bin/fusermount3 \
				&& setcap -r "$(command -v mergerfs)"
			""";
			File.WriteAllText(dockerfilePath, dockerfile);

			DockerCommandResult buildResult = _fixture.Runner.Execute(
				["build", "--tag", imageTag, "--file", dockerfilePath, buildContextPath],
				timeout: TimeSpan.FromMinutes(3));
			if (buildResult.TimedOut || buildResult.ExitCode != 0)
			{
				throw new InvalidOperationException(
					$"Failed to build capability-stripped image '{imageTag}'.{Environment.NewLine}" +
					$"Command: {buildResult.Command}{Environment.NewLine}" +
					$"TimedOut: {buildResult.TimedOut}{Environment.NewLine}" +
					$"ExitCode: {buildResult.ExitCode}{Environment.NewLine}" +
					$"Stdout:{Environment.NewLine}{buildResult.StandardOutput}{Environment.NewLine}" +
					$"Stderr:{Environment.NewLine}{buildResult.StandardError}");
			}
		}
		finally
		{
			try
			{
				if (Directory.Exists(buildContextPath))
				{
					Directory.Delete(buildContextPath, recursive: true);
				}
			}
			catch
			{
				// Best-effort temp-build-context cleanup.
			}
		}
	}
}
