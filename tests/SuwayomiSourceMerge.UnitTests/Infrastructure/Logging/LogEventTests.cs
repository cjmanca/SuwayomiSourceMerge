namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Infrastructure.Logging;

public sealed class LogEventTests
{
    [Fact]
    public void Constructor_ShouldAssignAllProperties()
    {
        DateTimeOffset timestamp = new(2026, 2, 9, 10, 0, 0, TimeSpan.Zero);
        Dictionary<string, string> context = new(StringComparer.Ordinal)
        {
            ["key"] = "value"
        };

        LogEvent logEvent = new(
            timestamp,
            LogLevel.Debug,
            "event.id",
            "hello",
            context);

        Assert.Equal(timestamp, logEvent.Timestamp);
        Assert.Equal(LogLevel.Debug, logEvent.Level);
        Assert.Equal("event.id", logEvent.EventId);
        Assert.Equal("hello", logEvent.Message);
        Assert.Same(context, logEvent.Context);
    }

    [Fact]
    public void Constructor_ShouldAllowNullContext()
    {
        LogEvent logEvent = new(
            DateTimeOffset.UtcNow,
            LogLevel.Trace,
            "event.id",
            "message",
            context: null);

        Assert.Null(logEvent.Context);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_ShouldThrow_WhenEventIdInvalid(string eventId)
    {
        Assert.Throws<ArgumentException>(
            () => new LogEvent(
                DateTimeOffset.UtcNow,
                LogLevel.Warning,
                eventId,
                "message",
                context: null));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_ShouldThrow_WhenMessageInvalid(string message)
    {
        Assert.Throws<ArgumentException>(
            () => new LogEvent(
                DateTimeOffset.UtcNow,
                LogLevel.Warning,
                "event.id",
                message,
                context: null));
    }
}
