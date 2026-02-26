using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Coordinates per-title Comick metadata behavior for merge-pass processing.
/// </summary>
internal sealed partial class ComickMetadataCoordinator : IComickMetadataCoordinator
{
	/// <summary>
	/// Canonical cover artifact file name.
	/// </summary>
	private const string CoverJpgFileName = "cover.jpg";

	/// <summary>
	/// Canonical details artifact file name.
	/// </summary>
	private const string DetailsJsonFileName = "details.json";

	/// <summary>
	/// Comick API gateway dependency.
	/// </summary>
	private readonly IComickApiGateway _comickApiGateway;

	/// <summary>
	/// Comick candidate matcher dependency.
	/// </summary>
	private readonly IComickCandidateMatcher _comickCandidateMatcher;

	/// <summary>
	/// Cover ensure service dependency.
	/// </summary>
	private readonly IOverrideCoverService _overrideCoverService;

	/// <summary>
	/// Details ensure service dependency.
	/// </summary>
	private readonly IOverrideDetailsService _overrideDetailsService;

	/// <summary>
	/// Persisted metadata state storage.
	/// </summary>
	private readonly IMetadataStateStore _metadataStateStore;

	/// <summary>
	/// Optional mutable runtime equivalence catalog used for Comick-driven alias sync updates.
	/// </summary>
	private readonly IMangaEquivalenceCatalog? _mangaEquivalenceCatalog;

	/// <summary>
	/// Path to <c>manga_equivalents.yml</c> used for runtime catalog updates.
	/// </summary>
	private readonly string _mangaEquivalentsYamlPath;

	/// <summary>
	/// details.json description mode used for ensure requests.
	/// </summary>
	private readonly string _detailsDescriptionMode;

	/// <summary>
	/// Title normalizer used for cooldown-key and expected-title processing.
	/// </summary>
	private readonly ITitleComparisonNormalizer _titleComparisonNormalizer;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickMetadataCoordinator"/> class.
	/// </summary>
	/// <param name="comickApiGateway">Comick API gateway dependency.</param>
	/// <param name="comickCandidateMatcher">Comick candidate matcher dependency.</param>
	/// <param name="overrideCoverService">Cover ensure service dependency.</param>
	/// <param name="overrideDetailsService">Details ensure service dependency.</param>
	/// <param name="metadataStateStore">Persisted metadata state storage.</param>
	/// <param name="detailsDescriptionMode">details.json description mode.</param>
	/// <param name="mangaEquivalenceCatalog">Optional mutable runtime equivalence catalog dependency.</param>
	/// <param name="mangaEquivalentsYamlPath">Path to <c>manga_equivalents.yml</c> used for update requests.</param>
	/// <param name="sceneTagMatcher">Optional scene-tag matcher used for cooldown key normalization.</param>
	public ComickMetadataCoordinator(
		IComickApiGateway comickApiGateway,
		IComickCandidateMatcher comickCandidateMatcher,
		IOverrideCoverService overrideCoverService,
		IOverrideDetailsService overrideDetailsService,
		IMetadataStateStore metadataStateStore,
		string detailsDescriptionMode,
		IMangaEquivalenceCatalog? mangaEquivalenceCatalog,
		string mangaEquivalentsYamlPath,
		ISceneTagMatcher? sceneTagMatcher = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(detailsDescriptionMode);
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaEquivalentsYamlPath);

