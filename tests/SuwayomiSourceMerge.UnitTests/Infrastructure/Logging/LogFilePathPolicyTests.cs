namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class LogFilePathPolicyTests
{
    [Theory]
    [InlineData("daemon.log")]
    [InlineData("daemon_1.log")]
    [InlineData("daemon-1.LOG")]
    [InlineData("daemon.v1.log")]
    public void TryValidateFileName_ShouldReturnTrue_ForValidFileName(string fileName)
    {
        bool isValid = LogFilePathPolicy.TryValidateFileName(fileName, out string normalized);

        Assert.True(isValid);
        Assert.Equal(fileName, normalized);
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

    [Theory]
    [InlineData("daemon.")]
    [InlineData("daemon ")]
    public void TryValidateFileName_ShouldReturnFalse_ForTrailingDotOrSpace(string fileName)
    {
        Assert.False(LogFilePathPolicy.TryValidateFileName(fileName, out _));
    }

    [Theory]
    [InlineData("da:mon.log")]
    [InlineData("da*mon.log")]
    [InlineData("da?mon.log")]
    [InlineData("da\"mon.log")]
    [InlineData("da<mon.log")]
    [InlineData("da>mon.log")]
    [InlineData("da|mon.log")]
    public void TryValidateFileName_ShouldReturnFalse_ForInvalidCharacters(string fileName)
    {
        Assert.False(LogFilePathPolicy.TryValidateFileName(fileName, out _));
    }

    [Fact]
    public void TryValidateFileName_ShouldReturnFalse_ForControlCharacters()
    {
        Assert.False(LogFilePathPolicy.TryValidateFileName("daemon\u0001.log", out _));
    }

    [Fact]
    public void TryValidateFileName_ShouldReturnFalse_ForReservedWindowsDeviceNames_OnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.False(LogFilePathPolicy.TryValidateFileName("CON", out _));
        Assert.False(LogFilePathPolicy.TryValidateFileName("prn.txt", out _));
        Assert.False(LogFilePathPolicy.TryValidateFileName("LPT1.log", out _));
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
    public void ResolvePathUnderRootOrThrow_ShouldThrow_WhenFileNameContainsInvalidCharacters()
    {
        using TemporaryDirectory temporaryDirectory = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LogFilePathPolicy.ResolvePathUnderRootOrThrow(temporaryDirectory.Path, "da:mon.log"));
        Assert.Equal(LogFilePathPolicy.InvalidFileNameMessage, exception.Message);
    }

    [Fact]
    public void ResolvePathUnderRootOrThrow_ShouldThrow_WhenFileNameEndsWithDotOrSpace()
    {
        using TemporaryDirectory temporaryDirectory = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LogFilePathPolicy.ResolvePathUnderRootOrThrow(temporaryDirectory.Path, "daemon."));
        Assert.Equal(LogFilePathPolicy.InvalidFileNameMessage, exception.Message);
    }

    [Fact]
    public void ResolvePathUnderRootOrThrow_ShouldThrow_WhenRootPathMissing()
    {
        Assert.Throws<ArgumentException>(() => LogFilePathPolicy.ResolvePathUnderRootOrThrow(" ", "daemon.log"));
    }
}
