namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class LogFilePathPolicyTests
{
    [Fact]
    public void TryValidateFileName_ShouldReturnTrue_ForSimpleFileName()
    {
        bool isValid = LogFilePathPolicy.TryValidateFileName("daemon.log", out string normalized);

        Assert.True(isValid);
        Assert.Equal("daemon.log", normalized);
    }

    [Fact]
    public void TryValidateFileName_ShouldReturnFalse_ForTraversalOrDirectorySegments()
    {
        Assert.False(LogFilePathPolicy.TryValidateFileName("../daemon.log", out _));
        Assert.False(LogFilePathPolicy.TryValidateFileName("logs/daemon.log", out _));
        Assert.False(LogFilePathPolicy.TryValidateFileName(@"logs\daemon.log", out _));
    }

    [Fact]
    public void TryValidateFileName_ShouldReturnFalse_ForNullOrWhitespace()
    {
        Assert.False(LogFilePathPolicy.TryValidateFileName(null, out _));
        Assert.False(LogFilePathPolicy.TryValidateFileName(" ", out _));
    }

    [Fact]
    public void ResolvePathUnderRootOrThrow_ShouldReturnPathUnderRoot_ForValidInput()
    {
        using TemporaryDirectory temporaryDirectory = new();

        string resolvedPath = LogFilePathPolicy.ResolvePathUnderRootOrThrow(temporaryDirectory.Path, "daemon.log");

        Assert.Equal(Path.Combine(temporaryDirectory.Path, "daemon.log"), resolvedPath);
    }

    [Fact]
    public void ResolvePathUnderRootOrThrow_ShouldThrow_WhenFileNameInvalid()
    {
        using TemporaryDirectory temporaryDirectory = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LogFilePathPolicy.ResolvePathUnderRootOrThrow(temporaryDirectory.Path, "../daemon.log"));
        Assert.Equal(LogFilePathPolicy.InvalidFileNameMessage, exception.Message);
    }

    [Fact]
    public void ResolvePathUnderRootOrThrow_ShouldThrow_WhenRootPathMissing()
    {
        Assert.Throws<ArgumentException>(() => LogFilePathPolicy.ResolvePathUnderRootOrThrow(" ", "daemon.log"));
    }
}
