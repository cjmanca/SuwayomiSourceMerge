namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Verifies direct Comick API cancellation and transport-failure classification behavior.
/// </summary>
public sealed partial class ComickDirectApiClientTests
{
	/// <summary>
	/// Verifies transport exceptions map to <see cref="ComickDirectApiOutcome.TransportFailure"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnTransportFailure_WhenSendThrowsHttpRequestExceptionAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ => throw new HttpRequestException("network failed"));
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.TransportFailure, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies caller cancellation maps to <see cref="ComickDirectApiOutcome.Cancelled"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnCancelled_WhenCallerTokenCanceledAsync()
	{
		RecordingHttpMessageHandler handler = new(
			static _ =>
			{
				return CreateResponse(
					HttpStatusCode.OK,
					CreateSearchJson());
			});
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);
		using CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece", cancellationTokenSource.Token);

		Assert.Equal(ComickDirectApiOutcome.Cancelled, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies read-phase cancellation maps to <see cref="ComickDirectApiOutcome.Cancelled"/> when caller token is canceled.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Edge_ShouldReturnCancelled_WhenCallerTokenCanceledDuringResponseReadAsync()
	{
		using CancellationTokenSource cancellationTokenSource = new();
		RecordingHttpMessageHandler handler = new(
			_ =>
			{
				cancellationTokenSource.Cancel();
				return CreateResponse(
					HttpStatusCode.OK,
					CreateSearchJson());
			});
		using HttpClient httpClient = new(handler);
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece", cancellationTokenSource.Token);

		Assert.Equal(ComickDirectApiOutcome.Cancelled, result.Outcome);
		Assert.Null(result.Payload);
	}

	/// <summary>
	/// Verifies non-cooperative read-phase cancellation maps to <see cref="ComickDirectApiOutcome.TransportFailure"/>.
	/// </summary>
	[Fact]
	public async Task SearchAsync_Failure_ShouldReturnTransportFailure_WhenResponseReadThrowsOperationCanceledExceptionWithoutCallerCancellationAsync()
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
		ComickDirectApiClient client = CreateClient(httpClient);

		ComickDirectApiResult<ComickSearchResponse> result = await client.SearchAsync("one piece");

		Assert.Equal(ComickDirectApiOutcome.TransportFailure, result.Outcome);
		Assert.Null(result.Payload);
	}
}
