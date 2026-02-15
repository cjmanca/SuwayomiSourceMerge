namespace SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

/// <summary>
/// Provides shared Docker image setup for integration tests.
/// </summary>
public sealed class DockerIntegrationFixture : IAsyncLifetime
{
	/// <summary>
	/// Gets collection name for this fixture.
	/// </summary>
	public const string COLLECTION_NAME = "docker-integration";

	/// <summary>
	/// Initializes a new instance of the <see cref="DockerIntegrationFixture"/> class.
	/// </summary>
	public DockerIntegrationFixture()
	{
		Runner = new DockerCommandRunner();
		ImageTag = $"ssm-integration:{Guid.NewGuid():N}";
	}

	/// <summary>
	/// Gets Docker command runner.
	/// </summary>
	internal DockerCommandRunner Runner
	{
		get;
	}

	/// <summary>
	/// Gets built integration image tag.
	/// </summary>
	public string ImageTag
	{
		get;
	}

	/// <inheritdoc />
	public Task InitializeAsync()
	{
		string repositoryRootPath = FindRepositoryRootPath();
		Runner.EnsureDockerDaemonAvailable();
		Runner.BuildImage(repositoryRootPath, "Dockerfile", ImageTag);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task DisposeAsync()
	{
		try
		{
			Runner.Execute(["rmi", "--force", ImageTag], timeout: TimeSpan.FromSeconds(30));
		}
		catch
		{
			// Image cleanup is best-effort and should not fail the test run teardown path.
		}

		return Task.CompletedTask;
	}

	/// <summary>
	/// Locates repository root by walking upward from test output base path.
	/// </summary>
	/// <returns>Repository root path.</returns>
	private static string FindRepositoryRootPath()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			string candidate = directory.FullName;
			if (File.Exists(Path.Combine(candidate, "SuwayomiSourceMerge.slnx")) &&
				File.Exists(Path.Combine(candidate, "Dockerfile")))
			{
				return candidate;
			}

			directory = directory.Parent;
		}

		throw new InvalidOperationException("Could not locate repository root for integration tests.");
	}
}
