namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies direct Comick API success and HTTP-status outcome behavior.
/// </summary>
public sealed partial class ComickDirectApiClientTests
{
	/// <summary>
	/// Verifies search success parses typed payload and sends URL-encoded query with JSON accept header.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Expected_ShouldParsePayloadAndSendEncodedQueryAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					CreateSearchJson()));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece/a+b?");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Single(result.Payload!.Comics);
		Assert.Equal("hid-1", result.Payload.Comics[0].Hid);
		Assert.NotNull(handler.LastRequest);
		Assert.Equal(
			"https://api.comick.dev/v1.0/search/?q=one%20piece%2Fa%2Bb%3F",
			handler.LastRequest!.RequestUri!.AbsoluteUri);
		Assert.Contains(
			handler.LastRequest.Headers.Accept,
			static header => string.Equals(header.MediaType, "application/json", StringComparison.OrdinalIgnoreCase));
	}

	/// <summary>
	/// Verifies comic success parses typed payload.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Expected_ShouldParsePayloadAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(
					HttpStatusCode.OK,
					CreateComicJson()));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload);
		Assert.Equal("slug-1", result.Payload!.Comic!.Slug);
		Assert.Equal("日本語 (にほんご／にっぽんご)", result.Payload.Comic.LanguageNative);
		Assert.NotNull(result.Payload.Comic.MdTitles);
		Assert.NotNull(result.Payload.Comic.Recommendations);
		Assert.Single(result.Payload.Comic.MdTitles!);
		Assert.Single(result.Payload.Comic.Recommendations!);
		Assert.NotNull(result.Payload.Comic.MuComics?.MuComicCategories);
		Assert.Equal(2, result.Payload.Comic.MuComics!.MuComicCategories!.Count);
		Assert.Equal("Pirate/s", result.Payload.Comic.MuComics.MuComicCategories[0].Category?.Title);
		Assert.Equal(10, result.Payload.Comic.MuComics.MuComicCategories[0].PositiveVote);
		Assert.Equal(1, result.Payload.Comic.MuComics.MuComicCategories[0].NegativeVote);
	}

	/// <summary>
	/// Verifies null MU vote fields are tolerated and parsed without failing the full comic payload.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldParsePayload_WhenMuCategoryVoteFieldsAreNullAsync()
	{
		string responseBody = CreateComicJson()
			.Replace("\"positive_vote\": 10", "\"positive_vote\": null", StringComparison.Ordinal)
			.Replace("\"negative_vote\": 5", "\"negative_vote\": null", StringComparison.Ordinal);
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, responseBody));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("slug-1");

		Assert.Equal(ComickDirectApiOutcome.Success, result.Outcome);
		Assert.NotNull(result.Payload?.Comic?.MuComics?.MuComicCategories);
		Assert.Equal(2, result.Payload!.Comic!.MuComics!.MuComicCategories!.Count);
		Assert.Null(result.Payload.Comic.MuComics.MuComicCategories[0].PositiveVote);
		Assert.Null(result.Payload.Comic.MuComics.MuComicCategories[1].NegativeVote);
	}

	/// <summary>
	/// Verifies 404 responses map to <see cref="ComickDirectApiOutcome.NotFound"/>.
	/// </summary>
	[Fact]
	public async Task GetComicAsync_Edge_ShouldReturnNotFound_WhenStatusIs404Async()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(HttpStatusCode.NotFound, """{"message":"Not Found"}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickComicResponse> result = await client.GetComicAsync("missing-slug");

		Assert.Equal(ComickDirectApiOutcome.NotFound, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
	}

	/// <summary>
	/// Verifies challenge responses map to <see cref="ComickDirectApiOutcome.CloudflareBlocked"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnCloudflareBlocked_WhenChallengeMarkersPresentAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
			{
				HttpResponseMessage response = CreateResponse(
					HttpStatusCode.Forbidden,
					"<html><title>Just a moment...</title><script>window._cf_chl_opt={};</script></html>");
				response.Headers.TryAddWithoutValidation("cf-mitigated", "challenge");
				return response;
			});
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.CloudflareBlocked, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
	}

	/// <summary>
	/// Verifies non-cloudflare non-success responses map to <see cref="ComickDirectApiOutcome.HttpFailure"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnHttpFailure_WhenStatusIsNonSuccessWithoutChallengeAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
				CreateResponse(HttpStatusCode.BadGateway, """{"message":"gateway"}"""));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.HttpFailure, result.Outcome);
		Assert.Null(result.Payload);
		Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
	}
}
