namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Additional workflow fixture fakes split from the primary fixture file.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
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
	/// In-memory metadata-state fake.
	/// </summary>
	private sealed class InMemoryMetadataStateStore : IMetadataStateStore
	{
		/// <summary>
		/// Current snapshot value.
		/// </summary>
		private MetadataStateSnapshot _snapshot = MetadataStateSnapshot.Empty;

		/// <inheritdoc />
		public MetadataStateSnapshot Read()
		{
			return _snapshot;
		}

		/// <inheritdoc />
		public void Transform(Func<MetadataStateSnapshot, MetadataStateSnapshot> transformer)
		{
			ArgumentNullException.ThrowIfNull(transformer);
			_snapshot = transformer(_snapshot);
		}
	}

	/// <summary>
	/// Comick API gateway fake that returns configurable search results.
	/// </summary>
	private sealed class RecordingComickApiGateway : IComickApiGateway
	{
		/// <summary>
		/// Gets or sets next search result payload.
		/// </summary>
		public ComickDirectApiResult<ComickSearchResponse> NextSearchResult
		{
			get;
			set;
		} = new(
			ComickDirectApiOutcome.Success,
			new ComickSearchResponse([]),
			statusCode: System.Net.HttpStatusCode.OK,
			diagnostic: "Success.");

		/// <summary>
		/// Gets the number of search requests.
		/// </summary>
		public int SearchCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
			string query,
			CancellationToken cancellationToken = default)
		{
			SearchCallCount++;
			return Task.FromResult(NextSearchResult);
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
			string slug,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(
				new ComickDirectApiResult<ComickComicResponse>(
					ComickDirectApiOutcome.NotFound,
					payload: null,
					statusCode: System.Net.HttpStatusCode.NotFound,
					diagnostic: "Not Found."));
		}
	}

	/// <summary>
	/// Comick candidate matcher fake.
	/// </summary>
	private sealed class RecordingComickCandidateMatcher : IComickCandidateMatcher
	{
		/// <summary>
		/// Gets or sets the next match result.
		/// </summary>
		public ComickCandidateMatchResult NextMatchResult
		{
			get;
			set;
		} = new(
			ComickCandidateMatchOutcome.NoHighConfidenceMatch,
			matchedCandidate: null,
			ComickCandidateMatchResult.NoMatchCandidateIndex,
			hadTopTie: false,
			matchScore: 0);

		/// <summary>
		/// Gets recorded expected-title inputs.
		/// </summary>
		public List<IReadOnlyList<string>> ExpectedTitles
		{
			get;
		} = [];

		/// <summary>
		/// Gets the number of match calls.
		/// </summary>
		public int MatchCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task<ComickCandidateMatchResult> MatchAsync(
			IReadOnlyList<ComickSearchComic> candidates,
			IReadOnlyList<string> expectedTitles,
			CancellationToken cancellationToken = default)
		{
			MatchCallCount++;
			ExpectedTitles.Add(expectedTitles.ToArray());
			return Task.FromResult(NextMatchResult);
		}
	}

	/// <summary>
	/// cover.jpg service fake that records requests and writes deterministic files by default.
	/// </summary>
	private sealed class RecordingOverrideCoverService : IOverrideCoverService
	{
		/// <summary>
		/// Gets recorded requests.
		/// </summary>
		public List<OverrideCoverRequest> Requests
		{
			get;
		} = [];

		/// <summary>
		/// Gets or sets a value indicating whether the fake writes cover.jpg to disk.
		/// </summary>
		public bool WriteCoverToDisk
		{
			get;
			set;
		} = true;

		/// <inheritdoc />
		public Task<OverrideCoverResult> EnsureCoverJpgAsync(
			OverrideCoverRequest request,
			CancellationToken cancellationToken = default)
		{
			Requests.Add(request);

			string coverPath = Path.Combine(request.PreferredOverrideDirectoryPath, "cover.jpg");
			if (WriteCoverToDisk)
			{
				Directory.CreateDirectory(request.PreferredOverrideDirectoryPath);
				File.WriteAllBytes(coverPath, [0xFF, 0xD8, 0xFF, 0xD9]);
			}

			bool exists = File.Exists(coverPath);
			OverrideCoverOutcome outcome = exists
				? OverrideCoverOutcome.WrittenDownloadedJpeg
				: OverrideCoverOutcome.WriteFailed;
			return Task.FromResult(
				new OverrideCoverResult(
					outcome,
					coverPath,
					exists,
					existingCoverPath: exists ? coverPath : null,
					coverUri: null,
					diagnostic: exists ? null : "cover not written"));
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
			Directory.CreateDirectory(request.PreferredOverrideDirectoryPath);
			string detailsPath = Path.Combine(request.PreferredOverrideDirectoryPath, "details.json");
			File.WriteAllText(detailsPath, "{}");
			return new OverrideDetailsResult(
				OverrideDetailsOutcome.AlreadyExists,
				detailsPath,
				detailsJsonExists: true,
				sourceDetailsJsonPath: null,
				comicInfoXmlPath: null);
		}
	}
}
