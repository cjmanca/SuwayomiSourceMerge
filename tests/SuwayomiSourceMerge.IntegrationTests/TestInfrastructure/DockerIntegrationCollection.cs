namespace SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

/// <summary>
/// Defines shared docker fixture collection for integration tests.
/// </summary>
[CollectionDefinition(DockerIntegrationFixture.CollectionName, DisableParallelization = true)]
public sealed class DockerIntegrationCollection : ICollectionFixture<DockerIntegrationFixture>
{
}
