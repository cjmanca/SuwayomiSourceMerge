namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Logging;

using SuwayomiSourceMerge.Infrastructure.Logging;

public sealed class LogLevelParserTests
{
    [Fact]
    public void TryParse_ShouldReturnTrue_ForSupportedLevel()
    {
        bool parsed = LogLevelParser.TryParse("warning", out LogLevel level);

        Assert.True(parsed);
        Assert.Equal(LogLevel.Warning, level);
    }

    [Fact]
    public void TryParse_ShouldNormalizeWhitespaceAndCase()
    {
        bool parsed = LogLevelParser.TryParse("  DeBuG ", out LogLevel level);

        Assert.True(parsed);
        Assert.Equal(LogLevel.Debug, level);
    }

    [Fact]
    public void TryParse_ShouldReturnFalse_ForUnsupportedLevel()
    {
        bool parsed = LogLevelParser.TryParse("information", out LogLevel level);

        Assert.False(parsed);
        Assert.Equal(default, level);
    }

    [Fact]
    public void TryParse_ShouldThrow_WhenInputMissing()
    {
        Assert.Throws<ArgumentException>(() => LogLevelParser.TryParse(" ", out _));
    }

    [Fact]
    public void ParseOrThrow_ShouldReturnLevel_ForSupportedLevel()
    {
        LogLevel level = LogLevelParser.ParseOrThrow("trace", "missing message");

        Assert.Equal(LogLevel.Trace, level);
    }

    [Fact]
    public void ParseOrThrow_ShouldNormalizeWhitespaceAndCase()
    {
        LogLevel level = LogLevelParser.ParseOrThrow("  ErRoR ", "missing message");

        Assert.Equal(LogLevel.Error, level);
    }

    [Fact]
    public void ParseOrThrow_ShouldThrow_WhenInputMissing()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LogLevelParser.ParseOrThrow(null, "Settings logging.level is required for logger creation."));

        Assert.Equal("Settings logging.level is required for logger creation.", exception.Message);
    }

    [Fact]
    public void ParseOrThrow_ShouldThrow_WhenUnsupportedLevel()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => LogLevelParser.ParseOrThrow("information", "Settings logging.level is required for logger creation."));

        Assert.Contains("Supported values are: trace, debug, warning, error, none.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParseOrThrow_ShouldThrow_WhenMissingValueMessageIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => LogLevelParser.ParseOrThrow("trace", " "));
    }
}
