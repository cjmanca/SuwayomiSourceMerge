namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Volumes;

using SuwayomiSourceMerge.Infrastructure.Volumes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies constructor, happy-path, edge-path, and failure-path behavior for <see cref="ContainerVolumeDiscoveryService"/>.
/// </summary>
public sealed class ContainerVolumeDiscoveryServiceTests
{
    /// <summary>
    /// Confirms the default constructor creates a usable service with the production filesystem implementation.
    /// </summary>
    [Fact]
    public void Constructor_Expected_ShouldCreateInstance_WhenUsingDefaultConstructor()
    {
        ContainerVolumeDiscoveryService service = new();

        Assert.NotNull(service);
    }

    /// <summary>
    /// Confirms the internal constructor accepts a supplied filesystem dependency for deterministic testing.
    /// </summary>
    [Fact]
    public void Constructor_Expected_ShouldCreateInstance_WhenFileSystemProvided()
    {
        ContainerVolumeDiscoveryService service = new(new FakeContainerVolumeFileSystem());

        Assert.NotNull(service);
    }

    /// <summary>
    /// Ensures constructor argument validation rejects a null filesystem dependency.
    /// </summary>
    [Fact]
    public void Constructor_Exception_ShouldThrow_WhenFileSystemIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new ContainerVolumeDiscoveryService(null!));
    }

    /// <summary>
    /// Validates that discovery returns sorted direct-child source and override volumes when both roots exist.
    /// </summary>
    [Fact]
    public void Discover_Expected_ShouldReturnSortedDirectChildDirectories_ForBothRoots()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcesRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
        string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;

        string sourceVolumeBPath = Directory.CreateDirectory(Path.Combine(sourcesRootPath, "disk2")).FullName;
        string sourceVolumeAPath = Directory.CreateDirectory(Path.Combine(sourcesRootPath, "disk1")).FullName;
        string overrideVolumeBPath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
        string overrideVolumeAPath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "disk1")).FullName;

        ContainerVolumeDiscoveryService service = new();

        ContainerVolumeDiscoveryResult result = service.Discover(sourcesRootPath, overrideRootPath);

        Assert.Equal([sourceVolumeAPath, sourceVolumeBPath], result.SourceVolumePaths);
        Assert.Equal([overrideVolumeAPath, overrideVolumeBPath], result.OverrideVolumePaths);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// Validates that existing but empty roots produce empty volume lists without warnings.
    /// </summary>
    [Fact]
    public void Discover_Edge_ShouldReturnEmptyLists_WhenRootsExistWithoutVolumes()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcesRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
        string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;

        ContainerVolumeDiscoveryService service = new();

        ContainerVolumeDiscoveryResult result = service.Discover(sourcesRootPath, overrideRootPath);

        Assert.Empty(result.SourceVolumePaths);
        Assert.Empty(result.OverrideVolumePaths);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// Confirms discovery ignores regular files and nested directories and only returns direct-child directories.
    /// </summary>
    [Fact]
    public void Discover_Edge_ShouldIgnoreFilesAndNestedDirectories()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string sourcesRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
        string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;

        string sourceVolumePath = Directory.CreateDirectory(Path.Combine(sourcesRootPath, "disk1")).FullName;
        string overrideVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
        Directory.CreateDirectory(Path.Combine(sourceVolumePath, "Source Name", "Manga Title"));
        Directory.CreateDirectory(Path.Combine(overrideVolumePath, "Manga Title", "Chapter 1"));
        File.WriteAllText(Path.Combine(sourcesRootPath, "source-file.txt"), "ignore");
        File.WriteAllText(Path.Combine(overrideRootPath, "override-file.txt"), "ignore");

        ContainerVolumeDiscoveryService service = new();

        ContainerVolumeDiscoveryResult result = service.Discover(sourcesRootPath, overrideRootPath);

        Assert.Equal([sourceVolumePath], result.SourceVolumePaths);
        Assert.Equal([overrideVolumePath], result.OverrideVolumePaths);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// Confirms missing source and override roots are reported as warnings instead of exceptions.
    /// </summary>
    [Fact]
    public void Discover_Edge_ShouldAddWarnings_WhenBothRootsAreMissing()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string missingSourcesRootPath = Path.Combine(temporaryDirectory.Path, "sources-missing");
        string missingOverrideRootPath = Path.Combine(temporaryDirectory.Path, "override-missing");

        ContainerVolumeDiscoveryService service = new();

        ContainerVolumeDiscoveryResult result = service.Discover(missingSourcesRootPath, missingOverrideRootPath);

        Assert.Empty(result.SourceVolumePaths);
        Assert.Empty(result.OverrideVolumePaths);
        Assert.Equal(2, result.Warnings.Count);
        Assert.All(result.Warnings, warning => Assert.Equal("VOL-DISC-001", warning.Code));
    }

    /// <summary>
    /// Ensures discovery rejects invalid source root arguments before any filesystem work starts.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Discover_Exception_ShouldThrow_WhenSourcesRootPathInvalid(string? invalidSourcesRootPath)
    {
        ContainerVolumeDiscoveryService service = new();

        Assert.ThrowsAny<ArgumentException>(() => service.Discover(invalidSourcesRootPath!, "/ssm/override"));
    }

    /// <summary>
    /// Ensures discovery rejects invalid override root arguments before any filesystem work starts.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Discover_Exception_ShouldThrow_WhenOverrideRootPathInvalid(string? invalidOverrideRootPath)
    {
        ContainerVolumeDiscoveryService service = new();

        Assert.ThrowsAny<ArgumentException>(() => service.Discover("/ssm/sources", invalidOverrideRootPath!));
    }

    /// <summary>
    /// Verifies invalid path formats are surfaced as argument failures during path normalization.
    /// </summary>
    [Fact]
    public void Discover_Exception_ShouldThrow_WhenPathFormatIsInvalid()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string validOverrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
        string invalidSourcesRootPath = string.Concat("bad", '\0', "path");
        ContainerVolumeDiscoveryService service = new();

        Assert.ThrowsAny<ArgumentException>(() => service.Discover(invalidSourcesRootPath, validOverrideRootPath));
    }

    /// <summary>
    /// Verifies filesystem enumeration exceptions are propagated to callers for higher-level handling.
    /// </summary>
    [Fact]
    public void Discover_Exception_ShouldPropagate_WhenDirectoryEnumerationFails()
    {
        FakeContainerVolumeFileSystem fileSystem = new()
        {
            GetFullPathHandler = path => path,
            DirectoryExistsHandler = _ => true,
            EnumerateDirectoriesHandler = _ => throw new IOException("simulated enumerate failure")
        };

        ContainerVolumeDiscoveryService service = new(fileSystem);

        IOException exception = Assert.Throws<IOException>(() => service.Discover("/sources", "/override"));

        Assert.Contains("simulated enumerate failure", exception.Message, StringComparison.Ordinal);
    }

    private sealed class FakeContainerVolumeFileSystem : IContainerVolumeFileSystem
    {
        public Func<string, bool> DirectoryExistsHandler
        {
            get;
            set;
        } = _ => false;

        public Func<string, IEnumerable<string>> EnumerateDirectoriesHandler
        {
            get;
            set;
        } = _ => [];

        public Func<string, string> GetFullPathHandler
        {
            get;
            set;
        } = path => path;

        public bool DirectoryExists(string path)
        {
            return DirectoryExistsHandler(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path)
        {
            return EnumerateDirectoriesHandler(path);
        }

        public string GetFullPath(string path)
        {
            return GetFullPathHandler(path);
        }
    }
}
