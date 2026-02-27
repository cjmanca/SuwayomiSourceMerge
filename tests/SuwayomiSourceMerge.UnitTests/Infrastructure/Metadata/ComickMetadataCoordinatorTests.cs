namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies cooldown persistence behavior for <see cref="ComickMetadataCoordinator"/> cancellation and failure paths.
/// </summary>
public sealed partial class ComickMetadataCoordinatorTests
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
		Func<string, CancellationToken, ComickDirectApiResult<ComickSearchResponse>> searchHandler,
		IMangaEquivalenceCatalog? mangaEquivalenceCatalog = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
		ArgumentNullException.ThrowIfNull(searchHandler);

		RecordingComickApiGateway apiGateway = new(searchHandler);
		RecordingComickCandidateMatcher candidateMatcher = new();
		RecordingOverrideCoverService coverService = new();
		RecordingOverrideDetailsService detailsService = new();
		RecordingMetadataStateStore metadataStateStore = new();
		RecordingLogger logger = new();
		ComickMetadataCoordinator coordinator = new(
			apiGateway,
			candidateMatcher,
			coverService,
			detailsService,
			metadataStateStore,
			detailsDescriptionMode: "text",
			mangaEquivalenceCatalog,
			mangaEquivalentsYamlPath: Path.Combine(rootPath, "manga_equivalents.yml"),
			sceneTagMatcher: null,
			logger: logger);
		return new TestFixture(
			rootPath,
			coordinator,
			apiGateway,
			candidateMatcher,
			coverService,
			detailsService,
			metadataStateStore,
			logger);
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
}
