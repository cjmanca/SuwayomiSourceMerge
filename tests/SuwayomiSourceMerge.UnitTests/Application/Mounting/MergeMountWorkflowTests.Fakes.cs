namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Volumes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Fixture and fakes for <see cref="MergeMountWorkflowTests"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Creates workflow fixture state and source/override directories.
	/// </summary>
	/// <param name="temporaryDirectory">Temporary directory fixture.</param>
	/// <returns>Workflow fixture.</returns>
	private static WorkflowFixture CreateFixture(
		TemporaryDirectory temporaryDirectory,
		bool cleanupApplyHighPriority = false,
		IReadOnlyList<string>? excludedSources = null,
		int maxConsecutiveMountFailures = 5)
	{
		string sourcesRootPath = Path.Combine(temporaryDirectory.Path, "sources");
		string sourceVolumePath = Path.Combine(sourcesRootPath, "disk1");
		string sourceTitlePath = Path.Combine(sourceVolumePath, "SourceA", "Title One");
		Directory.CreateDirectory(sourceTitlePath);

		string configRootPath = Path.Combine(temporaryDirectory.Path, "config");
		Directory.CreateDirectory(configRootPath);

		string overrideRootPath = Path.Combine(temporaryDirectory.Path, "override");
		string overrideVolumePath = Path.Combine(overrideRootPath, "priority");
		Directory.CreateDirectory(overrideVolumePath);

		string mergedRootPath = Path.Combine(temporaryDirectory.Path, "merged");
		Directory.CreateDirectory(mergedRootPath);

		string branchLinksRootPath = Path.Combine(temporaryDirectory.Path, "branch-links");
		Directory.CreateDirectory(branchLinksRootPath);

		MergeMountWorkflowOptions options = new(
			configRootPath,
			sourcesRootPath,
			overrideRootPath,
			mergedRootPath,
			branchLinksRootPath,
			detailsDescriptionMode: "text",
			metadataOrchestration: new MetadataOrchestrationOptions(
				comickMetadataCooldown: TimeSpan.FromHours(24),
				flaresolverrServerUri: null,
				flaresolverrDirectRetryInterval: TimeSpan.FromMinutes(60),
				preferredLanguage: "en"),
			mergerfsOptionsBase: "allow_other",
			excludedSources: excludedSources ?? [],
			enableMountHealthcheck: false,
			maxConsecutiveMountFailures: maxConsecutiveMountFailures,
			startupCleanupEnabled: true,
			unmountOnExit: true,
			cleanupHighPriority: true,
			cleanupApplyHighPriority,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20,
			unmountCommandTimeout: TimeSpan.FromSeconds(5),
			commandPollInterval: TimeSpan.FromMilliseconds(10));

		return new WorkflowFixture(
			options,
			sourceVolumePath,
			overrideVolumePath);
	}

	/// <summary>
	/// Workflow fixture that owns fakes and workflow construction helpers.
	/// </summary>
	private sealed class WorkflowFixture
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="WorkflowFixture"/> class.
		/// </summary>
		/// <param name="options">Workflow options.</param>
		/// <param name="sourceVolumePath">Source volume path.</param>
		/// <param name="overrideVolumePath">Override volume path.</param>
		public WorkflowFixture(
			MergeMountWorkflowOptions options,
			string sourceVolumePath,
			string overrideVolumePath)
		{
			Options = options;
			Logger = new RecordingLogger();
			VolumeDiscoveryService = new RecordingVolumeDiscoveryService(sourceVolumePath, overrideVolumePath);
			BranchPlanningService = new RecordingBranchPlanningService();
			MountSnapshotService = new RecordingMountSnapshotService();
			ReconciliationService = new RecordingReconciliationService();
			MountCommandService = new RecordingMountCommandService();
			BranchStagingService = new RecordingBranchLinkStagingService();
			ComickApiGateway = new RecordingComickApiGateway();
			ComickCandidateMatcher = new RecordingComickCandidateMatcher();
			CoverService = new RecordingOverrideCoverService();
			DetailsService = new RecordingOverrideDetailsService();
			MetadataStateStore = new InMemoryMetadataStateStore();
			MountSnapshotService.AppliedActionsProvider = () => MountCommandService.AppliedActions;
		}

		/// <summary>
		/// Gets workflow options.
		/// </summary>
		public MergeMountWorkflowOptions Options
		{
			get;
		}

		/// <summary>
		/// Gets logger fake.
		/// </summary>
		public RecordingLogger Logger
		{
			get;
		}

		/// <summary>
		/// Gets volume discovery fake.
		/// </summary>
		public RecordingVolumeDiscoveryService VolumeDiscoveryService
		{
			get;
		}

		/// <summary>
		/// Gets branch planning fake.
		/// </summary>
		public RecordingBranchPlanningService BranchPlanningService
		{
			get;
		}

		/// <summary>
		/// Gets mount snapshot fake.
		/// </summary>
		public RecordingMountSnapshotService MountSnapshotService
		{
			get;
		}

		/// <summary>
		/// Gets reconciliation fake.
		/// </summary>
		public RecordingReconciliationService ReconciliationService
		{
			get;
		}

		/// <summary>
		/// Gets mount command fake.
		/// </summary>
		public RecordingMountCommandService MountCommandService
		{
			get;
		}

		/// <summary>
		/// Gets branch staging fake.
		/// </summary>
		public RecordingBranchLinkStagingService BranchStagingService
		{
			get;
		}

		/// <summary>
		/// Gets Comick API gateway fake.
		/// </summary>
		public RecordingComickApiGateway ComickApiGateway
		{
			get;
		}

		/// <summary>
		/// Gets Comick candidate matcher fake.
		/// </summary>
		public RecordingComickCandidateMatcher ComickCandidateMatcher
		{
			get;
		}

		/// <summary>
		/// Gets cover service fake.
		/// </summary>
		public RecordingOverrideCoverService CoverService
		{
			get;
		}

		/// <summary>
		/// Gets details service fake.
		/// </summary>
		public RecordingOverrideDetailsService DetailsService
		{
			get;
		}

		/// <summary>
		/// Gets metadata state store fake.
		/// </summary>
		public InMemoryMetadataStateStore MetadataStateStore
		{
			get;
		}

		/// <summary>
		/// Creates a workflow under this fixture.
		/// </summary>
		/// <returns>Workflow instance.</returns>
		public MergeMountWorkflow CreateWorkflow()
		{
			ISceneTagMatcher sceneTagMatcher = new SceneTagMatcher(SceneTagsDocumentDefaults.Create().Tags ?? []);
			IMangaEquivalenceService mangaEquivalenceService = new MangaEquivalenceService(
				new MangaEquivalentsDocument
				{
					Groups =
					[
						new MangaEquivalentGroup
						{
							Canonical = "Canonical Title",
							Aliases = ["Title One"]
						}
					]
				},
				sceneTagMatcher);
			ComickMetadataCoordinator metadataCoordinator = new(
				ComickApiGateway,
				ComickCandidateMatcher,
				CoverService,
				DetailsService,
				MetadataStateStore,
				Options.DetailsDescriptionMode,
				mangaEquivalenceCatalog: null,
				mangaEquivalentsYamlPath: Path.Combine(Options.ConfigRootPath, "manga_equivalents.yml"),
				sceneTagMatcher);

			return new MergeMountWorkflow(
				Options,
				mangaEquivalenceService,
				sceneTagMatcher,
				VolumeDiscoveryService,
				BranchPlanningService,
				MountSnapshotService,
				ReconciliationService,
				MountCommandService,
				BranchStagingService,
				metadataCoordinator,
				Logger);
		}
	}

	/// <summary>
	/// Volume discovery fake.
	/// </summary>
	private sealed class RecordingVolumeDiscoveryService : IContainerVolumeDiscoveryService
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingVolumeDiscoveryService"/> class.
		/// </summary>
		/// <param name="sourceVolumePath">Source volume path.</param>
		/// <param name="overrideVolumePath">Override volume path.</param>
		public RecordingVolumeDiscoveryService(string sourceVolumePath, string overrideVolumePath)
		{
			SourceVolumePaths = [sourceVolumePath];
			OverrideVolumePaths = [overrideVolumePath];
		}

		/// <summary>
		/// Gets or sets source volumes.
		/// </summary>
		public IReadOnlyList<string> SourceVolumePaths
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets override volumes.
		/// </summary>
		public IReadOnlyList<string> OverrideVolumePaths
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets discovery warnings.
		/// </summary>
		public IReadOnlyList<ContainerVolumeDiscoveryWarning> Warnings
		{
			get;
			set;
		} = [];

		/// <summary>
		/// Gets or sets one optional callback invoked when discovery executes.
		/// </summary>
		public Action? OnDiscover
		{
			get;
			set;
		}

		/// <inheritdoc />
		public ContainerVolumeDiscoveryResult Discover(string sourcesRootPath, string overrideRootPath)
		{
			OnDiscover?.Invoke();
			return new ContainerVolumeDiscoveryResult(SourceVolumePaths, OverrideVolumePaths, Warnings);
		}
	}

}
