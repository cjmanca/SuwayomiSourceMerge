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
	/// Verifies candidate-matcher expected titles include display, canonical, and all equivalent titles with normalized deduplication.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Expected_ShouldPassExpandedEquivalentTitleSetToMatcher()
	{
		using TemporaryDirectory temporaryDirectory = new();
		StubEquivalentTitleCatalog catalog = new(
			resolvedCanonicalTitle: "Canonical Title",
			["Canonical Title", "Alt Title", "Display Title", " The Alt Title "]);
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.Success,
				new ComickSearchResponse(
				[
					new ComickSearchComic
					{
						Slug = "candidate-slug"
					}
				]),
				HttpStatusCode.OK,
				"Success."),
			catalog);
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Display Title");

		_ = fixture.Coordinator.EnsureMetadata(request);

		Assert.Equal(1, fixture.CandidateMatcher.MatchCallCount);
		Assert.Equal(
			["Display Title", "Canonical Title", "Alt Title"],
			fixture.CandidateMatcher.LastExpectedTitles);
	}

	/// <summary>
	/// Verifies expected-title fallback uses canonical lookup even when canonical title dedupes against display-title normalization.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Edge_ShouldLookupCanonicalEquivalents_WhenCanonicalMatchesDisplayTitle()
	{
		using TemporaryDirectory temporaryDirectory = new();
		CanonicalOnlyEquivalentTitleCatalog catalog = new(
			displayTitle: "The Canonical Title",
			canonicalTitle: "Canonical Title",
			canonicalEquivalentTitles: ["Canonical Title", "Alias From Canonical"]);
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => new ComickDirectApiResult<ComickSearchResponse>(
				ComickDirectApiOutcome.Success,
				new ComickSearchResponse(
				[
					new ComickSearchComic
					{
						Slug = "candidate-slug"
					}
				]),
				HttpStatusCode.OK,
				"Success."),
			catalog);
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("The Canonical Title");

		_ = fixture.Coordinator.EnsureMetadata(request);

		Assert.Equal(1, fixture.CandidateMatcher.MatchCallCount);
		Assert.Equal(
			["The Canonical Title", "Alias From Canonical"],
			fixture.CandidateMatcher.LastExpectedTitles);
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
			preferredLanguage: "en",
			metadataApiRequestDelay: TimeSpan.FromMilliseconds(1000),
			metadataApiCacheTtl: TimeSpan.FromHours(24));
	}

	/// <summary>
	/// Mutable equivalence-catalog stub used for expected-title expansion tests.
	/// </summary>
	private sealed class StubEquivalentTitleCatalog : IMangaEquivalenceCatalog
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="StubEquivalentTitleCatalog"/> class.
		/// </summary>
		/// <param name="resolvedCanonicalTitle">Resolved canonical title when lookup succeeds.</param>
		/// <param name="equivalentTitles">Equivalent-title group entries returned by the stub.</param>
		public StubEquivalentTitleCatalog(string resolvedCanonicalTitle, IReadOnlyList<string> equivalentTitles)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(resolvedCanonicalTitle);
			ArgumentNullException.ThrowIfNull(equivalentTitles);
			ResolvedCanonicalTitle = resolvedCanonicalTitle;
			EquivalentTitles = equivalentTitles.ToArray();
		}

		/// <summary>
		/// Gets resolved canonical title returned by this stub.
		/// </summary>
		private string ResolvedCanonicalTitle
		{
			get;
		}

		/// <summary>
		/// Gets equivalent-title entries returned by this stub.
		/// </summary>
		private IReadOnlyList<string> EquivalentTitles
		{
			get;
		}

		/// <inheritdoc />
		public MangaEquivalenceCatalogUpdateResult Update(MangaEquivalentsUpdateRequest request)
		{
			throw new InvalidOperationException("Update is not expected for expected-title expansion tests.");
		}

		/// <inheritdoc />
		public bool TryResolveCanonicalTitle(string inputTitle, out string canonicalTitle)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(inputTitle);
			canonicalTitle = ResolvedCanonicalTitle;
			return true;
		}

		/// <inheritdoc />
		public bool TryGetEquivalentTitles(string inputTitle, out IReadOnlyList<string> equivalentTitles)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(inputTitle);
			equivalentTitles = EquivalentTitles;
			return true;
		}

		/// <inheritdoc />
		public string ResolveCanonicalOrInput(string inputTitle)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(inputTitle);
			return ResolvedCanonicalTitle;
		}
	}

	/// <summary>
	/// Catalog stub that resolves canonical titles for display input and serves equivalents only for canonical lookups.
	/// </summary>
	private sealed class CanonicalOnlyEquivalentTitleCatalog : IMangaEquivalenceCatalog
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="CanonicalOnlyEquivalentTitleCatalog"/> class.
		/// </summary>
		/// <param name="displayTitle">Display title that resolves to canonical.</param>
		/// <param name="canonicalTitle">Canonical title returned by resolver.</param>
		/// <param name="canonicalEquivalentTitles">Equivalent titles returned only for canonical lookups.</param>
		public CanonicalOnlyEquivalentTitleCatalog(
			string displayTitle,
			string canonicalTitle,
			IReadOnlyList<string> canonicalEquivalentTitles)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);
			ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTitle);
			ArgumentNullException.ThrowIfNull(canonicalEquivalentTitles);

			DisplayTitle = displayTitle;
			CanonicalTitle = canonicalTitle;
			CanonicalEquivalentTitles = canonicalEquivalentTitles.ToArray();
		}

		/// <summary>
		/// Gets display title key.
		/// </summary>
		private string DisplayTitle
		{
			get;
		}

		/// <summary>
		/// Gets canonical title key.
		/// </summary>
		private string CanonicalTitle
		{
			get;
		}

		/// <summary>
		/// Gets canonical equivalent-title entries.
		/// </summary>
		private IReadOnlyList<string> CanonicalEquivalentTitles
		{
			get;
		}

		/// <inheritdoc />
		public MangaEquivalenceCatalogUpdateResult Update(MangaEquivalentsUpdateRequest request)
		{
			throw new InvalidOperationException("Update is not expected for canonical-only equivalence tests.");
		}

		/// <inheritdoc />
		public bool TryResolveCanonicalTitle(string inputTitle, out string canonicalTitle)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(inputTitle);
			canonicalTitle = CanonicalTitle;
			return string.Equals(inputTitle, DisplayTitle, StringComparison.Ordinal) ||
				string.Equals(inputTitle, CanonicalTitle, StringComparison.Ordinal);
		}

		/// <inheritdoc />
		public bool TryGetEquivalentTitles(string inputTitle, out IReadOnlyList<string> equivalentTitles)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(inputTitle);
			if (string.Equals(inputTitle, CanonicalTitle, StringComparison.Ordinal))
			{
				equivalentTitles = CanonicalEquivalentTitles;
				return true;
			}

			equivalentTitles = [];
			return false;
		}

		/// <inheritdoc />
		public string ResolveCanonicalOrInput(string inputTitle)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(inputTitle);
			return TryResolveCanonicalTitle(inputTitle, out string canonicalTitle)
				? canonicalTitle
				: inputTitle;
		}
	}
}
