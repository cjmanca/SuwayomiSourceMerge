using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal sealed class SsmLoggerFactory : ISsmLoggerFactory
{
    public ISsmLogger Create(SettingsDocument settings, Action<string> fallbackErrorWriter)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fallbackErrorWriter);

        SettingsPathsSection paths = settings.Paths
            ?? throw new InvalidOperationException("Settings paths section is required for logger creation.");
        SettingsLoggingSection logging = settings.Logging
            ?? throw new InvalidOperationException("Settings logging section is required for logger creation.");

        if (string.IsNullOrWhiteSpace(paths.LogRootPath))
        {
            throw new InvalidOperationException("Settings paths.log_root_path is required for logger creation.");
        }

        if (string.IsNullOrWhiteSpace(logging.FileName))
        {
            throw new InvalidOperationException("Settings logging.file_name is required for logger creation.");
        }

        if (!logging.MaxFileSizeMb.HasValue || logging.MaxFileSizeMb.Value <= 0)
        {
            throw new InvalidOperationException("Settings logging.max_file_size_mb must be greater than 0.");
        }

        if (!logging.RetainedFileCount.HasValue || logging.RetainedFileCount.Value <= 0)
        {
            throw new InvalidOperationException("Settings logging.retained_file_count must be greater than 0.");
        }

        LogLevel minimumLevel = ParseLogLevel(logging.Level);
        long maxFileSizeBytes = checked(logging.MaxFileSizeMb.Value * 1024L * 1024L);
        string logFilePath = Path.Combine(paths.LogRootPath, logging.FileName);

        ILogSink sink = new RollingFileSink(logFilePath, maxFileSizeBytes, logging.RetainedFileCount.Value);
        StructuredTextLogFormatter formatter = new();
        return new RollingFileLogger(minimumLevel, sink, formatter, fallbackErrorWriter);
    }

    private static LogLevel ParseLogLevel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException("Settings logging.level is required for logger creation.");
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "none" => LogLevel.None,
            _ => throw new InvalidOperationException($"Unsupported logging level '{value}'.")
        };
    }
}
