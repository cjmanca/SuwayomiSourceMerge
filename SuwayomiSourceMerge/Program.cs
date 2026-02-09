using SuwayomiSourceMerge.Application.Hosting;

return ProgramEntryPoint.Run(Console.Error);

internal static class ProgramEntryPoint
{
    private const string DEFAULT_CONFIG_ROOT_PATH = "/ssm/config";

    public static int Run(TextWriter standardError)
    {
        return Run(
            standardError,
            (configRootPath, errorWriter) =>
            {
                ApplicationHost host = ApplicationHost.CreateDefault();
                return host.Run(configRootPath, errorWriter);
            });
    }

    internal static int Run(
        TextWriter standardError,
        Func<string, TextWriter, int> hostRunner)
    {
        ArgumentNullException.ThrowIfNull(standardError);
        ArgumentNullException.ThrowIfNull(hostRunner);

        return hostRunner(DEFAULT_CONFIG_ROOT_PATH, standardError);
    }
}
