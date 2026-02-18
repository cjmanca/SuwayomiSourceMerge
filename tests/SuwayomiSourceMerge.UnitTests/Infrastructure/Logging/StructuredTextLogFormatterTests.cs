namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Infrastructure.Logging;

public sealed class StructuredTextLogFormatterTests
{
    [Fact]
    public void Format_ShouldIncludeCoreFieldsAndContext()
    {
        StructuredTextLogFormatter formatter = new();
        LogEvent logEvent = new(
            new DateTimeOffset(2026, 2, 9, 12, 30, 0, TimeSpan.Zero),
            LogLevel.Normal,
            "bootstrap.warning",
            "Warning during bootstrap.",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["code"] = "CFG-MIG-001",
                ["file"] = "manga_equivalents.txt"
            });

        string line = formatter.Format(logEvent);

        Assert.Contains("ts=2026-02-09T12:30:00.0000000+00:00", line);
        Assert.Contains("level=normal", line);
        Assert.Contains("event=\"bootstrap.warning\"", line);
        Assert.Contains("msg=\"Warning during bootstrap.\"", line);
        Assert.Contains("code=\"CFG-MIG-001\"", line);
        Assert.Contains("file=\"manga_equivalents.txt\"", line);
        Assert.True(line.IndexOf("code=\"CFG-MIG-001\"", StringComparison.Ordinal) < line.IndexOf("file=\"manga_equivalents.txt\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Format_ShouldEscapeMessageAndContextValues()
    {
        StructuredTextLogFormatter formatter = new();
        LogEvent logEvent = new(
            new DateTimeOffset(2026, 2, 9, 12, 30, 0, TimeSpan.Zero),
            LogLevel.Error,
            "host.unhandled_exception",
            "line1\n\"line2\"",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["exception type"] = "System.InvalidOperationException",
                ["message"] = "bad \"state\""
            });

        string line = formatter.Format(logEvent);

        Assert.Contains("msg=\"line1\\n\\\"line2\\\"\"", line);
        Assert.Contains("exception_type=\"System.InvalidOperationException\"", line);
        Assert.Contains("message=\"bad \\\"state\\\"\"", line);
    }

    [Fact]
    public void Format_ShouldThrow_WhenEventIsNull()
    {
        StructuredTextLogFormatter formatter = new();

        Assert.Throws<ArgumentNullException>(() => formatter.Format(null!));
    }

    [Fact]
    public void Format_ShouldHandleEmptyAndInvalidContextKeys()
    {
        StructuredTextLogFormatter formatter = new();
        LogEvent logEvent = new(
            new DateTimeOffset(2026, 2, 9, 12, 30, 0, TimeSpan.Zero),
            LogLevel.Trace,
            "event",
            "message",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [""] = "empty",
                ["bad key!"] = "normalized"
            });

        string line = formatter.Format(logEvent);

        Assert.Contains("context=\"empty\"", line);
        Assert.Contains("bad_key_=\"normalized\"", line);
    }

    [Fact]
    public void Format_ShouldThrow_WhenLogLevelIsUnsupported()
    {
        StructuredTextLogFormatter formatter = new();
        LogEvent logEvent = new(
            DateTimeOffset.UtcNow,
            (LogLevel)999,
            "event",
            "message",
            context: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => formatter.Format(logEvent));
    }
}
