using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Hosting;

/// <summary>
/// Coordinates runtime startup, configuration bootstrap, logger creation, and shutdown diagnostics.
/// </summary>
internal sealed class ApplicationHost
{
	/// <summary>Event id emitted when bootstrap completes successfully.</summary>
	private const string BOOTSTRAP_COMPLETED_EVENT = "bootstrap.completed";

	/// <summary>Event id emitted for each bootstrap warning.</summary>
	private const string BOOTSTRAP_WARNING_EVENT = "bootstrap.warning";

	/// <summary>Event id emitted when host shutdown completes.</summary>
	private const string HOST_SHUTDOWN_EVENT = "host.shutdown";

	/// <summary>Event id emitted when host runtime exits with failure.</summary>
	private const string HOST_SHUTDOWN_FAILED_EVENT = "host.shutdown_failed";

	/// <summary>Event id emitted when host startup completes.</summary>
	private const string HOST_STARTUP_EVENT = "host.startup";

	/// <summary>Event id emitted for unhandled host exceptions.</summary>
	private const string HOST_UNHANDLED_EXCEPTION_EVENT = "host.unhandled_exception";

	/// <summary>
	/// Service responsible for configuration bootstrap and validation.
	/// </summary>
	private readonly IConfigurationBootstrapService _bootstrapService;

	/// <summary>
	/// Factory used to create runtime logger instances from bootstrapped settings.
	/// </summary>
	private readonly ISsmLoggerFactory _loggerFactory;

	/// <summary>
	/// Runtime supervision runner used after bootstrap and logger creation.
	/// </summary>
	private readonly IRuntimeSupervisorRunner _runtimeSupervisorRunner;

	/// <summary>
	/// Creates an application host with explicit bootstrap and logging dependencies.
	/// </summary>
	/// <param name="bootstrapService">Configuration bootstrap service.</param>
	/// <param name="loggerFactory">Logger factory for runtime diagnostics.</param>
	/// <param name="runtimeSupervisorRunner">Runtime supervisor runner.</param>
	public ApplicationHost(
		IConfigurationBootstrapService bootstrapService,
		ISsmLoggerFactory loggerFactory,
		IRuntimeSupervisorRunner runtimeSupervisorRunner)
	{
		_bootstrapService = bootstrapService ?? throw new ArgumentNullException(nameof(bootstrapService));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_runtimeSupervisorRunner = runtimeSupervisorRunner ?? throw new ArgumentNullException(nameof(runtimeSupervisorRunner));
	}

	/// <summary>
	/// Creates the default production host composition.
	/// </summary>
	/// <returns>A host wired with schema parsing, validation pipeline, bootstrap service, and rolling-file logger factory.</returns>
	public static ApplicationHost CreateDefault()
	{
		return new ApplicationHost(
			new ConfigurationBootstrapService(
				new ConfigurationSchemaService(
					new ConfigurationValidationPipeline(
						new YamlDocumentParser()))),
			new SsmLoggerFactory(),
			new DefaultRuntimeSupervisorRunner());
	}

	/// <summary>
	/// Runs the host lifecycle from bootstrap through shutdown.
	/// </summary>
	/// <param name="configRootPath">Root directory that contains configuration files.</param>
	/// <param name="standardError">Writer used for bootstrap and fallback diagnostics.</param>
	/// <returns>0 on successful startup/shutdown path; 1 when fatal bootstrap/runtime errors occur.</returns>
	public int Run(string configRootPath, TextWriter standardError)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(configRootPath);
		ArgumentNullException.ThrowIfNull(standardError);

		ISsmLogger? logger = null;

		void HandleUnhandledException(object? _, UnhandledExceptionEventArgs eventArgs)
		{
			if (eventArgs.ExceptionObject is Exception exception)
			{
				TryLogUnhandledException(logger, standardError, exception);
				return;
			}

			WriteToStandardError(standardError, "Unhandled host exception: unknown exception object.");
		}

		AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;

