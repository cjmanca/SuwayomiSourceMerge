using SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

namespace SuwayomiSourceMerge.IntegrationTests;

/// <summary>
/// Verifies timeout and process-lifecycle behavior for <see cref="DockerCommandRunner"/>.
/// </summary>
[Collection(DockerIntegrationFixture.COLLECTION_NAME)]
public sealed class DockerCommandRunnerBehaviorTests
{
	/// <summary>
	/// Shared Docker fixture.
	/// </summary>
	private readonly DockerIntegrationFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="DockerCommandRunnerBehaviorTests"/> class.
	/// </summary>
	/// <param name="fixture">Docker integration fixture.</param>
	public DockerCommandRunnerBehaviorTests(DockerIntegrationFixture fixture)
	{
		_fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
	}

	/// <summary>
	/// Verifies timeout path returns promptly and reports timed-out status when command exceeds timeout.
	/// </summary>
	[Fact]
	public void Execute_Failure_ShouldReturnTimedOutWithoutIndefiniteWait_WhenCommandExceedsTimeout()
	{
		DateTimeOffset startedAt = DateTimeOffset.UtcNow;
		DockerCommandResult result = _fixture.Runner.Execute(["events"], timeout: TimeSpan.FromSeconds(1));
		TimeSpan elapsed = DateTimeOffset.UtcNow - startedAt;

		Assert.True(result.TimedOut);
		Assert.True(elapsed < TimeSpan.FromSeconds(20), $"Expected bounded timeout handling but elapsed was {elapsed}.");
	}
}
