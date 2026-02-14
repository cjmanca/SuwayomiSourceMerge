using SuwayomiSourceMerge.Application.Supervision;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Processes;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;

namespace SuwayomiSourceMerge.Application.Hosting;

/// <summary>
/// Default runtime supervisor composition used by production host startup.
/// </summary>
internal sealed class DefaultRuntimeSupervisorRunner : IRuntimeSupervisorRunner
{
	/// <inheritdoc />
	public int Run(ConfigurationDocumentSet documents, ISsmLogger logger)
	{
		ArgumentNullException.ThrowIfNull(documents);
		ArgumentNullException.ThrowIfNull(logger);

		FilesystemEventTriggerOptions triggerOptions = FilesystemEventTriggerOptions.FromSettings(documents.Settings);
		ChapterRenameOptions renameOptions = ChapterRenameOptions.FromSettings(documents.Settings);

		IInotifyEventReader inotifyReader = new InotifywaitEventReader(new ExternalCommandExecutor());
		IChapterRenameQueueProcessor renameQueueProcessor = new ChapterRenameQueueProcessor(
			renameOptions,
			new ShellParityChapterRenameSanitizer(),
			new InMemoryChapterRenameQueueStore(),
			new ChapterRenameFileSystem(),
			logger);

		IMergeScanRequestCoalescer mergeScanRequestCoalescer = new MergeScanRequestCoalescer(
			new NoOpMergeScanRequestHandler(logger),
			triggerOptions.MergeMinSecondsBetweenScans,
			triggerOptions.MergeLockRetrySeconds);

		FilesystemEventTriggerPipeline triggerPipeline = new(
			triggerOptions,
			inotifyReader,
			renameQueueProcessor,
			mergeScanRequestCoalescer,
			logger);

		IDaemonWorker worker = new FilesystemEventDaemonWorker(triggerPipeline, logger);
		IDaemonSupervisor supervisor = new DaemonSupervisor(
			worker,
			DaemonSupervisorOptions.FromSettings(documents.Settings),
			logger,
			new ConsoleSupervisorSignalRegistrar());

		return supervisor.RunAsync().GetAwaiter().GetResult();
	}
}