		_comickApiGateway = comickApiGateway ?? throw new ArgumentNullException(nameof(comickApiGateway));
		_comickCandidateMatcher = comickCandidateMatcher ?? throw new ArgumentNullException(nameof(comickCandidateMatcher));
		_overrideCoverService = overrideCoverService ?? throw new ArgumentNullException(nameof(overrideCoverService));
		_overrideDetailsService = overrideDetailsService ?? throw new ArgumentNullException(nameof(overrideDetailsService));
		_metadataStateStore = metadataStateStore ?? throw new ArgumentNullException(nameof(metadataStateStore));
		_detailsDescriptionMode = detailsDescriptionMode.Trim();
		_mangaEquivalenceCatalog = mangaEquivalenceCatalog;
		_mangaEquivalentsYamlPath = Path.GetFullPath(mangaEquivalentsYamlPath);
		_titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);
	}

	/// <inheritdoc />
	public ComickMetadataCoordinatorResult EnsureMetadata(
		ComickMetadataCoordinatorRequest request,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		cancellationToken.ThrowIfCancellationRequested();

		bool coverExists = ArtifactExists(request, CoverJpgFileName);
		bool detailsExists = ArtifactExists(request, DetailsJsonFileName);
		if (coverExists && detailsExists)
		{
			return new ComickMetadataCoordinatorResult(
				apiCalled: false,
				hadServiceInterruption: false,
				coverExists: true,
				detailsExists: true);
		}

		string normalizedTitleKey = _titleComparisonNormalizer.NormalizeTitleKey(request.DisplayTitle);
		DateTimeOffset nowUtc = DateTimeOffset.UtcNow;
		bool cooldownActive = IsCooldownActive(normalizedTitleKey, nowUtc);

		bool apiCalled = false;
		bool hadServiceInterruption = false;
		ComickComicResponse? matchedComic = null;
		if (!cooldownActive)
		{
			(apiCalled, hadServiceInterruption, matchedComic) = ResolveMatchedComic(
				request,
				normalizedTitleKey,
				nowUtc,
				cancellationToken);
		}

		if (!coverExists && matchedComic is not null)
		{
			EnsureCover(request, matchedComic, cancellationToken);
			coverExists = ArtifactExists(request, CoverJpgFileName);
		}

		if (!detailsExists)
		{
			EnsureDetails(request, matchedComic);
			detailsExists = ArtifactExists(request, DetailsJsonFileName);
		}

		if (matchedComic is not null)
		{
			TryUpdateMangaEquivalents(matchedComic, request.MetadataOrchestration.PreferredLanguage);
		}

		return new ComickMetadataCoordinatorResult(
			apiCalled,
			hadServiceInterruption,
			coverExists,
			detailsExists);
	}

	/// <summary>
	/// Resolves a matched Comick payload when API calls are allowed for this title.
	/// </summary>
	/// <param name="request">Coordinator request.</param>
	/// <param name="normalizedTitleKey">Normalized title key.</param>
	/// <param name="nowUtc">Current UTC timestamp.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>API call metadata, interruption flag, and matched comic payload when available.</returns>
	private (bool ApiCalled, bool HadServiceInterruption, ComickComicResponse? MatchedComic) ResolveMatchedComic(
		ComickMetadataCoordinatorRequest request,
		string normalizedTitleKey,
		DateTimeOffset nowUtc,
		CancellationToken cancellationToken)
	{
		bool shouldPersistCooldown = true;
		try
		{
			ComickDirectApiResult<ComickSearchResponse> searchResult = _comickApiGateway
				.SearchAsync(request.DisplayTitle, cancellationToken)
				.GetAwaiter()
				.GetResult();

			if (searchResult.Outcome == ComickDirectApiOutcome.Cancelled &&
				cancellationToken.IsCancellationRequested)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			if (IsServiceInterruptionOutcome(searchResult.Outcome))
			{
				return (true, true, null);
			}

			if (searchResult.Outcome != ComickDirectApiOutcome.Success || searchResult.Payload is null)
			{
				return (true, false, null);
			}

			ComickCandidateMatchResult matchResult = _comickCandidateMatcher
				.MatchAsync(
					searchResult.Payload.Comics,
					BuildExpectedTitles(request.DisplayTitle),
					cancellationToken)
				.GetAwaiter()
				.GetResult();

			if (matchResult.Outcome == ComickCandidateMatchOutcome.Matched)
			{
				return (true, false, matchResult.MatchedCandidate);
			}

			if (matchResult.HadServiceInterruption)
			{
				return (true, true, null);
			}

			return (true, false, null);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			shouldPersistCooldown = false;
			throw;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return (true, true, null);
		}
		finally
		{
			if (shouldPersistCooldown)
			{
				TryPersistCooldown(
					normalizedTitleKey,
					nowUtc + request.MetadataOrchestration.ComickMetadataCooldown);
			}
		}
	}

	/// <summary>
	/// Ensures <c>cover.jpg</c> when a matched Comick payload provides a usable cover key.
	/// </summary>
	/// <param name="request">Coordinator request.</param>
	/// <param name="matchedComic">Matched Comick payload.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	private void EnsureCover(
		ComickMetadataCoordinatorRequest request,
		ComickComicResponse matchedComic,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(matchedComic);

		string? coverKey = TryResolveCoverKey(matchedComic);
		if (string.IsNullOrWhiteSpace(coverKey))
		{
			return;
		}

		_ = _overrideCoverService
			.EnsureCoverJpgAsync(
				new OverrideCoverRequest(
					request.PreferredOverrideDirectoryPath,
					request.AllOverrideDirectoryPaths,
					coverKey),
				cancellationToken)
			.GetAwaiter()
			.GetResult();
	}

	/// <summary>
	/// Ensures <c>details.json</c> using matched Comick payload when available and source/ComicInfo fallback when needed.
	/// </summary>
	/// <param name="request">Coordinator request.</param>
	/// <param name="matchedComic">Optional matched Comick payload.</param>
	private void EnsureDetails(
		ComickMetadataCoordinatorRequest request,
		ComickComicResponse? matchedComic)
	{
		ArgumentNullException.ThrowIfNull(request);

		_ = _overrideDetailsService.EnsureDetailsJson(
			new OverrideDetailsRequest(
				request.PreferredOverrideDirectoryPath,
				request.AllOverrideDirectoryPaths,
				request.OrderedSourceDirectoryPaths,
				request.DisplayTitle,
				_detailsDescriptionMode,
				request.MetadataOrchestration,
				matchedComic));
	}
}