		try
		{
			ConfigurationBootstrapResult bootstrapResult = _bootstrapService.Bootstrap(configRootPath);

			logger = _loggerFactory.Create(
				bootstrapResult.Documents.Settings,
				message => WriteToStandardError(standardError, message));

			logger.Debug(
				HOST_STARTUP_EVENT,
				"Host startup completed.",
				BuildContext(
					("config_root_path", configRootPath)));

			logger.Debug(
				BOOTSTRAP_COMPLETED_EVENT,
				"Configuration bootstrap completed.",
				BuildContext(
					("files", bootstrapResult.Files.Count.ToString()),
					("warnings", bootstrapResult.Warnings.Count.ToString())));

			foreach (ConfigurationBootstrapWarning warning in bootstrapResult.Warnings)
			{
				logger.Warning(
					BOOTSTRAP_WARNING_EVENT,
					warning.Message,
					BuildContext(
						("code", warning.Code),
						("file", warning.File),
						("line", warning.Line.ToString())));
			}

			int runtimeExitCode = _runtimeSupervisorRunner.Run(bootstrapResult.Documents, logger);
			if (runtimeExitCode != 0)
			{
				logger.Error(
					HOST_SHUTDOWN_FAILED_EVENT,
					"Host runtime exited with failure.",
					BuildContext(("runtime_exit_code", runtimeExitCode.ToString())));
				return 1;
			}

			logger.Debug(HOST_SHUTDOWN_EVENT, "Host shutdown completed.");
			return 0;
		}
		catch (ConfigurationBootstrapException exception)
		{
			WriteToStandardError(standardError, FormatBootstrapException(exception));
			return 1;
		}
		catch (Exception exception)
		{
			TryLogUnhandledException(logger, standardError, exception);
			return 1;
		}
		finally
		{
			AppDomain.CurrentDomain.UnhandledException -= HandleUnhandledException;
		}
	}

	/// <summary>
	/// Builds a context dictionary from key/value tuples, omitting blank keys.
	/// </summary>
	/// <param name="values">Context tuples to normalize into a dictionary.</param>
	/// <returns>Dictionary keyed with ordinal comparison semantics.</returns>
	private static Dictionary<string, string> BuildContext(params (string Key, string Value)[] values)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		foreach ((string key, string value) in values)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			context[key] = value;
		}

		return context;
	}

	/// <summary>
	/// Formats a bootstrap exception and its validation errors for stderr output.
	/// </summary>
	/// <param name="exception">Bootstrap exception containing validation failures.</param>
	/// <returns>Multi-line diagnostic text suitable for startup failure output.</returns>
	private static string FormatBootstrapException(ConfigurationBootstrapException exception)
	{
		List<string> entries = ["Configuration bootstrap failed."];
		entries.AddRange(
			exception.ValidationErrors.Select(
				error => $"{error.File}:{error.Path}:{error.Code} {error.Message}"));

		return string.Join(Environment.NewLine, entries);
	}

	/// <summary>
	/// Best-effort unhandled exception logging that avoids throwing from exception handlers.
	/// </summary>
	/// <param name="logger">Logger instance when available.</param>
	/// <param name="standardError">Fallback stderr writer.</param>
	/// <param name="exception">Unhandled exception to report.</param>
	private static void TryLogUnhandledException(ISsmLogger? logger, TextWriter standardError, Exception exception)
	{
		if (logger is not null)
		{
			try
			{
				logger.Error(
					HOST_UNHANDLED_EXCEPTION_EVENT,
					"Unhandled host exception.",
					BuildContext(
						("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
						("message", exception.Message)));
			}
			catch
			{
				// Error reporting must never throw from exception handling paths.
			}
		}

		WriteToStandardError(
			standardError,
			$"Unhandled host exception: {exception.GetType().Name}: {exception.Message}");
	}

	/// <summary>
	/// Writes a diagnostic message to stderr in a best-effort manner.
	/// </summary>
	/// <param name="standardError">Target stderr writer.</param>
	/// <param name="message">Message text to write.</param>
	private static void WriteToStandardError(TextWriter standardError, string message)
	{
		try
		{
			standardError.WriteLine(message);
			standardError.Flush();
		}
		catch
		{
			// Best-effort stderr output.
		}
	}
}
