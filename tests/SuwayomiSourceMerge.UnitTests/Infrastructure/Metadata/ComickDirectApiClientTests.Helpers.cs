namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.IO;
using System.Net;
using System.Text;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Provides shared fixtures and payload builders for direct Comick client tests.
/// </summary>
public sealed partial class ComickDirectApiClientTests
{
	/// <summary>
	/// Creates one client instance with deterministic options and provided HTTP client.
	/// </summary>
	/// <param name="httpClient">HTTP client to use.</param>
	/// <returns>Constructed direct API client.</returns>
	private static ComickDirectApiClient CreateClient(HttpClient httpClient)
	{
		return new ComickDirectApiClient(
			new ComickDirectApiClientOptions(
				new Uri("https://api.comick.dev/"),
				TimeSpan.FromSeconds(10)),
			httpClient);
	}

	/// <summary>
	/// Creates one HTTP response with UTF-8 JSON content type.
	/// </summary>
	/// <param name="statusCode">Response status code.</param>
	/// <param name="content">Response content text.</param>
	/// <returns>Configured response.</returns>
	private static HttpResponseMessage CreateResponse(HttpStatusCode statusCode, string content)
	{
		return new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(content, Encoding.UTF8, "application/json")
		};
	}

	/// <summary>
	/// Creates one minimal valid search JSON payload used by tests.
	/// </summary>
	/// <returns>JSON payload.</returns>
	private static string CreateSearchJson()
	{
		return
			"""
			[
			  {
			    "id": 1,
			    "hid": "hid-1",
			    "slug": "slug-1",
			    "title": "Title One",
			    "rating": "9.1",
			    "bayesian_rating": "9.0",
			    "rating_count": 2,
			    "statistics": [
			      {
			        "score_count": 2,
			        "weighted_score": "9.0",
			        "distribution": {
			          "1": 0,
			          "2": 0,
			          "3": 0,
			          "4": 0,
			          "5": 0,
			          "6": 0,
			          "7": 0,
			          "8": 1,
			          "9": 0,
			          "10": 1
			        },
			        "score": "9.1"
			      }
			    ],
			    "desc": "desc",
			    "status": 1,
			    "last_chapter": 10,
			    "translation_completed": false,
			    "view_count": 20,
			    "content_rating": "safe",
			    "demographic": 1,
			    "uploaded_at": "2026-02-22T00:00:00Z",
			    "genres": [1,2],
			    "created_at": "2026-02-21T00:00:00Z",
			    "user_follow_count": 4,
			    "year": 2020,
			    "country": "jp",
			    "is_english_title": null,
			    "md_titles": [ { "title": "Title One" } ],
			    "md_covers": [ { "w": 100, "h": 200, "b2key": "cover.jpg" } ],
			    "mu_comics": { "year": 2020 },
			    "highlight": "<mark>Title</mark>"
			  }
			]
			""";
	}

	/// <summary>
	/// Creates one minimal valid comic-detail JSON payload used by tests.
	/// </summary>
	/// <returns>JSON payload.</returns>
	private static string CreateComicJson()
	{
		return
			"""
			{
			  "firstChap": {
			    "chap": "1",
			    "hid": "chapter-hid-1",
			    "lang": "en",
			    "group_name": [ "Official" ],
			    "vol": "1"
			  },
			  "comic": {
			    "id": 1,
			    "hid": "hid-1",
			    "title": "Title One",
			    "country": "jp",
			    "status": 1,
			    "links": { "al": "100" },
			    "last_chapter": 10,
			    "chapter_count": 100,
			    "demographic": 1,
			    "follow_rank": 1,
			    "user_follow_count": 500,
			    "desc": "desc",
			    "parsed": "<p>desc</p>",
			    "slug": "slug-1",
			    "mismatch": null,
			    "year": 2020,
			    "bayesian_rating": "9.0",
			    "rating_count": 100,
			    "content_rating": "safe",
			    "statistics": [
			      {
			        "score_count": 2,
			        "weighted_score": "9.0",
			        "distribution": {
			          "1": 0,
			          "2": 0,
			          "3": 0,
			          "4": 0,
			          "5": 0,
			          "6": 0,
			          "7": 0,
			          "8": 1,
			          "9": 0,
			          "10": 1
			        },
			        "score": "9.1"
			      }
			    ],
			    "translation_completed": false,
			    "chapter_numbers_reset_on_new_volume_manual": false,
			    "final_chapter": null,
			    "final_volume": null,
			    "noindex": false,
			    "adsense": true,
			    "comment_count": 0,
			    "login_required": false,
			    "has_anime": true,
			    "anime": {
			      "start": "Vol 1",
			      "end": "Vol 2"
			    },
			    "reviews": [],
			    "recommendations": [
			      {
			        "up": 1,
			        "down": 0,
			        "total": 1,
			        "relates": {
			          "title": "Related Title",
			          "slug": "related-slug",
			          "hid": "related-hid",
			          "md_covers": [
			            { "vol": "1", "w": 10, "h": 20, "b2key": "related.jpg" }
			          ]
			        }
			      }
			    ],
			    "relate_from": [
			      {
			        "relate_to": { "slug": "origin-slug", "title": "Origin Title" },
			        "md_relates": { "name": "Sequel" }
			      }
			    ],
			    "md_titles": [ { "title": "Title One", "lang": "en" } ],
			    "is_english_title": null,
			    "md_comic_md_genres": [
			      {
			        "md_genres": { "name": "Action", "type": "main", "slug": "action", "group": "Genre" }
			      }
			    ],
			    "md_covers": [ { "vol": "1", "w": 100, "h": 200, "b2key": "cover.jpg" } ],
			    "mu_comics": { "year": 2020 },
			    "iso639_1": "ja",
			    "lang_name": "Japanese",
			    "lang_native": "\u65e5\u672c\u8a9e (\u306b\u307b\u3093\u3054\uff0f\u306b\u3063\u307d\u3093\u3054)"
			  },
			  "artists": [ { "name": "Artist One", "slug": "artist-one" } ],
			  "authors": [ { "name": "Author One", "slug": "author-one" } ],
			  "langList": [ "en" ],
			  "recommendable": true,
			  "demographic": "Shounen",
			  "matureContent": false,
			  "checkVol2Chap1": false
			}
			""";
	}

	/// <summary>
	/// Minimal recording HTTP message handler used by direct-client tests.
	/// </summary>
	private sealed class RecordingHttpMessageHandler : HttpMessageHandler
	{
		/// <summary>
		/// Sends one response based on the provided request.
		/// </summary>
		private readonly Func<HttpRequestMessage, HttpResponseMessage> _send;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingHttpMessageHandler"/> class.
		/// </summary>
		/// <param name="send">Response factory callback.</param>
		public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
		{
			_send = send ?? throw new ArgumentNullException(nameof(send));
		}

		/// <summary>
		/// Gets the most recent request instance.
		/// </summary>
		public HttpRequestMessage? LastRequest
		{
			get;
			private set;
		}

		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);
			cancellationToken.ThrowIfCancellationRequested();
			LastRequest = request;
			return Task.FromResult(_send(request));
		}
	}

	/// <summary>
	/// Minimal <see cref="HttpContent"/> that throws while streaming content.
	/// </summary>
	private sealed class ThrowingHttpContent : HttpContent
	{
		/// <summary>
		/// Delegate used to create one exception per read attempt.
		/// </summary>
		private readonly Func<Exception> _exceptionFactory;

		/// <summary>
		/// Initializes a new instance of the <see cref="ThrowingHttpContent"/> class.
		/// </summary>
		/// <param name="exceptionFactory">Exception factory for read attempts.</param>
		public ThrowingHttpContent(Func<Exception> exceptionFactory)
		{
			_exceptionFactory = exceptionFactory ?? throw new ArgumentNullException(nameof(exceptionFactory));
			Headers.ContentType = new("application/json");
		}

		/// <inheritdoc />
		protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
		{
			ArgumentNullException.ThrowIfNull(stream);
			throw _exceptionFactory();
		}

		/// <inheritdoc />
		protected override bool TryComputeLength(out long length)
		{
			length = 0;
			return false;
		}
	}
}
