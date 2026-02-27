namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Logging coverage for <see cref="ComickMetadataCoordinator"/>.
/// </summary>
public sealed partial class ComickMetadataCoordinatorTests
{
	/// <summary>
	/// Verifies artifact-exists skip logs include normalized title keys for early-return paths.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Expected_ShouldLogArtifactExistsSkipsWithNormalizedTitleKey_WhenArtifactsAlreadyExist()
	{
		using TemporaryDirectory temporaryDirectory = new();
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => CreateSearchResult(ComickDirectApiOutcome.Success));
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");
		File.WriteAllText(Path.Combine(request.PreferredOverrideDirectoryPath, "cover.jpg"), "binary");

		ComickMetadataCoordinatorResult result = fixture.Coordinator.EnsureMetadata(request);

		Assert.False(result.ApiCalled);
		RecordingLogger.CapturedLogEvent coverEvent = Assert.Single(
			fixture.Logger.Events,
			static entry => entry.EventId == "metadata.artifact.cover.skipped");
		RecordingLogger.CapturedLogEvent detailsEvent = Assert.Single(
			fixture.Logger.Events,
			static entry => entry.EventId == "metadata.artifact.details.skipped");
		Assert.Equal("canonicaltitle", coverEvent.Context!["normalized_title_key"]);
		Assert.Equal("canonicaltitle", detailsEvent.Context!["normalized_title_key"]);
	}

	/// <summary>
	/// Verifies cooldown-skipped paths emit structured debug diagnostics.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Expected_ShouldLogCooldownSkipped_WhenTitleCooldownIsActive()
	{
		using TemporaryDirectory temporaryDirectory = new();
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => CreateSearchResult(ComickDirectApiOutcome.Success));
		fixture.MetadataStateStore.SetSnapshot(
			new MetadataStateSnapshot(
				new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal)
				{
					["canonicaltitle"] = DateTimeOffset.UtcNow.AddHours(1)
				},
				stickyFlaresolverrUntilUtc: null));
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");

		ComickMetadataCoordinatorResult result = fixture.Coordinator.EnsureMetadata(request);

		Assert.False(result.ApiCalled);
		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			fixture.Logger.Events,
			static entry => entry.EventId == "metadata.cooldown.skipped");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal("Canonical Title", logEvent.Context!["title"]);
		Assert.Equal("canonicaltitle", logEvent.Context["normalized_title_key"]);
	}

	/// <summary>
	/// Verifies cover-write failures emit structured warning diagnostics.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Failure_ShouldLogCoverFailureWarning_WhenCoverEnsureFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
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
				"Success."));
		fixture.CandidateMatcher.NextMatchResult = CreateMatchedCandidateResult();
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithoutArtifacts("Canonical Title");

		_ = fixture.Coordinator.EnsureMetadata(request);

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			fixture.Logger.Events,
			static entry => entry.EventId == "metadata.artifact.cover.failed");
		Assert.Equal(LogLevel.Warning, logEvent.Level);
		Assert.Equal(OverrideCoverOutcome.WriteFailed.ToString(), logEvent.Context!["outcome"]);
		Assert.Equal("Canonical Title", logEvent.Context["title"]);
	}

	/// <summary>
	/// Verifies details writes emit structured debug diagnostics.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Expected_ShouldLogDetailsWritten_WhenDetailsAreGenerated()
	{
		using TemporaryDirectory temporaryDirectory = new();
		TestFixture fixture = CreateFixture(
			temporaryDirectory.Path,
			static (_, _) => CreateSearchResult(ComickDirectApiOutcome.NotFound));
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithoutArtifacts("Canonical Title");
		string detailsPath = Path.Combine(request.PreferredOverrideDirectoryPath, "details.json");
		fixture.DetailsService.NextResult = new OverrideDetailsResult(
			OverrideDetailsOutcome.GeneratedFromComick,
			detailsPath,
			detailsJsonExists: true,
			sourceDetailsJsonPath: null,
			comicInfoXmlPath: null);

		_ = fixture.Coordinator.EnsureMetadata(request);

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			fixture.Logger.Events,
			static entry => entry.EventId == "metadata.artifact.details.written");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal(OverrideDetailsOutcome.GeneratedFromComick.ToString(), logEvent.Context!["outcome"]);
		Assert.Equal(detailsPath, logEvent.Context["details_path"]);
	}

	/// <summary>
	/// Verifies failed equivalents updates emit warning diagnostics.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Failure_ShouldLogEquivalentsUpdateWarning_WhenCatalogUpdateFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingMangaEquivalenceCatalog catalog = new(
			new MangaEquivalenceCatalogUpdateResult(
				MangaEquivalenceCatalogUpdateOutcome.UpdateFailed,
				new MangaEquivalentsUpdateResult(
					MangaEquivalentsUpdateOutcome.WriteFailed,
					Path.Combine(temporaryDirectory.Path, "manga_equivalents.yml"),
					MangaEquivalentsUpdateResult.NoAffectedGroupIndex,
					0,
					"write failed"),
				"catalog update failed"));
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
		fixture.CandidateMatcher.NextMatchResult = CreateMatchedCandidateResult();
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");

		_ = fixture.Coordinator.EnsureMetadata(request);

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			fixture.Logger.Events,
			static entry => entry.EventId == "metadata.equivalents.update");
		Assert.Equal(LogLevel.Warning, logEvent.Level);
		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.UpdateFailed.ToString(), logEvent.Context!["catalog_outcome"]);
		Assert.Equal(MangaEquivalentsUpdateOutcome.WriteFailed.ToString(), logEvent.Context["updater_outcome"]);
	}

	/// <summary>
	/// Verifies successful/no-change equivalents updates emit debug diagnostics.
	/// </summary>
	[Fact]
	public void EnsureMetadata_Expected_ShouldLogEquivalentsUpdateDebug_WhenCatalogUpdateHasNoChanges()
	{
		using TemporaryDirectory temporaryDirectory = new();
		RecordingMangaEquivalenceCatalog catalog = new(
			new MangaEquivalenceCatalogUpdateResult(
				MangaEquivalenceCatalogUpdateOutcome.NoChanges,
				new MangaEquivalentsUpdateResult(
					MangaEquivalentsUpdateOutcome.NoChanges,
					Path.Combine(temporaryDirectory.Path, "manga_equivalents.yml"),
					0,
					0,
					diagnostic: null),
				diagnostic: null));
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
		fixture.CandidateMatcher.NextMatchResult = CreateMatchedCandidateResult();
		ComickMetadataCoordinatorRequest request = fixture.CreateRequestWithExistingDetails("Canonical Title");

		_ = fixture.Coordinator.EnsureMetadata(request);

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			fixture.Logger.Events,
			static entry => entry.EventId == "metadata.equivalents.update");
		Assert.Equal(LogLevel.Debug, logEvent.Level);
		Assert.Equal(MangaEquivalenceCatalogUpdateOutcome.NoChanges.ToString(), logEvent.Context!["catalog_outcome"]);
		Assert.Equal(MangaEquivalentsUpdateOutcome.NoChanges.ToString(), logEvent.Context["updater_outcome"]);
	}

	/// <summary>
	/// Creates a deterministic matched-candidate payload for coordinator logging tests.
	/// </summary>
	/// <returns>Matched-candidate result.</returns>
	private static ComickCandidateMatchResult CreateMatchedCandidateResult()
	{
		return new ComickCandidateMatchResult(
			ComickCandidateMatchOutcome.Matched,
			new ComickComicResponse
			{
				Comic = new ComickComicDetails
				{
					Title = "Main Title",
					MdCovers =
					[
						new ComickCover
						{
							B2Key = "cover.webp"
						}
					],
					MdTitles =
					[
						new ComickTitleAlias
						{
							Title = "Alt One",
							Language = "en"
						}
					]
				}
			},
			matchedCandidateIndex: 0,
			hadTopTie: false,
			matchScore: 2);
	}

	/// <summary>
	/// Minimal mutable catalog fake for update-outcome logging assertions.
	/// </summary>
	private sealed class RecordingMangaEquivalenceCatalog : IMangaEquivalenceCatalog
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingMangaEquivalenceCatalog"/> class.
		/// </summary>
		/// <param name="nextUpdateResult">Update result returned by this fake.</param>
		public RecordingMangaEquivalenceCatalog(MangaEquivalenceCatalogUpdateResult nextUpdateResult)
		{
			NextUpdateResult = nextUpdateResult ?? throw new ArgumentNullException(nameof(nextUpdateResult));
		}

		/// <summary>
		/// Gets or sets the update result returned by this fake.
		/// </summary>
		public MangaEquivalenceCatalogUpdateResult NextUpdateResult
		{
			get;
			set;
		}

		/// <inheritdoc />
		public MangaEquivalenceCatalogUpdateResult Update(MangaEquivalentsUpdateRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);
			return NextUpdateResult;
		}

		/// <inheritdoc />
		public bool TryResolveCanonicalTitle(string inputTitle, out string canonicalTitle)
		{
			canonicalTitle = inputTitle;
			return false;
		}

		/// <inheritdoc />
		public string ResolveCanonicalOrInput(string inputTitle)
		{
			return inputTitle;
		}
	}
}
