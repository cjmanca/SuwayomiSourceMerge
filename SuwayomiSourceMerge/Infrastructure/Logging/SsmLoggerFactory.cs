using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Default logger factory that maps settings to rolling-file logger infrastructure.
/// </summary>
internal sealed class SsmLoggerFactory : ISsmLoggerFactory
{
	/// <summary>
	/// Creates a configured logger from settings, enforcing required logging invariants.
	/// </summary>
	/// <param name="settings">Settings document containing path and logging sections.</param>
	/// <param name="fallbackErrorWriter">Callback used to report sink failures outside the normal log file path.</param>
	/// <returns>A logger configured with level filtering, formatting, and rolling file retention.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="settings"/> or <paramref name="fallbackErrorWriter"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when required logging settings are missing or invalid.
	/// </exception>
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
