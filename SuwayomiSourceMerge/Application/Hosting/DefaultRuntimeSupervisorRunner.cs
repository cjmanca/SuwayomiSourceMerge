using System.Globalization;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Supervision;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Configuration.Bootstrap;
using SuwayomiSourceMerge.Configuration.Loading;
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
	/// <summary>
	/// Event id emitted when configured scene tags are missing recommended defaults.
	/// </summary>
	private const string SceneTagsRecommendedMissingEvent = "config.scene_tags.recommended_missing";

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
		IReadOnlyList<string> missingRecommendedTags = SceneTagConfigurationAdvisor.GetMissingRecommendedTags(sceneTagMatcher);
		if (missingRecommendedTags.Count > 0)
		{
			logger.Warning(
				SceneTagsRecommendedMissingEvent,
				"Configured scene_tags.yml is missing recommended default tags. Manual update is recommended.",
				new Dictionary<string, string>(StringComparer.Ordinal)
				{
					["missing_count"] = missingRecommendedTags.Count.ToString(CultureInfo.InvariantCulture),
					["missing_tags"] = string.Join(", ", missingRecommendedTags)
				});
		}

		IMangaEquivalentsUpdateService mangaEquivalentsUpdateService = new MangaEquivalentsUpdateService(sceneTagMatcher);
		IMangaEquivalenceService mangaEquivalenceService = new MangaEquivalenceCatalog(
			documents.MangaEquivalents,
			sceneTagMatcher,
			mangaEquivalentsUpdateService,
			new YamlDocumentParser());
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
