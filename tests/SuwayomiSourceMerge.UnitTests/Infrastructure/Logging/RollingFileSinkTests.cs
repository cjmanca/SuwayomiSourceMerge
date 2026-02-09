namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed class RollingFileSinkTests
{
    [Fact]
    public void WriteLine_ShouldWriteToActiveFile()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        RollingFileSink sink = new(logPath, 1024, 3);

        sink.WriteLine("line-one");

        Assert.True(File.Exists(logPath));
        string content = File.ReadAllText(logPath);
        Assert.Equal("line-one\n", content);
    }

    [Fact]
    public void WriteLine_ShouldRotateAndRetainConfiguredFileCount()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        RollingFileSink sink = new(logPath, 20, 2);

        sink.WriteLine("1234567890");
        sink.WriteLine("abcdefghij");
        sink.WriteLine("klmnopqrst");
        sink.WriteLine("uvwxyz1234");

        Assert.True(File.Exists(logPath));
        Assert.True(File.Exists(logPath + ".1"));
        Assert.True(File.Exists(logPath + ".2"));
        Assert.False(File.Exists(logPath + ".3"));
    }

    [Fact]
    public void WriteLine_ShouldNotRotate_WhenWriteExactlyMatchesMaxSize()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        RollingFileSink sink = new(logPath, 11, 2);

        sink.WriteLine("1234567890");

        Assert.True(File.Exists(logPath));
        Assert.False(File.Exists(logPath + ".1"));
        Assert.Equal("1234567890\n", File.ReadAllText(logPath));
    }

    [Fact]
    public void WriteLine_ShouldThrow_WhenLineIsNull()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        RollingFileSink sink = new(logPath, 1024, 2);

        Assert.Throws<ArgumentNullException>(() => sink.WriteLine(null!));
    }

    [Fact]
    public void WriteLine_ShouldThrowIOException_WhenLogPathIsDirectory()
    {
        using TemporaryDirectory temporaryDirectory = new();
        string logPath = Path.Combine(temporaryDirectory.Path, "daemon.log");
        Directory.CreateDirectory(logPath);
        RollingFileSink sink = new(logPath, 1024, 2);

        Exception exception = Assert.ThrowsAny<Exception>(() => sink.WriteLine("line"));
        Assert.True(exception is IOException || exception is UnauthorizedAccessException);
    }

    [Theory]
    [InlineData("", 1024, 1)]
    [InlineData("daemon.log", 0, 1)]
    [InlineData("daemon.log", 1024, 0)]
    public void Constructor_ShouldThrow_ForInvalidArguments(string path, long maxBytes, int retainedCount)
    {
        string logPath = string.IsNullOrWhiteSpace(path)
            ? path
            : Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-{path}");

        Assert.ThrowsAny<ArgumentException>(() => new RollingFileSink(logPath, maxBytes, retainedCount));
    }
}
