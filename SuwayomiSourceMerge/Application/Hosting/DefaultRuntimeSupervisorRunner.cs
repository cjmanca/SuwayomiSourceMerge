using System.Globalization;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Supervision;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Volumes;
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
		if (documents.Settings.Scan?.MergeTriggerRequestTimeoutBufferSeconds is int timeoutBufferSeconds)
		{
			logger.Warning(
				"watcher.config.deprecated",
				"Setting 'scan.merge_trigger_request_timeout_buffer_seconds' is deprecated and ignored by the persistent inotify monitor implementation.",
				new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["setting"] = "scan.merge_trigger_request_timeout_buffer_seconds",
					["value"] = timeoutBufferSeconds.ToString(CultureInfo.InvariantCulture)
				});
		}

		IInotifyEventReader inotifyReader = new PersistentInotifywaitEventReader(triggerOptions.WatchStartupMode);
		IChapterRenameQueueProcessor renameQueueProcessor = new ChapterRenameQueueProcessor(
			renameOptions,
			new ShellParityChapterRenameSanitizer(),
			new InMemoryChapterRenameQueueStore(),
			new ChapterRenameFileSystem(),
			logger);

		MergeMountWorkflowOptions mergeOptions = MergeMountWorkflowOptions.FromSettings(documents.Settings);
		ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(documents.SceneTags.Tags ?? []);
		IMangaEquivalenceService mangaEquivalenceService = new MangaEquivalenceService(documents.MangaEquivalents, sceneTagMatcher);
		ISourcePriorityService sourcePriorityService = new SourcePriorityService(documents.SourcePriority);
		MergeMountWorkflow mergeMountWorkflow = new(
			mergeOptions,
			mangaEquivalenceService,
			sceneTagMatcher,
			new ContainerVolumeDiscoveryService(),
			new MergerfsBranchPlanningService(sourcePriorityService),
			new FindmntMountSnapshotService(),
			new MountReconciliationService(),
			new MergerfsMountCommandService(),
			new BranchLinkStagingService(),
			new OverrideDetailsService(),
			logger);

		IMergeScanRequestCoalescer mergeScanRequestCoalescer = new MergeScanRequestCoalescer(
			new ProductionMergeScanRequestHandler(mergeMountWorkflow, logger),
			triggerOptions.MergeMinSecondsBetweenScans,
			triggerOptions.MergeLockRetrySeconds);

		FilesystemEventTriggerPipeline triggerPipeline = new(
			triggerOptions,
			inotifyReader,
			renameQueueProcessor,
			mergeScanRequestCoalescer,
			logger);

		IDaemonWorker worker = new FilesystemEventDaemonWorker(triggerPipeline, mergeMountWorkflow, logger);
		IDaemonSupervisor supervisor = new DaemonSupervisor(
			worker,
			DaemonSupervisorOptions.FromSettings(documents.Settings),
			logger,
			new ConsoleSupervisorSignalRegistrar());

		return supervisor.RunAsync().GetAwaiter().GetResult();
	}
}
