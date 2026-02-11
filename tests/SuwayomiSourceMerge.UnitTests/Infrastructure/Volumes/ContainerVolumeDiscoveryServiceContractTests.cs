namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Volumes;

using SuwayomiSourceMerge.Infrastructure.Volumes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Concrete contract test fixture that runs shared discovery-service contract tests against
/// <see cref="ContainerVolumeDiscoveryService"/>.
/// </summary>
public sealed class ContainerVolumeDiscoveryServiceContractTests : ContainerVolumeDiscoveryServiceContractTestsBase
{
    /// <summary>
    /// Creates the service instance used by contract tests.
    /// </summary>
    /// <returns>A concrete implementation of <see cref="IContainerVolumeDiscoveryService"/>.</returns>
    protected override object CreateService()
    {
        return new ContainerVolumeDiscoveryService();
    }
}

/// <summary>
/// Shared contract tests that any container volume discovery service implementation must satisfy.
/// </summary>
public abstract class ContainerVolumeDiscoveryServiceContractTestsBase
{
    /// <summary>
    /// Verifies valid roots return discovered source and override volumes without warnings.
    /// </summary>
    [Fact]
    public void Discover_ContractExpected_ShouldReturnDiscoveredVolumes_WhenRootsValid()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcesRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
        string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
        string sourceVolumePath = Directory.CreateDirectory(Path.Combine(sourcesRootPath, "disk1")).FullName;
        string overrideVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;

        IContainerVolumeDiscoveryService service = (IContainerVolumeDiscoveryService)CreateService();

        ContainerVolumeDiscoveryResult result = service.Discover(sourcesRootPath, overrideRootPath);

        Assert.Equal([sourceVolumePath], result.SourceVolumePaths);
        Assert.Equal([overrideVolumePath], result.OverrideVolumePaths);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// Verifies a missing root returns warnings while still returning data for available roots.
    /// </summary>
    [Fact]
    public void Discover_ContractEdge_ShouldReturnWarning_WhenOneRootMissing()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcesRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
        string sourceVolumePath = Directory.CreateDirectory(Path.Combine(sourcesRootPath, "disk1")).FullName;
        string missingOverrideRootPath = Path.Combine(temporaryDirectory.Path, "override-missing");

        IContainerVolumeDiscoveryService service = (IContainerVolumeDiscoveryService)CreateService();

        ContainerVolumeDiscoveryResult result = service.Discover(sourcesRootPath, missingOverrideRootPath);

        Assert.Equal([sourceVolumePath], result.SourceVolumePaths);
        Assert.Empty(result.OverrideVolumePaths);
        ContainerVolumeDiscoveryWarning warning = Assert.Single(result.Warnings);
        Assert.Equal("VOL-DISC-001", warning.Code);
    }

    /// <summary>
    /// Verifies invalid root arguments consistently throw argument-related exceptions.
    /// </summary>
    [Theory]
    [InlineData(null, "/override")]
    [InlineData("", "/override")]
    [InlineData(" ", "/override")]
    [InlineData("/sources", null)]
    [InlineData("/sources", "")]
    [InlineData("/sources", " ")]
    public void Discover_ContractException_ShouldThrow_WhenAnyRootPathInvalid(
        string? sourcesRootPath,
        string? overrideRootPath)
    {
        IContainerVolumeDiscoveryService service = (IContainerVolumeDiscoveryService)CreateService();

        Assert.ThrowsAny<ArgumentException>(() => service.Discover(sourcesRootPath!, overrideRootPath!));
    }

    /// <summary>
    /// Creates the service instance under test for the current concrete fixture.
    /// </summary>
    /// <returns>An object that can be cast to <see cref="IContainerVolumeDiscoveryService"/>.</returns>
    protected abstract object CreateService();
}
