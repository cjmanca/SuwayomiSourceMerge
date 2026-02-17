using SuwayomiSourceMerge.Application.Hosting;

return ProgramEntryPoint.Run(Console.Error);

/// <summary>
/// Provides testable entrypoint methods for process startup.
/// </summary>
/// <remarks>
/// The top-level statement forwards execution into this type so tests can invoke startup logic
/// without launching a separate process.
/// </remarks>
internal static class ProgramEntryPoint
{
	/// <summary>
	/// Default configuration root path used by containerized deployments.
	/// </summary>
	private const string DefaultConfigRootPath = "/ssm/config";

	/// <summary>
	/// Runs the application using the default host composition.
	/// </summary>
	/// <param name="standardError">Writer used for fallback diagnostic output.</param>
	/// <returns>Process exit code (0 for success, non-zero for failure).</returns>
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

	/// <summary>
	/// Runs the application using an injected host runner, primarily for tests.
	/// </summary>
	/// <param name="standardError">Writer used for fallback diagnostic output.</param>
	/// <param name="hostRunner">Delegate that executes the host given config root and stderr writer.</param>
	/// <returns>Process exit code returned by <paramref name="hostRunner"/>.</returns>
	internal static int Run(
		TextWriter standardError,
		Func<string, TextWriter, int> hostRunner)
	{
		ArgumentNullException.ThrowIfNull(standardError);
		ArgumentNullException.ThrowIfNull(hostRunner);

		return hostRunner(DefaultConfigRootPath, standardError);
	}
}
