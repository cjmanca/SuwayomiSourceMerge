namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Verifies FlareSolverr client malformed-payload classification behavior.
/// </summary>
public sealed partial class FlaresolverrClientTests
{
	/// <summary>
	/// Verifies malformed JSON maps to <see cref="FlaresolverrApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnMalformedPayload_WhenJsonInvalidAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => CreateResponse(HttpStatusCode.OK, "{"));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.MalformedPayload, result.Outcome);
		Assert.Null(result.UpstreamStatusCode);
		Assert.Null(result.UpstreamResponseBody);
	}

	/// <summary>
	/// Verifies payloads missing root status map to <see cref="FlaresolverrApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnMalformedPayload_WhenRootStatusMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => CreateResponse(
				HttpStatusCode.OK,
				"""
				{
				  "solution": {
				    "status": 200,
				    "response": "ok"
				  }
				}
				"""));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.MalformedPayload, result.Outcome);
	}

	/// <summary>
	/// Verifies payloads with non-ok status map to <see cref="FlaresolverrApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnMalformedPayload_WhenRootStatusIsNotOkAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => CreateResponse(
				HttpStatusCode.OK,
				"""
				{
				  "status": "error",
				  "solution": {
				    "status": 200,
				    "response": "ok"
				  }
				}
				"""));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.MalformedPayload, result.Outcome);
	}

	/// <summary>
	/// Verifies payloads missing solution node map to <see cref="FlaresolverrApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnMalformedPayload_WhenSolutionMissingAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => CreateResponse(
				HttpStatusCode.OK,
				"""
				{
				  "status": "ok"
				}
				"""));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.MalformedPayload, result.Outcome);
	}

	/// <summary>
	/// Verifies payloads with missing or non-integer solution.status map to <see cref="FlaresolverrApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Theory]
	[InlineData("""{"status":"ok","solution":{"response":"ok"}}""")]
	[InlineData("""{"status":"ok","solution":{"status":"200","response":"ok"}}""")]
	public async Task PostV1Async_Failure_ShouldReturnMalformedPayload_WhenSolutionStatusInvalidAsync(string wrapperJson)
	{
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, wrapperJson));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.MalformedPayload, result.Outcome);
	}

	/// <summary>
	/// Verifies payloads with missing or non-string solution.response map to <see cref="FlaresolverrApiOutcome.MalformedPayload"/>.
	/// </summary>
	[Theory]
	[InlineData("""{"status":"ok","solution":{"status":200}}""")]
	[InlineData("""{"status":"ok","solution":{"status":200,"response":null}}""")]
	[InlineData("""{"status":"ok","solution":{"status":200,"response":1}}""")]
	public async Task PostV1Async_Failure_ShouldReturnMalformedPayload_WhenSolutionResponseInvalidAsync(string wrapperJson)
	{
		RecordingHttpMessageHandler handler = new(
			_ => CreateResponse(HttpStatusCode.OK, wrapperJson));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.MalformedPayload, result.Outcome);
	}
}
