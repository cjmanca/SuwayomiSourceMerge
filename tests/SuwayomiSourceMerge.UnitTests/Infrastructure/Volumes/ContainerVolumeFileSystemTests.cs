namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Volumes;

using SuwayomiSourceMerge.Infrastructure.Volumes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and exception behavior for <see cref="ContainerVolumeFileSystem"/>.
/// </summary>
public sealed class ContainerVolumeFileSystemTests
{
    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.DirectoryExists"/> returns true for an existing directory.
    /// </summary>
    [Fact]
    public void DirectoryExists_Expected_ShouldReturnTrue_WhenDirectoryExists()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ContainerVolumeFileSystem fileSystem = new();

        bool exists = fileSystem.DirectoryExists(temporaryDirectory.Path);

        Assert.True(exists);
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.DirectoryExists"/> returns false for a non-existent directory.
    /// </summary>
    [Fact]
    public void DirectoryExists_Edge_ShouldReturnFalse_WhenDirectoryMissing()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ContainerVolumeFileSystem fileSystem = new();
        string missingPath = Path.Combine(temporaryDirectory.Path, "missing");

        bool exists = fileSystem.DirectoryExists(missingPath);

        Assert.False(exists);
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.DirectoryExists"/> rejects null or blank path arguments.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void DirectoryExists_Exception_ShouldThrow_WhenPathInvalid(string? invalidPath)
    {
        ContainerVolumeFileSystem fileSystem = new();

        Assert.ThrowsAny<ArgumentException>(() => fileSystem.DirectoryExists(invalidPath!));
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.EnumerateDirectories"/> returns direct-child directories only.
    /// </summary>
    [Fact]
    public void EnumerateDirectories_Expected_ShouldReturnDirectChildDirectories()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string volumeAPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "disk1")).FullName;
        string volumeBPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "disk2")).FullName;
        Directory.CreateDirectory(Path.Combine(volumeAPath, "nested"));
        File.WriteAllText(Path.Combine(temporaryDirectory.Path, "ignore.txt"), "ignore");
        ContainerVolumeFileSystem fileSystem = new();

        string[] discoveredPaths = fileSystem.EnumerateDirectories(temporaryDirectory.Path).ToArray();

        Assert.Equal(2, discoveredPaths.Length);
        Assert.Contains(volumeAPath, discoveredPaths);
        Assert.Contains(volumeBPath, discoveredPaths);
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.EnumerateDirectories"/> returns an empty sequence when no child directories exist.
    /// </summary>
    [Fact]
    public void EnumerateDirectories_Edge_ShouldReturnEmpty_WhenNoDirectoriesExist()
    {
        using TemporaryDirectory temporaryDirectory = new();
        File.WriteAllText(Path.Combine(temporaryDirectory.Path, "file.txt"), "ignore");
        ContainerVolumeFileSystem fileSystem = new();

        string[] discoveredPaths = fileSystem.EnumerateDirectories(temporaryDirectory.Path).ToArray();

        Assert.Empty(discoveredPaths);
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.EnumerateDirectories"/> rejects null or blank path arguments.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void EnumerateDirectories_Exception_ShouldThrow_WhenPathInvalid(string? invalidPath)
    {
        ContainerVolumeFileSystem fileSystem = new();

        Assert.ThrowsAny<ArgumentException>(() => fileSystem.EnumerateDirectories(invalidPath!).ToArray());
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.GetFullPath"/> resolves a truly relative path against the
    /// current working directory and restores the original directory after the assertion.
    /// </summary>
    [Fact]
    public void GetFullPath_Expected_ShouldResolveRelativePathAgainstCurrentDirectory()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ContainerVolumeFileSystem fileSystem = new();
        string originalCurrentDirectory = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = temporaryDirectory.Path;
            string relativePath = Path.Combine("manga", "..", "chapter");

            string fullPath = fileSystem.GetFullPath(relativePath);
            string expectedPath = Path.GetFullPath(Path.Combine(temporaryDirectory.Path, "chapter"));

            Assert.True(Path.IsPathRooted(fullPath));
            Assert.Equal(expectedPath, fullPath);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.GetFullPath"/> normalizes dot and parent segments
    /// for relative paths using the current working directory.
    /// </summary>
    [Fact]
    public void GetFullPath_Edge_ShouldNormalizeDotAndParentSegments_ForRelativeInput()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ContainerVolumeFileSystem fileSystem = new();
        string originalCurrentDirectory = Environment.CurrentDirectory;

        try
        {
            Environment.CurrentDirectory = temporaryDirectory.Path;
            string relativePath = Path.Combine(".", "disk1", "..", "disk2", ".");

            string fullPath = fileSystem.GetFullPath(relativePath);
            string expectedPath = Path.GetFullPath(Path.Combine(temporaryDirectory.Path, "disk2"));

            Assert.Equal(expectedPath, fullPath);
        }
        finally
        {
            Environment.CurrentDirectory = originalCurrentDirectory;
        }
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.GetFullPath"/> preserves already-absolute paths after normalization.
    /// </summary>
    [Fact]
    public void GetFullPath_Edge_ShouldReturnSamePath_WhenAlreadyAbsolute()
    {
        using TemporaryDirectory temporaryDirectory = new();
        ContainerVolumeFileSystem fileSystem = new();

        string fullPath = fileSystem.GetFullPath(temporaryDirectory.Path);

        Assert.Equal(Path.GetFullPath(temporaryDirectory.Path), fullPath);
    }

    /// <summary>
    /// Verifies <see cref="ContainerVolumeFileSystem.GetFullPath"/> rejects null or blank path arguments.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void GetFullPath_Exception_ShouldThrow_WhenPathInvalid(string? invalidPath)
    {
        ContainerVolumeFileSystem fileSystem = new();

        Assert.ThrowsAny<ArgumentException>(() => fileSystem.GetFullPath(invalidPath!));
    }
}
