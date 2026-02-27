namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Infrastructure.Logging;

public sealed class RollingFileLoggerTests
{
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 0, false)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    [InlineData(3, 4, true)]
    [InlineData(4, 3, false)]
    [InlineData(5, 4, false)]
    public void Log_ShouldRespectConfiguredMinimumLevel(int minimumLevelValue, int eventLevelValue, bool expectedWrite)
    {
        LogLevel minimumLevel = (LogLevel)minimumLevelValue;
        LogLevel eventLevel = (LogLevel)eventLevelValue;
        RecordingSink sink = new();
        List<string> fallbackMessages = [];
        RollingFileLogger logger = new(
            minimumLevel,
            sink,
            new StructuredTextLogFormatter(),
            fallbackMessages.Add);

        logger.Log(eventLevel, "event.test", "message");

        Assert.Equal(expectedWrite ? 1 : 0, sink.Lines.Count);
        Assert.Empty(fallbackMessages);
    }

    [Fact]
    public void Log_ShouldWriteFallbackError_WhenSinkThrows()
    {
        ThrowingSink sink = new();
        List<string> fallbackMessages = [];
        RollingFileLogger logger = new(
            LogLevel.Trace,
            sink,
            new StructuredTextLogFormatter(),
            fallbackMessages.Add);

        logger.Error("event.error", "message");

        string fallback = Assert.Single(fallbackMessages);
        Assert.Contains("logging_failure", fallback);
        Assert.Contains("event=\"event.error\"", fallback);
    }

    [Fact]
    public void Log_ShouldEscapeFallbackFields_WhenSinkThrowsWithMultilineMessage()
    {
        ThrowingSinkWithCustomMessage sink = new("bad \"state\"\nnext line");
        List<string> fallbackMessages = [];
        RollingFileLogger logger = new(
            LogLevel.Trace,
            sink,
            new StructuredTextLogFormatter(),
            fallbackMessages.Add);

        logger.Error("event.\"quoted\"\nname", "message");

        string fallback = Assert.Single(fallbackMessages);
        Assert.DoesNotContain("\n", fallback);
        Assert.Contains("event=\"event.\\\"quoted\\\"\\nname\"", fallback);
        Assert.Contains("error_message=\"bad \\\"state\\\"\\nnext line\"", fallback);
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenLevelIsNone()
    {
        RollingFileLogger logger = new(
            LogLevel.None,
            new RecordingSink(),
            new StructuredTextLogFormatter(),
            _ => { });

        bool enabled = logger.IsEnabled(LogLevel.Error);

        Assert.False(enabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void IsEnabled_ShouldAlwaysReturnFalse_ForNoneEventLevel(int minimumLevelValue)
    {
        LogLevel minimumLevel = (LogLevel)minimumLevelValue;

        RollingFileLogger logger = new(
            minimumLevel,
            new RecordingSink(),
            new StructuredTextLogFormatter(),
            _ => { });

        bool enabled = logger.IsEnabled(LogLevel.None);

        Assert.False(enabled);
    }

    [Fact]
    public void Log_ShouldNotEmit_WhenEventLevelIsNone()
    {
        RecordingSink sink = new();
        List<string> fallbackMessages = [];
        RollingFileLogger logger = new(
            LogLevel.Trace,
            sink,
            new StructuredTextLogFormatter(),
            fallbackMessages.Add);

        logger.Log(LogLevel.None, "event.none", "none");

        Assert.Empty(sink.Lines);
        Assert.Empty(fallbackMessages);
    }

    [Fact]
    public void WrapperMethods_ShouldEmitExpectedLevelTokens()
    {
        RecordingSink sink = new();
        RollingFileLogger logger = new(
            LogLevel.Trace,
            sink,
            new StructuredTextLogFormatter(),
            _ => { });

        logger.Trace("event.trace", "trace");
        logger.Debug("event.debug", "debug");
        logger.Normal("event.normal", "normal");
        logger.Warning("event.warning", "warning");
        logger.Error("event.error", "error");

        Assert.Equal(5, sink.Lines.Count);
        Assert.Contains("level=trace", sink.Lines[0]);
        Assert.Contains("level=debug", sink.Lines[1]);
        Assert.Contains("level=normal", sink.Lines[2]);
        Assert.Contains("level=warning", sink.Lines[3]);
        Assert.Contains("level=error", sink.Lines[4]);
    }

    [Fact]
    public void Log_ShouldThrow_WhenEventIdIsInvalid()
    {
        RollingFileLogger logger = new(
            LogLevel.Trace,
            new RecordingSink(),
            new StructuredTextLogFormatter(),
            _ => { });

        Assert.Throws<ArgumentException>(() => logger.Log(LogLevel.Error, " ", "message"));
    }

    [Fact]
    public void Log_ShouldThrow_WhenMessageInvalid()
    {
        RollingFileLogger logger = new(
            LogLevel.Trace,
            new RecordingSink(),
            new StructuredTextLogFormatter(),
            _ => { });

        Assert.Throws<ArgumentException>(() => logger.Log(LogLevel.Error, "event", " "));
    }

    [Fact]
    public void Log_ShouldSwallowFallbackWriterFailures()
    {
        ThrowingSink sink = new();
        RollingFileLogger logger = new(
            LogLevel.Trace,
            sink,
            new StructuredTextLogFormatter(),
            _ => throw new InvalidOperationException("fallback failed"));

        logger.Error("event.error", "message");
    }

    [Fact]
    public void Log_Failure_ShouldRethrow_WhenSinkThrowsFatalException()
    {
        FatalThrowingSink sink = new();
        List<string> fallbackMessages = [];
        RollingFileLogger logger = new(
            LogLevel.Trace,
            sink,
            new StructuredTextLogFormatter(),
            fallbackMessages.Add);

        Assert.Throws<OutOfMemoryException>(() => logger.Error("event.error", "message"));
        Assert.Empty(fallbackMessages);
    }

    [Fact]
    public void Log_Failure_ShouldRethrow_WhenFallbackWriterThrowsFatalException()
    {
        ThrowingSink sink = new();
        RollingFileLogger logger = new(
            LogLevel.Trace,
            sink,
            new StructuredTextLogFormatter(),
            _ => throw new OutOfMemoryException("fatal-fallback-writer"));

        Assert.Throws<OutOfMemoryException>(() => logger.Error("event.error", "message"));
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenDependenciesAreNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new RollingFileLogger(LogLevel.Trace, null!, new StructuredTextLogFormatter(), _ => { }));
        Assert.Throws<ArgumentNullException>(
            () => new RollingFileLogger(LogLevel.Trace, new RecordingSink(), null!, _ => { }));
        Assert.Throws<ArgumentNullException>(
            () => new RollingFileLogger(LogLevel.Trace, new RecordingSink(), new StructuredTextLogFormatter(), null!));
    }

    private sealed class RecordingSink : ILogSink
    {
        public List<string> Lines { get; } = [];

        public void WriteLine(string line)
        {
            Lines.Add(line);
        }
    }

    private sealed class ThrowingSink : ILogSink
    {
        public void WriteLine(string line)
        {
            throw new IOException("simulated sink failure");
        }
    }

    private sealed class ThrowingSinkWithCustomMessage : ILogSink
    {
        private readonly string _message;

        public ThrowingSinkWithCustomMessage(string message)
        {
            _message = message;
        }

        public void WriteLine(string line)
        {
            throw new IOException(_message);
        }
    }

    private sealed class FatalThrowingSink : ILogSink
    {
        public void WriteLine(string line)
        {
            throw new OutOfMemoryException("fatal-sink-failure");
        }
    }
}
