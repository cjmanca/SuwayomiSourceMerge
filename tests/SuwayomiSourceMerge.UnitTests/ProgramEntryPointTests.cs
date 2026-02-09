namespace SuwayomiSourceMerge.UnitTests;

public sealed class ProgramEntryPointTests
{
    [Fact]
    public void Run_ShouldPassDefaultConfigRootToHostRunner()
    {
        using StringWriter standardError = new();
        string? capturedConfigRoot = null;
        TextWriter? capturedWriter = null;

        int exitCode = ProgramEntryPoint.Run(
            standardError,
            (configRootPath, writer) =>
            {
                capturedConfigRoot = configRootPath;
                capturedWriter = writer;
                return 7;
            });

        Assert.Equal(7, exitCode);
        Assert.Equal("/ssm/config", capturedConfigRoot);
        Assert.Same(standardError, capturedWriter);
    }

    [Fact]
    public void Run_ShouldAllowHostRunnerToWriteToErrorStream()
    {
        using StringWriter standardError = new();

        int exitCode = ProgramEntryPoint.Run(
            standardError,
            (_, writer) =>
            {
                writer.Write("runner-error");
                return 1;
            });

        Assert.Equal(1, exitCode);
        Assert.Equal("runner-error", standardError.ToString());
    }

    [Fact]
    public void Run_ShouldThrow_WhenStandardErrorIsNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => ProgramEntryPoint.Run(
                null!,
                (_, _) => 0));
    }

    [Fact]
    public void Run_ShouldThrow_WhenHostRunnerIsNull()
    {
        using StringWriter standardError = new();

        Assert.Throws<ArgumentNullException>(
            () => ProgramEntryPoint.Run(standardError, null!));
    }
}
