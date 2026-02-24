namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Verifies FlareSolverr client success and HTTP-status outcome behavior.
/// </summary>
public sealed partial class FlaresolverrClientTests
{
	/// <summary>
	/// Verifies valid wrapper payload parsing and expected POST request details.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Expected_ShouldParseWrapperAndSendPostRequestAsync()
	{
		const string payload = """{"cmd":"request.get","url":"https://api.comick.dev/v1.0/search/?q=test"}""";
		string? capturedContentType = null;
		string? capturedContentTypeCharset = null;
		string? capturedRequestBody = null;
		HttpMethod? capturedRequestMethod = null;
		Uri? capturedRequestUri = null;
		List<string> capturedAcceptMediaTypes = [];
		RecordingHttpMessageHandler handler = new(
			request =>
			{
				capturedRequestMethod = request.Method;
				capturedRequestUri = request.RequestUri;
				capturedContentType = request.Content?.Headers.ContentType?.MediaType;
				capturedContentTypeCharset = request.Content?.Headers.ContentType?.CharSet;
				capturedRequestBody = request.Content is null
					? null
					: request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				capturedAcceptMediaTypes = request.Headers.Accept
					.Select(static header => header.MediaType ?? string.Empty)
					.ToList();
				return CreateResponse(
				HttpStatusCode.OK,
				CreateSuccessfulWrapperJson());
			});
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async(payload);

		Assert.Equal(FlaresolverrApiOutcome.Success, result.Outcome);
		Assert.Equal(HttpStatusCode.OK, result.StatusCode);
		Assert.Equal(200, result.UpstreamStatusCode);
		Assert.Equal("{\"ok\":true}", result.UpstreamResponseBody);
		Assert.Equal(HttpMethod.Post, capturedRequestMethod);
		Assert.NotNull(capturedRequestUri);
		Assert.Equal("https://flaresolverr.example.local/v1", capturedRequestUri!.AbsoluteUri);
		Assert.Contains(
			capturedAcceptMediaTypes,
			static mediaType => string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase));
		Assert.Equal("application/json", capturedContentType);
		Assert.Equal("utf-8", capturedContentTypeCharset);
		Assert.Equal(payload, capturedRequestBody);
	}

	/// <summary>
	/// Verifies wrapper status matching is case-insensitive for <c>ok</c>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Edge_ShouldAcceptWrapperStatusRegardlessOfCaseAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => CreateResponse(
				HttpStatusCode.OK,
				"""
				{
				  "status": "OK",
				  "solution": {
				    "status": 201,
				    "response": "created"
				  }
				}
				"""));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.Success, result.Outcome);
		Assert.Equal(201, result.UpstreamStatusCode);
		Assert.Equal("created", result.UpstreamResponseBody);
	}

	/// <summary>
	/// Verifies non-success HTTP responses map to <see cref="FlaresolverrApiOutcome.HttpFailure"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnHttpFailure_WhenStatusIsNonSuccessAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => CreateResponse(HttpStatusCode.BadGateway, """{"status":"error"}"""));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.HttpFailure, result.Outcome);
		Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
		Assert.Null(result.UpstreamStatusCode);
		Assert.Null(result.UpstreamResponseBody);
	}
}
