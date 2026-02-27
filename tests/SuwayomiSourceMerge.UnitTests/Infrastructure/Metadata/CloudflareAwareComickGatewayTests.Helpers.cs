namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Globalization;
using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Provides shared fixtures and test doubles for <see cref="CloudflareAwareComickGateway"/> tests.
/// </summary>
public sealed partial class CloudflareAwareComickGatewayTests
{
	/// <summary>
	/// Parses one expected UTC timestamp string used by gateway tests.
	/// </summary>
	/// <param name="timestampText">Timestamp text in <c>yyyy-MM-ddTHH:mm:sszzz</c> format.</param>
	/// <returns>Parsed timestamp.</returns>
	private static DateTimeOffset ParseUtcTimestamp(string timestampText)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(timestampText);
		return DateTimeOffset.ParseExact(
			timestampText,
			"yyyy-MM-dd'T'HH:mm:sszzz",
			CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Creates a gateway with deterministic dependencies for unit testing.
	/// </summary>
	/// <param name="directClient">Direct Comick client stub.</param>
	/// <param name="stateStore">Metadata state store stub.</param>
	/// <param name="flaresolverrClient">Optional FlareSolverr client stub.</param>
	/// <param name="flaresolverrServerUri">Optional FlareSolverr server URI.</param>
	/// <param name="directRetryInterval">Sticky direct-retry interval.</param>
	/// <param name="nowUtc">Current timestamp.</param>
	/// <returns>Configured gateway instance.</returns>
	private static CloudflareAwareComickGateway CreateGateway(
		StubComickDirectApiClient directClient,
		InMemoryMetadataStateStore stateStore,
		StubFlaresolverrClient? flaresolverrClient,
		Uri? flaresolverrServerUri,
		TimeSpan directRetryInterval,
		DateTimeOffset nowUtc,
		RecordingLogger? logger = null)
	{
		return CreateGateway(
			directClient,
			stateStore,
			flaresolverrClient,
			flaresolverrServerUri,
			directRetryInterval,
			() => nowUtc,
			logger);
	}

	/// <summary>
	/// Creates a gateway with deterministic dependencies for unit testing.
	/// </summary>
	/// <param name="directClient">Direct Comick client stub.</param>
	/// <param name="stateStore">Metadata state store stub.</param>
	/// <param name="flaresolverrClient">Optional FlareSolverr client stub.</param>
	/// <param name="flaresolverrServerUri">Optional FlareSolverr server URI.</param>
	/// <param name="directRetryInterval">Sticky direct-retry interval.</param>
	/// <param name="utcNowProvider">Clock provider.</param>
	/// <returns>Configured gateway instance.</returns>
	private static CloudflareAwareComickGateway CreateGateway(
		StubComickDirectApiClient directClient,
		InMemoryMetadataStateStore stateStore,
		StubFlaresolverrClient? flaresolverrClient,
		Uri? flaresolverrServerUri,
		TimeSpan directRetryInterval,
		Func<DateTimeOffset> utcNowProvider,
		RecordingLogger? logger = null)
	{
		MetadataOrchestrationOptions options = new(
			TimeSpan.FromHours(24),
			flaresolverrServerUri,
			directRetryInterval,
			"en");
		return new CloudflareAwareComickGateway(
			directClient,
			flaresolverrClient,
			stateStore,
			options,
			new Uri("https://api.comick.dev/"),
			utcNowProvider,
			logger);
	}

	/// <summary>
	/// Creates one successful search result payload for direct-client stubs.
	/// </summary>
	/// <returns>Successful search result.</returns>
	private static ComickDirectApiResult<ComickSearchResponse> CreateDirectSearchSuccess()
	{
		ComickSearchResponse payload = new(
			[
				new ComickSearchComic
				{
					Hid = "hid-search",
					Slug = "slug-search",
					Title = "Title Search",
					MdTitles = [new ComickTitleAlias { Title = "Title Search" }],
					MdCovers = [new ComickCover { B2Key = "cover-search.jpg" }],
					Statistics = [new ComickStatistic()]
				}
			]);
		return new ComickDirectApiResult<ComickSearchResponse>(
			ComickDirectApiOutcome.Success,
			payload,
			HttpStatusCode.OK,
			"Success.");
	}

	/// <summary>
	/// Creates one successful comic result payload for direct-client stubs.
	/// </summary>
	/// <returns>Successful comic result.</returns>
	private static ComickDirectApiResult<ComickComicResponse> CreateDirectComicSuccess()
	{
		ComickComicResponse payload = new()
		{
			Comic = new ComickComicDetails
			{
				Hid = "hid-comic",
				Slug = "slug-comic",
				Title = "Title Comic",
				Links = new ComickComicLinks(),
				Statistics = [new ComickStatistic()],
				Recommendations = [],
				RelateFrom = [],
				MdTitles = [new ComickTitleAlias { Title = "Title Comic" }],
				MdCovers = [new ComickCover { B2Key = "cover-comic.jpg" }],
				GenreMappings = []
			}
		};
		return new ComickDirectApiResult<ComickComicResponse>(
			ComickDirectApiOutcome.Success,
			payload,
			HttpStatusCode.OK,
			"Success.");
	}

	/// <summary>
	/// Creates one successful FlareSolverr wrapper result with search payload.
	/// </summary>
	/// <returns>Successful FlareSolverr API result.</returns>
	private static FlaresolverrApiResult CreateFlaresolverrSearchSuccess()
	{
		return new FlaresolverrApiResult(
			FlaresolverrApiOutcome.Success,
			HttpStatusCode.OK,
			200,
			"""
			[
			  {
			    "hid": "hid-1",
			    "slug": "slug-1",
			    "title": "Title One",
			    "statistics": [],
			    "md_titles": [ { "title": "Title One" } ],
			    "md_covers": [ { "b2key": "cover.jpg" } ]
			  }
			]
			""",
			"Success.");
	}

	/// <summary>
	/// Creates one successful FlareSolverr wrapper result with comic payload.
	/// </summary>
	/// <returns>Successful FlareSolverr API result.</returns>
	private static FlaresolverrApiResult CreateFlaresolverrComicSuccess()
	{
		return new FlaresolverrApiResult(
			FlaresolverrApiOutcome.Success,
			HttpStatusCode.OK,
			200,
			"""
			{
			  "comic": {
			    "hid": "comic-hid-1",
			    "slug": "comic-slug-1",
			    "title": "Comic One",
			    "links": {},
			    "statistics": [],
			    "recommendations": [],
			    "relate_from": [],
			    "md_titles": [ { "title": "Comic One" } ],
			    "md_covers": [ { "b2key": "cover.jpg" } ],
			    "md_comic_md_genres": []
			  }
			}
			""",
			"Success.");
	}

	/// <summary>
	/// Creates one direct Cloudflare-blocked search result.
	/// </summary>
	/// <returns>Cloudflare-blocked result.</returns>
	private static ComickDirectApiResult<ComickSearchResponse> CreateDirectSearchCloudflareBlocked()
	{
		return new ComickDirectApiResult<ComickSearchResponse>(
			ComickDirectApiOutcome.CloudflareBlocked,
			payload: null,
			HttpStatusCode.Forbidden,
			"Cloudflare challenge detected.");
	}

	/// <summary>
	/// Creates one direct Cloudflare-blocked comic result.
	/// </summary>
	/// <returns>Cloudflare-blocked result.</returns>
	private static ComickDirectApiResult<ComickComicResponse> CreateDirectComicCloudflareBlocked()
	{
		return new ComickDirectApiResult<ComickComicResponse>(
			ComickDirectApiOutcome.CloudflareBlocked,
			payload: null,
			HttpStatusCode.Forbidden,
			"Cloudflare challenge detected.");
	}

	/// <summary>
	/// Creates one direct search malformed-payload result.
	/// </summary>
	/// <returns>Malformed-payload result.</returns>
	private static ComickDirectApiResult<ComickSearchResponse> CreateDirectSearchMalformedPayload()
	{
		return new ComickDirectApiResult<ComickSearchResponse>(
			ComickDirectApiOutcome.MalformedPayload,
			payload: null,
			HttpStatusCode.OK,
			"Malformed payload.");
	}

	/// <summary>
	/// Minimal in-memory metadata state store for gateway tests.
	/// </summary>
	private sealed class InMemoryMetadataStateStore : IMetadataStateStore
	{
		/// <summary>
		/// Backing snapshot.
		/// </summary>
		private MetadataStateSnapshot _snapshot;

		/// <summary>
		/// Initializes a new instance of the <see cref="InMemoryMetadataStateStore"/> class.
		/// </summary>
		/// <param name="initialSnapshot">Initial snapshot.</param>
		public InMemoryMetadataStateStore(MetadataStateSnapshot initialSnapshot)
		{
			ArgumentNullException.ThrowIfNull(initialSnapshot);
			_snapshot = initialSnapshot;
		}

		/// <summary>
		/// Gets the number of transform calls executed.
		/// </summary>
		public int TransformCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public MetadataStateSnapshot Read()
		{
			return new MetadataStateSnapshot(_snapshot.TitleCooldownsUtc, _snapshot.StickyFlaresolverrUntilUtc);
		}

		/// <inheritdoc />
		public void Transform(Func<MetadataStateSnapshot, MetadataStateSnapshot> transformer)
		{
			ArgumentNullException.ThrowIfNull(transformer);
			TransformCallCount++;
			MetadataStateSnapshot transformed = transformer(Read());
			_snapshot = new MetadataStateSnapshot(transformed.TitleCooldownsUtc, transformed.StickyFlaresolverrUntilUtc);
		}
	}

	/// <summary>
	/// Minimal direct-client stub for gateway tests.
	/// </summary>
	private sealed class StubComickDirectApiClient : IComickDirectApiClient
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="StubComickDirectApiClient"/> class.
		/// </summary>
		/// <param name="searchHandler">Search result callback.</param>
		/// <param name="comicHandler">Comic result callback.</param>
		public StubComickDirectApiClient(
			Func<string, ComickDirectApiResult<ComickSearchResponse>> searchHandler,
			Func<string, ComickDirectApiResult<ComickComicResponse>> comicHandler)
		{
			SearchHandler = searchHandler ?? throw new ArgumentNullException(nameof(searchHandler));
			ComicHandler = comicHandler ?? throw new ArgumentNullException(nameof(comicHandler));
		}

		/// <summary>
		/// Gets search callback.
		/// </summary>
		private Func<string, ComickDirectApiResult<ComickSearchResponse>> SearchHandler
		{
			get;
		}

		/// <summary>
		/// Gets comic callback.
		/// </summary>
		private Func<string, ComickDirectApiResult<ComickComicResponse>> ComicHandler
		{
			get;
		}

		/// <summary>
		/// Gets search call count.
		/// </summary>
		public int SearchCallCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets comic call count.
		/// </summary>
		public int ComicCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
			string query,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			SearchCallCount++;
			return Task.FromResult(SearchHandler(query));
		}

		/// <inheritdoc />
		public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
			string slug,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ComicCallCount++;
			return Task.FromResult(ComicHandler(slug));
		}
	}

	/// <summary>
	/// Minimal FlareSolverr-client stub for gateway tests.
	/// </summary>
	private sealed class StubFlaresolverrClient : IFlaresolverrClient
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="StubFlaresolverrClient"/> class.
		/// </summary>
		/// <param name="handler">Request callback.</param>
		public StubFlaresolverrClient(Func<string, FlaresolverrApiResult> handler)
		{
			Handler = handler ?? throw new ArgumentNullException(nameof(handler));
		}

		/// <summary>
		/// Gets request callback.
		/// </summary>
		private Func<string, FlaresolverrApiResult> Handler
		{
			get;
		}

		/// <summary>
		/// Gets invocation count.
		/// </summary>
		public int CallCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets most recent request payload.
		/// </summary>
		public string? LastPayload
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public Task<FlaresolverrApiResult> PostV1Async(
			string requestPayloadJson,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			CallCount++;
			LastPayload = requestPayloadJson;
			return Task.FromResult(Handler(requestPayloadJson));
		}
	}
}
