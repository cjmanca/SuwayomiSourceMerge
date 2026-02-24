namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Verifies FlareSolverr client cancellation and transport-failure classification behavior.
/// </summary>
public sealed partial class FlaresolverrClientTests
{
	/// <summary>
	/// Verifies transport exceptions map to <see cref="FlaresolverrApiOutcome.TransportFailure"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnTransportFailure_WhenSendThrowsHttpRequestExceptionAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => throw new HttpRequestException("network failed"));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.TransportFailure, result.Outcome);
		Assert.Null(result.StatusCode);
		Assert.Null(result.UpstreamStatusCode);
		Assert.Null(result.UpstreamResponseBody);
	}

	/// <summary>
	/// Verifies read-phase I/O exceptions map to <see cref="FlaresolverrApiOutcome.TransportFailure"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnTransportFailure_WhenResponseReadThrowsIOExceptionAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
			{
				HttpResponseMessage response = new(HttpStatusCode.OK)
				{
					Content = new ThrowingHttpContent(static () => new IOException("socket reset"))
				};

				return response;
			});
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.TransportFailure, result.Outcome);
		Assert.Null(result.StatusCode);
		Assert.Null(result.UpstreamStatusCode);
		Assert.Null(result.UpstreamResponseBody);
	}

	/// <summary>
	/// Verifies caller cancellation maps to <see cref="FlaresolverrApiOutcome.Cancelled"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Edge_ShouldReturnCancelled_WhenCallerTokenCanceledAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => CreateResponse(HttpStatusCode.OK, CreateSuccessfulWrapperJson()));
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		FlaresolverrApiResult result = await client.PostV1Async(
			"""{"cmd":"request.get"}""",
			cancellationTokenSource.Token);

		Assert.Equal(FlaresolverrApiOutcome.Cancelled, result.Outcome);
		Assert.Null(result.StatusCode);
		Assert.Null(result.UpstreamStatusCode);
		Assert.Null(result.UpstreamResponseBody);
	}

	/// <summary>
	/// Verifies non-cooperative operation-canceled exceptions map to <see cref="FlaresolverrApiOutcome.TransportFailure"/>.
	/// </summary>
	[Fact]
	public async Task PostV1Async_Failure_ShouldReturnTransportFailure_WhenResponseReadThrowsOperationCanceledWithoutCallerCancellationAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
			{
				HttpResponseMessage response = new(HttpStatusCode.OK)
				{
					Content = new ThrowingHttpContent(static () => new OperationCanceledException("timed out"))
				};

				return response;
			});
		using HttpClient httpClient = new(handler);
		FlaresolverrClient client = CreateClient(httpClient);

		FlaresolverrApiResult result = await client.PostV1Async("""{"cmd":"request.get"}""");

		Assert.Equal(FlaresolverrApiOutcome.TransportFailure, result.Outcome);
		Assert.Null(result.StatusCode);
		Assert.Null(result.UpstreamStatusCode);
		Assert.Null(result.UpstreamResponseBody);
	}
}
