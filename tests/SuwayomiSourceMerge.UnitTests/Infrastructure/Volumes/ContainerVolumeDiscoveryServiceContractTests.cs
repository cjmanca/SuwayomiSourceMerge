namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Volumes;

using SuwayomiSourceMerge.Infrastructure.Volumes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Test-only contract that exposes volume discovery behavior using public test DTOs.
/// </summary>
/// <remarks>
/// This contract keeps reusable contract tests strongly typed without exposing internal runtime interfaces
/// through the public test base class surface.
/// </remarks>
public interface IVolumeDiscoveryContractService
{
    /// <summary>
    /// Discovers source and override volumes under the provided root paths.
    /// </summary>
    /// <param name="sourcesRootPath">Root path whose direct children represent source volumes.</param>
    /// <param name="overrideRootPath">Root path whose direct children represent override volumes.</param>
    /// <returns>A normalized contract result with discovered paths and warnings.</returns>
    VolumeDiscoveryContractResult Discover(string sourcesRootPath, string overrideRootPath);
}

/// <summary>
/// Public test DTO used by reusable volume discovery contract tests.
/// </summary>
public sealed class VolumeDiscoveryContractResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeDiscoveryContractResult"/> class.
    /// </summary>
    /// <param name="sourceVolumePaths">Discovered source volume paths.</param>
    /// <param name="overrideVolumePaths">Discovered override volume paths.</param>
    /// <param name="warnings">Discovery warnings associated with the result.</param>
    public VolumeDiscoveryContractResult(
        IReadOnlyList<string> sourceVolumePaths,
        IReadOnlyList<string> overrideVolumePaths,
        IReadOnlyList<VolumeDiscoveryWarningContract> warnings)
    {
        ArgumentNullException.ThrowIfNull(sourceVolumePaths);
        ArgumentNullException.ThrowIfNull(overrideVolumePaths);
        ArgumentNullException.ThrowIfNull(warnings);

        SourceVolumePaths = sourceVolumePaths.ToArray();
        OverrideVolumePaths = overrideVolumePaths.ToArray();
        Warnings = warnings.ToArray();
    }

    /// <summary>
    /// Gets discovered source volume paths.
    /// </summary>
    public IReadOnlyList<string> SourceVolumePaths
    {
        get;
    }

    /// <summary>
    /// Gets discovered override volume paths.
    /// </summary>
    public IReadOnlyList<string> OverrideVolumePaths
    {
        get;
    }

    /// <summary>
    /// Gets warnings emitted during discovery.
    /// </summary>
    public IReadOnlyList<VolumeDiscoveryWarningContract> Warnings
    {
        get;
    }
}

/// <summary>
/// Public test warning DTO used by volume discovery contract assertions.
/// </summary>
public sealed record VolumeDiscoveryWarningContract
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VolumeDiscoveryWarningContract"/> record.
    /// </summary>
    /// <param name="code">Stable warning code.</param>
    /// <param name="rootPath">Root path associated with the warning.</param>
    /// <param name="message">Human-readable warning message.</param>
    public VolumeDiscoveryWarningContract(string code, string rootPath, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        RootPath = rootPath;
        Message = message;
    }

    /// <summary>
    /// Gets the stable warning code.
    /// </summary>
    public string Code
    {
        get;
    }

    /// <summary>
    /// Gets the root path associated with this warning.
    /// </summary>
    public string RootPath
    {
        get;
    }

    /// <summary>
    /// Gets the warning message.
    /// </summary>
    public string Message
    {
        get;
    }
}

/// <summary>
/// Concrete contract test fixture that runs shared discovery-service contract tests against
/// <see cref="ContainerVolumeDiscoveryService"/>.
/// </summary>
public sealed class ContainerVolumeDiscoveryServiceContractTests : ContainerVolumeDiscoveryServiceContractTestsBase
{
    /// <summary>
    /// Creates the service instance used by contract tests.
    /// </summary>
    /// <returns>A strongly typed contract service adapter for volume discovery tests.</returns>
    protected override IVolumeDiscoveryContractService CreateService()
    {
        return new ContainerVolumeDiscoveryContractAdapter(new ContainerVolumeDiscoveryService());
    }

    /// <summary>
    /// Adapts the internal runtime discovery service into the public test contract surface.
    /// </summary>
    private sealed class ContainerVolumeDiscoveryContractAdapter : IVolumeDiscoveryContractService
    {
        private readonly IContainerVolumeDiscoveryService _inner;

        public ContainerVolumeDiscoveryContractAdapter(IContainerVolumeDiscoveryService inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public VolumeDiscoveryContractResult Discover(string sourcesRootPath, string overrideRootPath)
        {
            ContainerVolumeDiscoveryResult runtimeResult = _inner.Discover(sourcesRootPath, overrideRootPath);
            IReadOnlyList<VolumeDiscoveryWarningContract> warnings = runtimeResult.Warnings
                .Select(warning => new VolumeDiscoveryWarningContract(warning.Code, warning.RootPath, warning.Message))
                .ToArray();

            return new VolumeDiscoveryContractResult(
                runtimeResult.SourceVolumePaths,
                runtimeResult.OverrideVolumePaths,
                warnings);
        }
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

        IVolumeDiscoveryContractService service = CreateService();

        VolumeDiscoveryContractResult result = service.Discover(sourcesRootPath, overrideRootPath);

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

        IVolumeDiscoveryContractService service = CreateService();

        VolumeDiscoveryContractResult result = service.Discover(sourcesRootPath, missingOverrideRootPath);

        Assert.Equal([sourceVolumePath], result.SourceVolumePaths);
        Assert.Empty(result.OverrideVolumePaths);
        VolumeDiscoveryWarningContract warning = Assert.Single(result.Warnings);
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
        IVolumeDiscoveryContractService service = CreateService();

        Assert.ThrowsAny<ArgumentException>(() => service.Discover(sourcesRootPath!, overrideRootPath!));
    }

    /// <summary>
    /// Creates the service instance under test for the current concrete fixture.
    /// </summary>
    /// <returns>A strongly typed test contract service.</returns>
    protected abstract IVolumeDiscoveryContractService CreateService();
}
