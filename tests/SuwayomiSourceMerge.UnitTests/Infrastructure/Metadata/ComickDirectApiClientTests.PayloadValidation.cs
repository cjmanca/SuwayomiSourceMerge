namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
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
}
