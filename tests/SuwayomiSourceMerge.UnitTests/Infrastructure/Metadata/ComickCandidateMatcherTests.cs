namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="ComickCandidateMatcher"/>.
/// </summary>
public sealed class ComickCandidateMatcherTests
{
	/// <summary>
	/// Verifies the first search result is selected when its comic-detail title matches expected keys.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Expected_ShouldSelectFirstSearchResult_WhenDetailComicTitleMatches()
	{
		RecordingComickApiGateway gateway = new(
			slug => CreateSuccessResult(
				CreateDetailPayload(
					comicTitle: slug == "first-slug" ? "Target Title" : "Different Title")));
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "Unrelated Search Title"),
				CreateSearchCandidate("second-slug", "Target Title")
			],
			["target title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(2, result.MatchScore);
		Assert.Equal(["first-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies detail aliases are used for matching while search aliases are not used as matching input.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Expected_ShouldIgnoreSearchMdTitles_WhenDetailDoesNotMatch()
	{
		RecordingComickApiGateway gateway = new(
			slug => slug switch
			{
				"first-slug" => CreateSuccessResult(CreateDetailPayload("Wrong Detail Title", "Wrong Detail Alias")),
				"second-slug" => CreateSuccessResult(CreateDetailPayload("Different Title", "Target Alias")),
				_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound)
			});
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "Wrong Search Title", "Target Alias"),
				CreateSearchCandidate("second-slug", "Second Search Title", "Other Search Alias")
			],
			["target alias"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(1, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(1, result.MatchScore);
		Assert.Equal(["first-slug", "second-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies remaining candidates are ordered by normalized Levenshtein similarity and can use search aliases as ranking hints.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Edge_ShouldOrderRemainingCandidatesByLevenshtein_UsingSearchAliasHints()
	{
		RecordingComickApiGateway gateway = new(
			_ => CreateSuccessResult(CreateDetailPayload("No Match")));
		ComickCandidateMatcher matcher = new(gateway);

		_ = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "first"),
				CreateSearchCandidate("ranked-by-alias", "zzz", "target title"),
				CreateSearchCandidate("ranked-by-title", "target titel"),
				CreateSearchCandidate("ranked-last", "abc")
			],
			["target title"]);

		Assert.Equal(
			["first-slug", "ranked-by-alias", "ranked-by-title", "ranked-last"],
			gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies equal similarity ties for remaining candidates preserve original index ordering.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Edge_ShouldUseOriginalIndexOrder_WhenRemainingSimilarityTies()
	{
		RecordingComickApiGateway gateway = new(
			_ => CreateSuccessResult(CreateDetailPayload("No Match")));
		ComickCandidateMatcher matcher = new(gateway);

		_ = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "first"),
				CreateSearchCandidate("second-slug", "abc"),
				CreateSearchCandidate("third-slug", "abc")
			],
			["target title"]);

		Assert.Equal(["first-slug", "second-slug", "third-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies candidates with empty slugs are skipped safely.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Edge_ShouldSkipCandidate_WhenSlugIsBlank()
	{
		RecordingComickApiGateway gateway = new(
			_ => CreateSuccessResult(CreateDetailPayload("Target Title")));
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate(string.Empty, "target title"),
				CreateSearchCandidate("second-slug", "target title")
			],
			["target title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(1, result.MatchedCandidateIndex);
		Assert.Equal(["second-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies the matcher continues through mixed detail failures and selects a later successful match.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Failure_ShouldContinueOnNonSuccessOutcomes_AndSelectLaterMatch()
	{
		RecordingComickApiGateway gateway = new(
			slug => slug switch
			{
				"cloudflare" => CreateOutcomeOnlyResult(ComickDirectApiOutcome.CloudflareBlocked),
				"http-failure" => CreateOutcomeOnlyResult(ComickDirectApiOutcome.HttpFailure),
				"success" => CreateSuccessResult(CreateDetailPayload("Target Title")),
				_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound)
			});
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("cloudflare", "target title"),
				CreateSearchCandidate("http-failure", "target title"),
				CreateSearchCandidate("success", "target title")
			],
			["target title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(2, result.MatchedCandidateIndex);
		Assert.Equal(["cloudflare", "http-failure", "success"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies cancelled detail requests are surfaced as cancellation instead of no-match.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Failure_ShouldThrowOperationCanceledException_WhenDetailRequestReturnsCancelled()
	{
		RecordingComickApiGateway gateway = new(
			slug => slug switch
			{
				"cancelled" => CreateOutcomeOnlyResult(ComickDirectApiOutcome.Cancelled),
				"success" => CreateSuccessResult(CreateDetailPayload("Target Title")),
				_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound)
			});
		ComickCandidateMatcher matcher = new(gateway);
		using CancellationTokenSource cancellationTokenSource = new();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => matcher.MatchAsync(
				[
					CreateSearchCandidate("cancelled", "target title"),
					CreateSearchCandidate("success", "target title")
				],
				["target title"],
				cancellationTokenSource.Token));
		Assert.Equal(["cancelled"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies no successful detail matches return no-high-confidence output.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Failure_ShouldReturnNoHighConfidenceMatch_WhenNoDetailMatchesFound()
	{
		RecordingComickApiGateway gateway = new(
			_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.MalformedPayload));
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "first"),
				CreateSearchCandidate("second-slug", "second")
			],
			["target title"]);

		Assert.Equal(ComickCandidateMatchOutcome.NoHighConfidenceMatch, result.Outcome);
		Assert.Null(result.MatchedCandidate);
		Assert.Equal(ComickCandidateMatchResult.NoMatchCandidateIndex, result.MatchedCandidateIndex);
		Assert.False(result.HadTopTie);
		Assert.Equal(0, result.MatchScore);
		Assert.Equal(["first-slug", "second-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies scene-tag normalization continues to work when matching comic-detail titles.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Expected_ShouldMatchDetailTitleAfterSceneTagNormalization()
	{
		RecordingComickApiGateway gateway = new(
			_ => CreateSuccessResult(CreateDetailPayload("Manga Title")));
		ComickCandidateMatcher matcher = new(
			gateway,
			new SceneTagMatcher(SceneTagsDocumentDefaults.Create().Tags!));

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[CreateSearchCandidate("slug-1", "Manga Title [Official]")],
			["Manga Title [Official]"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.Equal(2, result.MatchScore);
	}

	/// <summary>
	/// Verifies null argument guards for candidate and expected title collections.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Failure_ShouldThrow_WhenArgumentsAreNull()
	{
		RecordingComickApiGateway gateway = new(_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound));
		ComickCandidateMatcher matcher = new(gateway);

		await Assert.ThrowsAsync<ArgumentNullException>(() => matcher.MatchAsync(null!, ["title"]));
		await Assert.ThrowsAsync<ArgumentNullException>(() => matcher.MatchAsync([], null!));
	}

	/// <summary>
	/// Verifies null candidate entries are rejected deterministically.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Failure_ShouldThrow_WhenCandidatesContainNullEntry()
	{
		RecordingComickApiGateway gateway = new(_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound));
		ComickCandidateMatcher matcher = new(gateway);

		await Assert.ThrowsAsync<ArgumentException>(() => matcher.MatchAsync([null!], ["title"]));
	}

	/// <summary>
	/// Verifies empty/invalid expected keys short-circuit to no-match without detail requests.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Failure_ShouldReturnNoMatchWithoutRequests_WhenExpectedKeysAreEmptyAfterNormalization()
	{
		RecordingComickApiGateway gateway = new(_ => CreateSuccessResult(CreateDetailPayload("Target")));
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[CreateSearchCandidate("slug-1", "Target")],
			["   "]);

		Assert.Equal(ComickCandidateMatchOutcome.NoHighConfidenceMatch, result.Outcome);
		Assert.Empty(gateway.RequestedSlugs);
	}

	/// <summary>
	/// Creates one search-candidate payload.
	/// </summary>
	/// <param name="slug">Search candidate slug.</param>
	/// <param name="title">Search candidate title.</param>
	/// <param name="searchMdTitles">Search alias hints.</param>
	/// <returns>Search candidate.</returns>
	private static ComickSearchComic CreateSearchCandidate(
		string slug,
		string title,
		params string[] searchMdTitles)
	{
		ArgumentNullException.ThrowIfNull(searchMdTitles);

		return new ComickSearchComic
		{
			Slug = slug,
			Title = title,
			MdTitles = searchMdTitles
				.Select(
					aliasTitle => new ComickTitleAlias
					{
						Title = aliasTitle
					})
				.ToArray()
		};
	}

	/// <summary>
	/// Creates one comic-detail payload used by match outcomes.
	/// </summary>
	/// <param name="comicTitle">Primary detail title.</param>
	/// <param name="mdTitles">Detail alias values.</param>
	/// <returns>Comic detail payload.</returns>
	private static ComickComicResponse CreateDetailPayload(string comicTitle, params string[] mdTitles)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(comicTitle);
		ArgumentNullException.ThrowIfNull(mdTitles);

		return new ComickComicResponse
		{
			Comic = new ComickComicDetails
			{
				Title = comicTitle,
				MdTitles = mdTitles
					.Select(
						title => new ComickTitleAlias
						{
							Title = title
						})
					.ToArray()
			}
		};
	}

	/// <summary>
	/// Creates one successful comic-detail API result.
	/// </summary>
	/// <param name="payload">Payload value.</param>
	/// <returns>Success result.</returns>
	private static ComickDirectApiResult<ComickComicResponse> CreateSuccessResult(ComickComicResponse payload)
	{
		return new ComickDirectApiResult<ComickComicResponse>(
			ComickDirectApiOutcome.Success,
			payload,
			HttpStatusCode.OK,
			"Success.");
	}

	/// <summary>
	/// Creates one non-success result with no payload.
	/// </summary>
	/// <param name="outcome">Non-success outcome.</param>
	/// <returns>Result instance.</returns>
	private static ComickDirectApiResult<ComickComicResponse> CreateOutcomeOnlyResult(ComickDirectApiOutcome outcome)
	{
		return new ComickDirectApiResult<ComickComicResponse>(
			outcome,
			payload: null,
			statusCode: null,
			diagnostic: outcome.ToString());
	}

	/// <summary>
	/// Recording gateway test double for candidate matcher tests.
	/// </summary>
	private sealed class RecordingComickApiGateway : IComickApiGateway
	{
		/// <summary>
		/// Detail-result callback.
		/// </summary>
		private readonly Func<string, ComickDirectApiResult<ComickComicResponse>> _comicHandler;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingComickApiGateway"/> class.
		/// </summary>
		/// <param name="comicHandler">Detail result callback by slug.</param>
		public RecordingComickApiGateway(Func<string, ComickDirectApiResult<ComickComicResponse>> comicHandler)
		{
			_comicHandler = comicHandler ?? throw new ArgumentNullException(nameof(comicHandler));
		}

		/// <summary>
		/// Gets requested comic slugs in call order.
		/// </summary>
		public List<string> RequestedSlugs
		{
			get;
		} = [];

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
			string query,
			CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(query);
			cancellationToken.ThrowIfCancellationRequested();

			return Task.FromResult(
				new ComickDirectApiResult<ComickSearchResponse>(
					ComickDirectApiOutcome.Success,
					new ComickSearchResponse([]),
					HttpStatusCode.OK,
					"Success."));
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
			string slug,
			CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(slug);
			cancellationToken.ThrowIfCancellationRequested();

			RequestedSlugs.Add(slug);
			return Task.FromResult(_comicHandler(slug));
		}
	}
}
