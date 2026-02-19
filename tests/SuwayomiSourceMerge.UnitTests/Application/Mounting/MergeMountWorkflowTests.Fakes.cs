namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Mounts;
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

		string overrideRootPath = Path.Combine(temporaryDirectory.Path, "override");
		string overrideVolumePath = Path.Combine(overrideRootPath, "priority");
		Directory.CreateDirectory(overrideVolumePath);

		string mergedRootPath = Path.Combine(temporaryDirectory.Path, "merged");
		Directory.CreateDirectory(mergedRootPath);

		string branchLinksRootPath = Path.Combine(temporaryDirectory.Path, "branch-links");
		Directory.CreateDirectory(branchLinksRootPath);

		MergeMountWorkflowOptions options = new(
			sourcesRootPath,
			overrideRootPath,
			mergedRootPath,
			branchLinksRootPath,
			detailsDescriptionMode: "text",
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
			DetailsService = new RecordingOverrideDetailsService();
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
		/// Gets details service fake.
		/// </summary>
		public RecordingOverrideDetailsService DetailsService
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
				DetailsService,
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

	/// <summary>
	/// Branch planning fake.
	/// </summary>
	private sealed class RecordingBranchPlanningService : IMergerfsBranchPlanningService
	{
		/// <summary>
		/// Gets canonical titles that should trigger simulated planning failures.
		/// </summary>
		public HashSet<string> ThrowOnCanonicalTitles
		{
			get;
		} = new(StringComparer.Ordinal);

		/// <summary>
		/// Gets canonical titles that should trigger simulated cancellation failures.
		/// </summary>
		public HashSet<string> ThrowCancellationOnCanonicalTitles
		{
			get;
		} = new(StringComparer.Ordinal);

		/// <summary>
		/// Gets planned requests in call order.
		/// </summary>
		public List<MergerfsBranchPlanningRequest> PlannedRequests
		{
			get;
		} = [];

		/// <inheritdoc />
		public MergerfsBranchPlan Plan(MergerfsBranchPlanningRequest request)
		{
			PlannedRequests.Add(request);

			if (ThrowCancellationOnCanonicalTitles.Contains(request.CanonicalTitle))
			{
				throw new OperationCanceledException($"Simulated cancellation for '{request.CanonicalTitle}'.");
			}

			if (ThrowOnCanonicalTitles.Contains(request.CanonicalTitle))
			{
				throw new InvalidOperationException($"Simulated planning failure for '{request.CanonicalTitle}'.");
			}

			string branchDirectoryPath = Path.Combine(request.BranchLinksRootPath, request.GroupKey);
			string overrideTitlePath = Path.Combine(request.OverrideVolumePaths[0], request.CanonicalTitle);
			Directory.CreateDirectory(overrideTitlePath);
			List<MergerfsBranchLinkDefinition> branchLinks =
			[
				new MergerfsBranchLinkDefinition(
					"00_override",
					Path.Combine(branchDirectoryPath, "00_override"),
					overrideTitlePath,
					MergerfsBranchAccessMode.ReadWrite)
			];

			for (int index = 0; index < request.SourceBranches.Count; index++)
			{
				MergerfsSourceBranchCandidate sourceBranch = request.SourceBranches[index];
				string sourceLinkName = $"10_source_{index:D2}";
				branchLinks.Add(new MergerfsBranchLinkDefinition(
					sourceLinkName,
					Path.Combine(branchDirectoryPath, sourceLinkName),
					sourceBranch.SourcePath,
					MergerfsBranchAccessMode.ReadOnly));
			}

			string branchSpecification = string.Join(
				':',
				branchLinks.Select(
					static link => $"{link.LinkPath}={(link.AccessMode == MergerfsBranchAccessMode.ReadWrite ? "RW" : "RO")}"));

			return new MergerfsBranchPlan(
				overrideTitlePath,
				branchDirectoryPath,
				branchSpecification,
				$"suwayomi_{request.GroupKey}",
				request.GroupKey,
				branchLinks);
		}
	}

	/// <summary>
	/// Snapshot fake that returns configurable snapshots.
	/// </summary>
	private sealed class RecordingMountSnapshotService : IMountSnapshotService
	{
		/// <summary>
		/// Snapshot sequence consumed by <see cref="Capture"/>.
		/// </summary>
		private readonly Queue<MountSnapshot> _queuedSnapshots = [];

		/// <summary>
		/// Gets or sets next snapshot value.
		/// </summary>
		public MountSnapshot NextSnapshot
		{
			get;
			set;
		} = new MountSnapshot([], []);

		/// <summary>
		/// Gets the number of capture invocations.
		/// </summary>
		public int CaptureCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets or sets a provider for applied actions used to synthesize readiness snapshots.
		/// </summary>
		public Func<IReadOnlyList<MountReconciliationAction>>? AppliedActionsProvider
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets a value indicating whether synthesized readiness snapshots are enabled.
		/// </summary>
		public bool AutoIncludeAppliedMountActions
		{
			get;
			set;
		} = true;

		/// <summary>
		/// Enqueues one snapshot for subsequent capture calls.
		/// </summary>
		/// <param name="snapshot">Snapshot value.</param>
		public void EnqueueSnapshot(MountSnapshot snapshot)
		{
			ArgumentNullException.ThrowIfNull(snapshot);
			_queuedSnapshots.Enqueue(snapshot);
		}

		/// <inheritdoc />
		public MountSnapshot Capture()
		{
			CaptureCount++;
			if (_queuedSnapshots.Count > 0)
			{
				return _queuedSnapshots.Dequeue();
			}

			if (NextSnapshot.Entries.Count > 0 || NextSnapshot.Warnings.Count > 0)
			{
				return NextSnapshot;
			}

			if (AutoIncludeAppliedMountActions && AppliedActionsProvider is not null)
			{
				HashSet<string> seenMountPoints = new(StringComparer.Ordinal);
				List<MountSnapshotEntry> entries = [];
				IReadOnlyList<MountReconciliationAction> appliedActions = AppliedActionsProvider();
				for (int index = 0; index < appliedActions.Count; index++)
				{
					MountReconciliationAction action = appliedActions[index];
					if (action.Kind != MountReconciliationActionKind.Mount &&
						action.Kind != MountReconciliationActionKind.Remount)
					{
						continue;
					}

					if (seenMountPoints.Add(action.MountPoint))
					{
						entries.Add(new MountSnapshotEntry(action.MountPoint, "fuse.mergerfs", "test", "rw", isHealthy: null));
					}
				}

				if (entries.Count > 0)
				{
					return new MountSnapshot(entries, []);
				}
			}

			return NextSnapshot;
		}
	}

	/// <summary>
	/// Reconciliation fake that records input and emits configurable plans.
	/// </summary>
	private sealed class RecordingReconciliationService : IMountReconciliationService
	{
		/// <summary>
		/// Gets or sets the next-plan factory.
		/// </summary>
		public Func<MountReconciliationInput, MountReconciliationPlan> NextPlanFactory
		{
			get;
			set;
		} = input =>
		{
			MountReconciliationAction[] actions = input.DesiredMounts
				.Select(static desired => new MountReconciliationAction(
					MountReconciliationActionKind.Mount,
					desired.MountPoint,
					desired.DesiredIdentity,
					desired.MountPayload,
					MountReconciliationReason.MissingMount))
				.ToArray();
			return new MountReconciliationPlan(actions);
		};

		/// <summary>
		/// Gets last input.
		/// </summary>
		public MountReconciliationInput? LastInput
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public MountReconciliationPlan Reconcile(MountReconciliationInput input)
		{
			LastInput = input;
			return NextPlanFactory(input);
		}
	}

	/// <summary>
	/// Branch staging fake that records stage/cleanup calls.
	/// </summary>
	private sealed class RecordingBranchLinkStagingService : IBranchLinkStagingService
	{
		/// <summary>
		/// Gets staged plans.
		/// </summary>
		public List<MergerfsBranchPlan> StagedPlans
		{
			get;
		} = [];

		/// <summary>
		/// Gets cleanup call count.
		/// </summary>
		public int CleanupCalls
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets active branch directories from the last cleanup call.
		/// </summary>
		public IReadOnlySet<string>? LastCleanupActiveBranchDirectoryPaths
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public void StageBranchLinks(MergerfsBranchPlan plan)
		{
			StagedPlans.Add(plan);
		}

		/// <inheritdoc />
		public void CleanupStaleBranchDirectories(string branchLinksRootPath, IReadOnlySet<string> activeBranchDirectoryPaths)
		{
			CleanupCalls++;
			LastCleanupActiveBranchDirectoryPaths = new HashSet<string>(activeBranchDirectoryPaths, PathSafetyPolicy.GetPathComparer());
		}
	}

	/// <summary>
	/// details.json service fake that records requests.
	/// </summary>
	private sealed class RecordingOverrideDetailsService : IOverrideDetailsService
	{
		/// <summary>
		/// Gets recorded requests.
		/// </summary>
		public List<OverrideDetailsRequest> Requests
		{
			get;
		} = [];

		/// <inheritdoc />
		public OverrideDetailsResult EnsureDetailsJson(OverrideDetailsRequest request)
		{
			Requests.Add(request);
			return new OverrideDetailsResult(
				OverrideDetailsOutcome.AlreadyExists,
				Path.Combine(request.PreferredOverrideDirectoryPath, "details.json"),
				detailsJsonExists: true,
				sourceDetailsJsonPath: null,
				comicInfoXmlPath: null);
		}
	}
}
