using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Loading;
using SuwayomiSourceMerge.Infrastructure.Logging;

namespace SuwayomiSourceMerge.Application.Hosting;

internal sealed class ApplicationHost
{
    private const string BOOTSTRAP_COMPLETED_EVENT = "bootstrap.completed";
    private const string BOOTSTRAP_WARNING_EVENT = "bootstrap.warning";
    private const string HOST_SHUTDOWN_EVENT = "host.shutdown";
    private const string HOST_STARTUP_EVENT = "host.startup";
    private const string HOST_UNHANDLED_EXCEPTION_EVENT = "host.unhandled_exception";

    private readonly IConfigurationBootstrapService _bootstrapService;
    private readonly ISsmLoggerFactory _loggerFactory;

    public ApplicationHost(
        IConfigurationBootstrapService bootstrapService,
        ISsmLoggerFactory loggerFactory)
    {
        _bootstrapService = bootstrapService ?? throw new ArgumentNullException(nameof(bootstrapService));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public static ApplicationHost CreateDefault()
    {
        return new ApplicationHost(
            new ConfigurationBootstrapService(
                new ConfigurationSchemaService(
                    new ConfigurationValidationPipeline(
                        new YamlDocumentParser()))),
            new SsmLoggerFactory());
    }

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

    private static string FormatBootstrapException(ConfigurationBootstrapException exception)
    {
        List<string> entries = ["Configuration bootstrap failed."];
        entries.AddRange(
            exception.ValidationErrors.Select(
                error => $"{error.File}:{error.Path}:{error.Code} {error.Message}"));

        return string.Join(Environment.NewLine, entries);
    }

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
