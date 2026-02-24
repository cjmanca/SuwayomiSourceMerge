namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.IO;
using System.Net;
using System.Text;
using SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Provides shared fixtures and payload builders for FlareSolverr client tests.
/// </summary>
public sealed partial class FlaresolverrClientTests
{
	/// <summary>
	/// Creates one client instance with deterministic options and provided HTTP client.
	/// </summary>
	/// <param name="httpClient">HTTP client to use.</param>
	/// <returns>Constructed FlareSolverr client.</returns>
	private static FlaresolverrClient CreateClient(HttpClient httpClient)
	{
		return new FlaresolverrClient(
			new FlaresolverrClientOptions(
				new Uri("https://flaresolverr.example.local/"),
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
	/// Creates one minimal valid FlareSolverr wrapper payload used by tests.
	/// </summary>
	/// <returns>JSON payload.</returns>
	private static string CreateSuccessfulWrapperJson()
	{
		return
			"""
			{
			  "status": "ok",
			  "solution": {
			    "status": 200,
			    "response": "{\"ok\":true}"
			  }
			}
			""";
	}

	/// <summary>
	/// Minimal recording HTTP message handler used by FlareSolverr client tests.
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

		/// <inheritdoc />
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);
			cancellationToken.ThrowIfCancellationRequested();
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
