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

        LogLevel minimumLevel = LogLevelParser.ParseOrThrow(
            logging.Level,
            "Settings logging.level is required for logger creation.");
        long maxFileSizeBytes = checked(logging.MaxFileSizeMb.Value * 1024L * 1024L);
        string logFilePath = LogFilePathPolicy.ResolvePathUnderRootOrThrow(paths.LogRootPath, logging.FileName);

        ILogSink sink = new RollingFileSink(logFilePath, maxFileSizeBytes, logging.RetainedFileCount.Value);
        StructuredTextLogFormatter formatter = new();
        return new RollingFileLogger(minimumLevel, sink, formatter, fallbackErrorWriter);
    }
}
