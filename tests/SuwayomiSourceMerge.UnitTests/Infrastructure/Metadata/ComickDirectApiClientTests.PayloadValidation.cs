namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using System.Text.Json;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies direct Comick API malformed-payload classification behavior.
/// </summary>
public sealed partial class ComickDirectApiClientTests
{
	/// <summary>
	/// Verifies invalid JSON maps to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenJsonInvalidAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(HttpStatusCode.OK, "{"));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies search payloads remain valid when non-matching fields are omitted.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnSuccess_WhenNonMatchingFieldsAreMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "slug-1", "title": "Title" }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload!.Comics);
		Assert.Equal("slug-1", result.Payload.Comics[0].Slug);
	}

	/// <summary>
	/// Verifies search payloads with null top-level entries map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenSearchItemIsNullAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[null]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies search payloads tolerate null alias entries in ranking-only alias hints.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnSuccess_WhenSearchAliasItemIsNullAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "slug-1", "title": "Title", "md_titles": [null] }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload!.Comics);
	}

	/// <summary>
	/// Verifies search payloads tolerate schema drift for non-matching cover fields.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnSuccess_WhenSearchCoverItemIsNullAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "slug-1", "title": "Title", "md_titles": [ { "title": "Alias" } ], "md_covers": [null] }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload!.Comics);
	}

	/// <summary>
	/// Verifies search payloads tolerate one object-shaped <c>md_titles</c> value.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnSuccess_WhenSearchMdTitlesIsObjectAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "slug-1", "title": "Title", "md_titles": { "title": "Alias", "lang": "en" } }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		IReadOnlyList<ComickTitleAlias> aliases = Assert.IsAssignableFrom<IReadOnlyList<ComickTitleAlias>>(result.Payload!.Comics[0].MdTitles);
		Assert.Single(aliases);
		Assert.Equal("Alias", aliases[0].Title);
		Assert.Equal("en", aliases[0].Language);
	}

	/// <summary>
	/// Verifies search payloads tolerate one string-shaped <c>md_titles</c> value.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnSuccess_WhenSearchMdTitlesIsStringAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "slug-1", "title": "Title", "md_titles": "Alias One" }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		IReadOnlyList<ComickTitleAlias> aliases = Assert.IsAssignableFrom<IReadOnlyList<ComickTitleAlias>>(result.Payload!.Comics[0].MdTitles);
		Assert.Single(aliases);
		Assert.Equal("Alias One", aliases[0].Title);
	}

	/// <summary>
	/// Verifies search payloads tolerate mixed-shape <c>md_titles</c> arrays by filtering invalid entries.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnSuccess_WhenSearchMdTitlesArrayIsMixedShapeAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "slug-1", "title": "Title", "md_titles": [null, { "title": "Alias One" }, 123, "Alias Two", { "lang": "en" }, { "title": "   " }] }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		IReadOnlyList<ComickTitleAlias> aliases = Assert.IsAssignableFrom<IReadOnlyList<ComickTitleAlias>>(result.Payload!.Comics[0].MdTitles);
		Assert.Equal(2, aliases.Count);
		Assert.Equal("Alias One", aliases[0].Title);
		Assert.Equal("Alias Two", aliases[1].Title);
	}

	/// <summary>
	/// Verifies search payloads tolerate non-critical translation-completed token shapes.
	/// </summary>
	/// <param name="translationCompletedToken">JSON token used for translation_completed.</param>
	[Theory]
	[InlineData("null")]
	[InlineData("\"false\"")]
	[InlineData("0")]
	[InlineData("{}")]
	[InlineData("[]")]
	public async Task SearchAsync_Edge_ShouldReturnSuccess_WhenTranslationCompletedShapeVariesAsync(string translationCompletedToken)
	{
		RecordingHttpMessageHandler handler = new(
			_ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "slug-1", "title": "Title", "translation_completed": """ +
					translationCompletedToken +
					""" }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload!.Comics);
	}

	/// <summary>
	/// Verifies search payloads still fail when match-critical slug/title fields are missing.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenMatchFieldsMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "slug": "", "title": "   " }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies payloads still fail when the comic node is missing.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldReturnMalformedPayload_WhenComicNodeMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""{"authors":[],"artists":[]}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies payloads still fail when all match-critical title fields are missing.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldReturnMalformedPayload_WhenNoUsableTitleFieldsExistAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""{"comic":{"title":"   ","md_titles":[{"title":"   "},null,{"lang":"en"}]}}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies malformed non-match fields do not fail the payload when a primary comic title exists.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldReturnSuccess_WhenNonMatchFieldShapesAreMalformedAndPrimaryTitleExistsAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""
					{
					  "firstChap": 42,
					  "recommendable": "yes",
					  "demographic": [1,2],
					  "matureContent": {},
					  "checkVol2Chap1": "no",
					  "comic": {
					    "id": "bad",
					    "hid": { "x": 1 },
					    "title": "Title",
					    "country": [ "jp" ],
					    "status": "ongoing",
					    "links": [1,2,3],
					    "last_chapter": {},
					    "chapter_count": [],
					    "demographic": "shounen",
					    "follow_rank": true,
					    "user_follow_count": false,
					    "desc": { "markdown": "bad" },
					    "parsed": 123,
					    "slug": 77,
					    "year": "2020",
					    "bayesian_rating": {},
					    "rating_count": "100",
					    "content_rating": { "safe": true },
					    "statistics": { "unexpected": true },
					    "translation_completed": "false",
					    "chapter_numbers_reset_on_new_volume_manual": "0",
					    "noindex": "1",
					    "adsense": [],
					    "comment_count": "22",
					    "login_required": "true",
					    "has_anime": 0,
					    "anime": "n/a",
					    "reviews": { "raw": true },
					    "recommendations": { "raw": true },
					    "relate_from": 999,
					    "is_english_title": "false",
					    "md_titles": [null,{"title":"  "},"Alt Title",{"title":"Alt Title 2","lang":"en"},5],
					    "md_comic_md_genres": [null,{"md_genres":{"name":"Action"}},{"md_genres":"Adventure"},{"md_genres":5}],
					    "md_covers": [null,{"b2key":"cover-a.jpg"},{"b2key":"   "},"cover-b.jpg",5],
					    "mu_comics": {
					      "year": "2001",
					      "mu_comic_categories": [
					        null,
					        {"mu_categories":{"title":"Pirates"},"positive_vote":"10","negative_vote":"1"},
					        {"mu_categories":"Adventure","positive_vote":"2","negative_vote":"0"},
					        {"mu_categories":{"title":"Ignored"},"positive_vote":"x","negative_vote":"y"},
					        5
					      ]
					    },
					    "iso639_1": [ "en" ],
					    "lang_name": 7,
					    "lang_native": { "name": "bad" }
					  },
					  "authors": [null,{"name":"Author A"},{"name":5},"Author B",7],
					  "artists": [null,{"name":"Artist A"},{"name":8},"Artist B",{}],
					  "langList": {"en":true}
					}
					"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload?.Comic);
		Assert.Equal("Title", result.Payload!.Comic!.Title);
		Assert.Equal(2, result.Payload.Comic.MdTitles!.Count);
		Assert.Equal(2, result.Payload.Comic.MdCovers!.Count);
		Assert.Equal("cover-a.jpg", result.Payload.Comic.MdCovers![0].B2Key);
		Assert.Equal("cover-b.jpg", result.Payload.Comic.MdCovers[1].B2Key);
		Assert.Equal(2, result.Payload.Comic.GenreMappings!.Count);
		Assert.NotNull(result.Payload.Comic.MuComics?.MuComicCategories);
		Assert.Equal(3, result.Payload.Comic.MuComics!.MuComicCategories!.Count);
		Assert.Equal(2, result.Payload.Authors.Count);
		Assert.Equal(2, result.Payload.Artists.Count);
	}

	/// <summary>
	/// Verifies malformed primary-title token still succeeds when aliases provide a usable title.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldReturnSuccess_WhenPrimaryTitleShapeIsMalformedButAliasTitleIsUsableAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""{"comic":{"title":{"bad":true},"md_titles":[null,{"title":"Alias One"}]}}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload?.Comic?.MdTitles);
		Assert.Single(result.Payload!.Comic!.MdTitles!);
		Assert.Equal("Alias One", result.Payload.Comic.MdTitles[0].Title);
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is a numeric JSON number token.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Expected_ShouldParsePayload_WhenLinksMangaBuddyIsNumberAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": 1489 }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.Number, mangaBuddyValue.ValueKind);
		Assert.Equal(1489L, mangaBuddyValue.GetInt64());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is a numeric string token.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyIsNumericStringAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": \"1489\" }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.String, mangaBuddyValue.ValueKind);
		Assert.Equal("1489", mangaBuddyValue.GetString());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is an empty string token.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyIsEmptyStringAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": \"\" }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.String, mangaBuddyValue.ValueKind);
		Assert.Equal(string.Empty, mangaBuddyValue.GetString());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is whitespace-only.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyIsWhitespaceStringAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": \"   \" }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.String, mangaBuddyValue.ValueKind);
		Assert.Equal("   ", mangaBuddyValue.GetString());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is a valid Int64-range numeric string token.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyIsLongMaxNumericStringAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": \"9223372036854775807\" }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.String, mangaBuddyValue.ValueKind);
		Assert.Equal("9223372036854775807", mangaBuddyValue.GetString());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is a non-numeric string token.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyIsNonNumericStringAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": \"not-a-number\" }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.String, mangaBuddyValue.ValueKind);
		Assert.Equal("not-a-number", mangaBuddyValue.GetString());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is a numeric string outside Int64 range.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyOverflowsInt64Async()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": \"9223372036854775808\" }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.String, mangaBuddyValue.ValueKind);
		Assert.Equal("9223372036854775808", mangaBuddyValue.GetString());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is a JSON object token.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyIsObjectTokenAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": { \"id\": 1489 } }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.Object, mangaBuddyValue.ValueKind);
		Assert.Equal(1489, mangaBuddyValue.GetProperty("id").GetInt32());
	}

	/// <summary>
	/// Verifies comic payloads parse dynamic links when links.mb is a JSON array token.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenLinksMangaBuddyIsArrayTokenAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"links\": { \"al\": \"100\" }", "\"links\": { \"al\": \"100\", \"mb\": [1489] }", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		JsonElement mangaBuddyValue = AssertLinkEntry(result, "mb");
		Assert.Equal(JsonValueKind.Array, mangaBuddyValue.ValueKind);
		JsonElement.ArrayEnumerator arrayEnumerator = mangaBuddyValue.EnumerateArray();
		Assert.True(arrayEnumerator.MoveNext());
		Assert.Equal(1489, arrayEnumerator.Current.GetInt32());
	}

	/// <summary>
	/// Asserts one parsed comic payload contains the specified dynamic link key and returns the value.
	/// </summary>
	/// <param name="result">Direct API result.</param>
	/// <param name="key">Link key to lookup.</param>
	/// <returns>Parsed link value element.</returns>
	private static JsonElement AssertLinkEntry(ComickDirectApiResult<ComickComicResponse> result, string key)
	{
		ArgumentNullException.ThrowIfNull(result);
		ArgumentException.ThrowIfNullOrWhiteSpace(key);

		ComickComicLinks links = Assert.IsType<ComickComicLinks>(result.Payload?.Comic?.Links);
		Assert.True(links.TryGetEntry(key, out JsonElement value));
		return value;
	}
}
