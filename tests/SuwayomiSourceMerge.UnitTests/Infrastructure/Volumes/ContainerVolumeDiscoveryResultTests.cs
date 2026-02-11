namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Volumes;

using SuwayomiSourceMerge.Infrastructure.Volumes;

/// <summary>
/// Verifies construction behavior for <see cref="ContainerVolumeDiscoveryResult"/>.
/// </summary>
public sealed class ContainerVolumeDiscoveryResultTests
{
    /// <summary>
    /// Ensures constructor assigns provided values to output collections.
    /// </summary>
    [Fact]
    public void Constructor_Expected_ShouldStoreProvidedValues()
    {
        List<string> sourceVolumePaths = ["/sources/disk1"];
        List<string> overrideVolumePaths = ["/override/priority"];
        List<ContainerVolumeDiscoveryWarning> warnings =
        [
            new("VOL-DISC-001", "/override", "missing override root")
        ];

        ContainerVolumeDiscoveryResult result = new(sourceVolumePaths, overrideVolumePaths, warnings);

        Assert.Equal(sourceVolumePaths, result.SourceVolumePaths);
        Assert.Equal(overrideVolumePaths, result.OverrideVolumePaths);
        Assert.Equal(warnings, result.Warnings);
    }

    /// <summary>
    /// Ensures constructor performs defensive copying so later caller mutations do not affect stored state.
    /// </summary>
    [Fact]
    public void Constructor_Edge_ShouldDefensivelyCopyCollections()
    {
        List<string> sourceVolumePaths = ["/sources/disk1"];
        List<string> overrideVolumePaths = ["/override/priority"];
        List<ContainerVolumeDiscoveryWarning> warnings =
        [
            new("VOL-DISC-001", "/override", "missing override root")
        ];

        ContainerVolumeDiscoveryResult result = new(sourceVolumePaths, overrideVolumePaths, warnings);
        sourceVolumePaths.Add("/sources/disk2");
        overrideVolumePaths.Add("/override/disk1");
        warnings.Add(new("VOL-DISC-001", "/sources", "missing sources root"));

        Assert.Single(result.SourceVolumePaths);
        Assert.Single(result.OverrideVolumePaths);
        Assert.Single(result.Warnings);
    }

    /// <summary>
    /// Ensures constructor argument guards throw when required collections are null.
    /// </summary>
    [Fact]
    public void Constructor_Exception_ShouldThrow_WhenAnyCollectionIsNull()
    {
        List<string> sourceVolumePaths = ["/sources/disk1"];
        List<string> overrideVolumePaths = ["/override/priority"];
        List<ContainerVolumeDiscoveryWarning> warnings =
        [
            new("VOL-DISC-001", "/override", "missing override root")
        ];

        Assert.Throws<ArgumentNullException>(() => new ContainerVolumeDiscoveryResult(null!, overrideVolumePaths, warnings));
        Assert.Throws<ArgumentNullException>(() => new ContainerVolumeDiscoveryResult(sourceVolumePaths, null!, warnings));
        Assert.Throws<ArgumentNullException>(() => new ContainerVolumeDiscoveryResult(sourceVolumePaths, overrideVolumePaths, null!));
    }
}

/// <summary>
/// Verifies validation and value semantics for <see cref="ContainerVolumeDiscoveryWarning"/>.
/// </summary>
public sealed class ContainerVolumeDiscoveryWarningTests
{
    /// <summary>
    /// Confirms warning instances preserve field values and use record value equality semantics.
    /// </summary>
    [Fact]
    public void Constructor_Expected_ShouldPreserveValuesAndEquality()
    {
        ContainerVolumeDiscoveryWarning first = new("VOL-DISC-001", "/override", "missing override root");
        ContainerVolumeDiscoveryWarning second = new("VOL-DISC-001", "/override", "missing override root");

        Assert.Equal("VOL-DISC-001", first.Code);
        Assert.Equal("/override", first.RootPath);
        Assert.Equal("missing override root", first.Message);
        Assert.Equal(first, second);
    }

    /// <summary>
    /// Confirms warning messages accept descriptive punctuation and symbolic text used in diagnostics.
    /// </summary>
    [Fact]
    public void Constructor_Edge_ShouldAllowComplexTextValues()
    {
        ContainerVolumeDiscoveryWarning warning = new(
            "VOL-DISC-001",
            "/override/priority",
            "Complex message: [disk-1], punctuation !? and symbols #$%.");

        Assert.Contains("Complex message", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Confirms constructor rejects null, empty, or whitespace values for each required field.
    /// </summary>
    [Fact]
    public void Constructor_Exception_ShouldThrow_WhenAnyInputIsNullOrWhitespace()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning(null!, "/override", "message"));
        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning("", "/override", "message"));
        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning(" ", "/override", "message"));

        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning("VOL-DISC-001", null!, "message"));
        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning("VOL-DISC-001", "", "message"));
        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning("VOL-DISC-001", " ", "message"));

        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning("VOL-DISC-001", "/override", null!));
        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning("VOL-DISC-001", "/override", ""));
        Assert.ThrowsAny<ArgumentException>(() => new ContainerVolumeDiscoveryWarning("VOL-DISC-001", "/override", " "));
    }
}
