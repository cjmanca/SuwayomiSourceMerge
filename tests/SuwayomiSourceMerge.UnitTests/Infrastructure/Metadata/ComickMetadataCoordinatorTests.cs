namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies cooldown persistence behavior for <see cref="ComickMetadataCoordinator"/> cancellation and failure paths.
/// </summary>
public sealed class ComickMetadataCoordinatorTests
{
	/// <summary>
	/// Verifies non-cancelled search outcomes persist per-title cooldown state.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Expected_ShouldPersistCooldown_WhenSearchIsNotCancelled()
	{
		using TemporaryDirectory temporaryDirectory = new();
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => CreateSearchResult(ComickDirectApiOutcome.NotFound));
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");

		ComickMetadataCoordinatorResult result = fixture.Coordinator.EnsureMetadata(request);

		Assert.True(result.ApiCalled);
		Assert.False(result.HadServiceInterruption);
		Assert.Equal(1, fixture.MetadataStateStore.TransformCallCount);
		Assert.Single(fixture.MetadataStateStore.Read().TitleCooldownsUtc);
		Assert.Equal(0, fixture.CandidateMatcher.MatchCallCount);
		Assert.Equal(0, fixture.CoverService.CallCount);
		Assert.Equal(0, fixture.DetailsService.CallCount);
	}

	/// <summary>
	/// Verifies cooperative cancellation skips cooldown persistence and throws cooperatively.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Edge_ShouldThrowAndSkipCooldown_WhenSearchIsCancelledCooperatively()
	{
		using TemporaryDirectory temporaryDirectory = new();
		using CancellationTokenSource cancellationTokenSource = new();
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			(_, _) =>
			{
				cancellationTokenSource.Cancel();
				return CreateSearchResult(ComickDirectApiOutcome.Cancelled);
			});
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");

		Assert.ThrowsAny<OperationCanceledException>(
			() => fixture.Coordinator.EnsureMetadata(request, cancellationTokenSource.Token));

		Assert.Equal(0, fixture.MetadataStateStore.TransformCallCount);
		Assert.Empty(fixture.MetadataStateStore.Read().TitleCooldownsUtc);
		Assert.Equal(0, fixture.CandidateMatcher.MatchCallCount);
		Assert.Equal(0, fixture.CoverService.CallCount);
		Assert.Equal(0, fixture.DetailsService.CallCount);
	}

	/// <summary>
	/// Verifies cooperative cancellation during candidate matching skips cooldown persistence and throws cooperatively.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Edge_ShouldThrowAndSkipCooldown_WhenCandidateMatchingIsCancelledCooperatively()
	{
		using TemporaryDirectory temporaryDirectory = new();
		using CancellationTokenSource cancellationTokenSource = new();
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => CreateSearchResult(ComickDirectApiOutcome.Success));
		fixture.CandidateMatcher.ThrowOperationCanceledOnCall = true;
		fixture.CandidateMatcher.BeforeThrowOperationCanceled = cancellationTokenSource.Cancel;
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");

		Assert.ThrowsAny<OperationCanceledException>(
			() => fixture.Coordinator.EnsureMetadata(request, cancellationTokenSource.Token));

		Assert.Equal(0, fixture.MetadataStateStore.TransformCallCount);
		Assert.Empty(fixture.MetadataStateStore.Read().TitleCooldownsUtc);
		Assert.Equal(1, fixture.CandidateMatcher.MatchCallCount);
		Assert.Equal(0, fixture.CoverService.CallCount);
		Assert.Equal(0, fixture.DetailsService.CallCount);
	}

	/// <summary>
	/// Verifies non-cooperative cancelled outcomes still persist cooldown and report interruption semantics.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Failure_ShouldPersistCooldownAndReportInterruption_WhenSearchCancelledWithoutCallerCancellation()
	{
		using TemporaryDirectory temporaryDirectory = new();
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => CreateSearchResult(ComickDirectApiOutcome.Cancelled));
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");

		ComickMetadataCoordinatorResult result = fixture.Coordinator.EnsureMetadata(request);

		Assert.True(result.ApiCalled);
		Assert.True(result.HadServiceInterruption);
		Assert.Equal(1, fixture.MetadataStateStore.TransformCallCount);
		Assert.Single(fixture.MetadataStateStore.Read().TitleCooldownsUtc);
		Assert.Equal(0, fixture.CandidateMatcher.MatchCallCount);
		Assert.Equal(0, fixture.CoverService.CallCount);
		Assert.Equal(0, fixture.DetailsService.CallCount);
	}

	/// <summary>
	/// Creates one coordinator test fixture rooted at a temporary test path.
	/// </summary>
	/// <param name="rootPath">Temporary root path.</param>
	/// <param name="searchHandler">Search-handler callback used by the API gateway fake.</param>
	/// <returns>Configured fixture.</returns>
	private static TestFixture CreateFixture(
		string rootPath,
		Func<string, CancellationToken, ComickDirectApiResult<ComickSearchResponse>> searchHandler)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
		ArgumentNullException.ThrowIfNull(searchHandler);

		RecordingComickApiGateway apiGateway = new(searchHandler);
		RecordingComickCandidateMatcher candidateMatcher = new();
		RecordingOverrideCoverService coverService = new();
		RecordingOverrideDetailsService detailsService = new();
		RecordingMetadataStateStore metadataStateStore = new();
		ComickMetadataCoordinator coordinator = new(
			apiGateway,
			candidateMatcher,
			coverService,
			detailsService,
			metadataStateStore,
			detailsDescriptionMode: "text",
			mangaEquivalenceCatalog: null,
			mangaEquivalentsYamlPath: Path.Combine(rootPath, "manga_equivalents.yml"),
			sceneTagMatcher: null);
		return new TestFixture(
			rootPath,
			coordinator,
			apiGateway,
			candidateMatcher,
			coverService,
			detailsService,
			metadataStateStore);
	}

	/// <summary>
	/// Creates one deterministic search-result payload with the requested outcome.
	/// </summary>
	/// <param name="outcome">Search outcome.</param>
	/// <returns>Typed search result.</returns>
	private static ComickDirectApiResult<ComickSearchResponse> CreateSearchResult(ComickDirectApiOutcome outcome)
	{
		return new ComickDirectApiResult<ComickSearchResponse>(
			outcome,
			payload: outcome == ComickDirectApiOutcome.Success ? new ComickSearchResponse([]) : null,
			statusCode: outcome == ComickDirectApiOutcome.NotFound ? HttpStatusCode.NotFound : null,
			diagnostic: outcome.ToString());
	}

	/// <summary>
	/// Aggregates coordinator dependencies and helper methods used by tests.
	/// </summary>
	private sealed class TestFixture
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TestFixture"/> class.
		/// </summary>
		/// <param name="rootPath">Temporary root path.</param>
		/// <param name="coordinator">Coordinator under test.</param>
		/// <param name="apiGateway">API gateway fake.</param>
		/// <param name="candidateMatcher">Candidate-matcher fake.</param>
		/// <param name="coverService">Cover-service fake.</param>
		/// <param name="detailsService">Details-service fake.</param>
		/// <param name="metadataStateStore">Metadata-state-store fake.</param>
		public TestFixture(
			string rootPath,
			ComickMetadataCoordinator coordinator,
			RecordingComickApiGateway apiGateway,
			RecordingComickCandidateMatcher candidateMatcher,
			RecordingOverrideCoverService coverService,
			RecordingOverrideDetailsService detailsService,
			RecordingMetadataStateStore metadataStateStore)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
			RootPath = rootPath;
			Coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
			ApiGateway = apiGateway ?? throw new ArgumentNullException(nameof(apiGateway));
			CandidateMatcher = candidateMatcher ?? throw new ArgumentNullException(nameof(candidateMatcher));
			CoverService = coverService ?? throw new ArgumentNullException(nameof(coverService));
			DetailsService = detailsService ?? throw new ArgumentNullException(nameof(detailsService));
			MetadataStateStore = metadataStateStore ?? throw new ArgumentNullException(nameof(metadataStateStore));
		}

		/// <summary>
		/// Gets temporary root path.
		/// </summary>
		public string RootPath
		{
			get;
		}

		/// <summary>
		/// Gets coordinator under test.
		/// </summary>
		public ComickMetadataCoordinator Coordinator
		{
			get;
		}

		/// <summary>
		/// Gets API gateway fake.
		/// </summary>
		public RecordingComickApiGateway ApiGateway
		{
			get;
		}

		/// <summary>
		/// Gets candidate matcher fake.
		/// </summary>
		public RecordingComickCandidateMatcher CandidateMatcher
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
		/// Gets metadata state-store fake.
		/// </summary>
		public RecordingMetadataStateStore MetadataStateStore
		{
			get;
		}

		/// <summary>
		/// Creates one request for a title where details.json already exists in the preferred override path.
		/// </summary>
		/// <param name="displayTitle">Display title.</param>
		/// <returns>Coordinator request.</returns>
		public ComickMetadataCoordinatorRequest CreateRequestWithExistingDetails(string displayTitle)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);

			string preferredOverridePath = Path.Combine(RootPath, "override", "priority", displayTitle);
			Directory.CreateDirectory(preferredOverridePath);
			File.WriteAllText(Path.Combine(preferredOverridePath, "details.json"), "{}");
			return new ComickMetadataCoordinatorRequest(
				preferredOverridePath,
				[preferredOverridePath],
				[],
				displayTitle,
				CreateMetadataOrchestrationOptions());
		}
	}

	/// <summary>
	/// Creates baseline metadata orchestration options for coordinator tests.
	/// </summary>
	/// <returns>Options instance.</returns>
	private static MetadataOrchestrationOptions CreateMetadataOrchestrationOptions()
	{
		return new MetadataOrchestrationOptions(
			comickMetadataCooldown: TimeSpan.FromHours(24),
			flaresolverrServerUri: null,
			flaresolverrDirectRetryInterval: TimeSpan.FromMinutes(60),
			preferredLanguage: "en");
	}

	/// <summary>
	/// Recording API gateway fake for coordinator tests.
	/// </summary>
	private sealed class RecordingComickApiGateway : IComickApiGateway
	{
		/// <summary>
		/// Search callback dependency.
		/// </summary>
		private readonly Func<string, CancellationToken, ComickDirectApiResult<ComickSearchResponse>> _searchHandler;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingComickApiGateway"/> class.
		/// </summary>
		/// <param name="searchHandler">Search callback dependency.</param>
		public RecordingComickApiGateway(Func<string, CancellationToken, ComickDirectApiResult<ComickSearchResponse>> searchHandler)
		{
			_searchHandler = searchHandler ?? throw new ArgumentNullException(nameof(searchHandler));
		}

		/// <summary>
		/// Gets the number of search calls.
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
			ArgumentException.ThrowIfNullOrWhiteSpace(query);
			SearchCallCount++;
			return Task.FromResult(_searchHandler(query, cancellationToken));
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
			string slug,
			CancellationToken cancellationToken = default)
		{
			throw new InvalidOperationException("Comic detail requests are not expected for these coordinator test scenarios.");
		}
	}

	/// <summary>
	/// Recording candidate matcher fake for coordinator tests.
	/// </summary>
	private sealed class RecordingComickCandidateMatcher : IComickCandidateMatcher
	{
		/// <summary>
		/// Gets or sets a value indicating whether the matcher should throw <see cref="OperationCanceledException"/>.
		/// </summary>
		public bool ThrowOperationCanceledOnCall
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets an optional callback executed immediately before throwing cancellation.
		/// </summary>
		public Action? BeforeThrowOperationCanceled
		{
			get;
			set;
		}

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
			if (ThrowOperationCanceledOnCall)
			{
				BeforeThrowOperationCanceled?.Invoke();
				throw new OperationCanceledException(cancellationToken);
			}

			return Task.FromResult(
				new ComickCandidateMatchResult(
					ComickCandidateMatchOutcome.NoHighConfidenceMatch,
					matchedCandidate: null,
					ComickCandidateMatchResult.NoMatchCandidateIndex,
					hadTopTie: false,
					matchScore: 0));
		}
	}

	/// <summary>
	/// Recording cover-service fake for coordinator tests.
	/// </summary>
	private sealed class RecordingOverrideCoverService : IOverrideCoverService
	{
		/// <summary>
		/// Gets the number of cover ensure calls.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task<OverrideCoverResult> EnsureCoverJpgAsync(
			OverrideCoverRequest request,
			CancellationToken cancellationToken = default)
		{
			CallCount++;
			return Task.FromResult(
				new OverrideCoverResult(
					OverrideCoverOutcome.WriteFailed,
					Path.Combine(request.PreferredOverrideDirectoryPath, "cover.jpg"),
					coverJpgExists: false,
					existingCoverPath: null,
					coverUri: null,
					diagnostic: "not expected"));
		}
	}

	/// <summary>
	/// Recording details-service fake for coordinator tests.
	/// </summary>
	private sealed class RecordingOverrideDetailsService : IOverrideDetailsService
	{
		/// <summary>
		/// Gets the number of details ensure calls.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public OverrideDetailsResult EnsureDetailsJson(OverrideDetailsRequest request)
		{
			CallCount++;
			return new OverrideDetailsResult(
				OverrideDetailsOutcome.AlreadyExists,
				Path.Combine(request.PreferredOverrideDirectoryPath, "details.json"),
				detailsJsonExists: true,
				sourceDetailsJsonPath: null,
				comicInfoXmlPath: null);
		}
	}

	/// <summary>
	/// Recording metadata-state-store fake for coordinator tests.
	/// </summary>
	private sealed class RecordingMetadataStateStore : IMetadataStateStore
	{
		/// <summary>
		/// Backing snapshot value.
		/// </summary>
		private MetadataStateSnapshot _snapshot = MetadataStateSnapshot.Empty;

		/// <summary>
		/// Gets the number of transform calls.
		/// </summary>
		public int TransformCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public MetadataStateSnapshot Read()
		{
			return _snapshot;
		}

		/// <inheritdoc />
		public void Transform(Func<MetadataStateSnapshot, MetadataStateSnapshot> transformer)
		{
			ArgumentNullException.ThrowIfNull(transformer);
			TransformCallCount++;
			_snapshot = transformer(_snapshot);
		}
	}
}
