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
	/// Verifies search payloads missing required nested list nodes map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenRequiredListNodeMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "id": 1, "hid": "hid-1", "slug": "slug-1", "title": "Title", "statistics": [], "md_covers": [ { "w": 1, "h": 1, "b2key": "cover.jpg" } ] }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
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
	/// Verifies search payloads with null alias entries map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenSearchAliasItemIsNullAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "id": 1, "hid": "hid-1", "slug": "slug-1", "title": "Title", "statistics": [], "md_titles": [null], "md_covers": [ { "w": 1, "h": 1, "b2key": "cover.jpg" } ] }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies search payloads with null cover entries map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnMalformedPayload_WhenSearchCoverItemIsNullAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""[{ "id": 1, "hid": "hid-1", "slug": "slug-1", "title": "Title", "statistics": [], "md_titles": [ { "title": "Alias" } ], "md_covers": [null] }]"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies structurally invalid payloads map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldReturnMalformedPayload_WhenRequiredFieldsMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""{"comic":{"id":1,"hid":"","title":"Title","slug":"","links":{},"statistics":[],"recommendations":[],"relate_from":[],"md_titles":[],"md_covers":[],"md_comic_md_genres":[]}}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies comic payloads missing required nested list nodes map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldReturnMalformedPayload_WhenRequiredListNodeMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""{"comic":{"id":1,"hid":"hid-1","title":"Title","slug":"slug-1","links":{},"statistics":[],"recommendations":[],"relate_from":[],"md_covers":[{"w":1,"h":1,"b2key":"cover.jpg"}],"md_comic_md_genres":[]}}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies comic payloads with null alias entries map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldReturnMalformedPayload_WhenComicAliasItemIsNullAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""{"comic":{"id":1,"hid":"hid-1","title":"Title","slug":"slug-1","links":{},"statistics":[],"recommendations":[],"relate_from":[],"md_titles":[null],"md_covers":[{"w":1,"h":1,"b2key":"cover.jpg"}],"md_comic_md_genres":[]}}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies comic payloads with null cover entries map to <see cref="ComickDirectApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Failure_ShouldReturnMalformedPayload_WhenComicCoverItemIsNullAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					"""{"comic":{"id":1,"hid":"hid-1","title":"Title","slug":"slug-1","links":{},"statistics":[],"recommendations":[],"relate_from":[],"md_titles":[{"title":"Alias"}],"md_covers":[null],"md_comic_md_genres":[]}}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.Payload);
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
