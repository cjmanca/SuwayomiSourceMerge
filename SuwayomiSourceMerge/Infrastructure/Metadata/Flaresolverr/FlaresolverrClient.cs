using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Flaresolverr;

/// <summary>
/// Executes FlareSolverr wrapper requests against the <c>/v1</c> endpoint.
/// </summary>
internal sealed class FlaresolverrClient : IFlaresolverrClient, IDisposable
{
	/// <summary>
	/// Relative path for wrapper execution requests.
	/// </summary>
	private const string ExecutePath = "v1";

	/// <summary>
	/// Root status value indicating a successful wrapper execution.
	/// </summary>
	private const string WrapperOkStatus = "ok";

	/// <summary>
	/// Shared HTTP client dependency.
	/// </summary>
	private readonly HttpClient _httpClient;

	/// <summary>
	/// Resolved client options.
	/// </summary>
	private readonly FlaresolverrClientOptions _options;

	/// <summary>
	/// Indicates whether this instance owns and must dispose the HTTP client.
	/// </summary>
	private readonly bool _ownsHttpClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="FlaresolverrClient"/> class.
	/// </summary>
	/// <param name="options">FlareSolverr client options.</param>
	public FlaresolverrClient(FlaresolverrClientOptions options)
		: this(options, httpClient: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FlaresolverrClient"/> class.
	/// </summary>
	/// <param name="options">FlareSolverr client options.</param>
	/// <param name="httpClient">
	/// Optional HTTP client.
	/// When <see langword="null"/>, one internal client is created and disposed by this instance.
	/// When provided, caller owns its lifetime and disposal.
	/// This client may issue concurrent requests through the injected instance and does not provide additional synchronization.
	/// </param>
	internal FlaresolverrClient(
		FlaresolverrClientOptions options,
		HttpClient? httpClient)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		if (httpClient is null)
		{
			_httpClient = new HttpClient
			{
				Timeout = _options.RequestTimeout
			};
			_ownsHttpClient = true;
		}
		else
		{
			_httpClient = httpClient;
			_ownsHttpClient = false;
		}
	}

	/// <inheritdoc />
	public async Task<FlaresolverrApiResult> PostV1Async(
		string requestPayloadJson,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requestPayloadJson);

		Uri requestUri = new(_options.BaseUri, ExecutePath);
		using HttpRequestMessage request = new(HttpMethod.Post, requestUri);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		request.Content = new StringContent(requestPayloadJson, Encoding.UTF8, "application/json");

		try
		{
			using HttpResponseMessage response = await _httpClient
				.SendAsync(
					request,
					HttpCompletionOption.ResponseHeadersRead,
					cancellationToken)
				.ConfigureAwait(false);
			string responseBody = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
			if (!response.IsSuccessStatusCode)
			{
				return new FlaresolverrApiResult(
					FlaresolverrApiOutcome.HttpFailure,
					response.StatusCode,
					upstreamStatusCode: null,
					upstreamResponseBody: null,
					diagnostic: $"HTTP failure status code: {(int)response.StatusCode}.");
			}

			return TryParseWrapperPayload(
				responseBody,
				out int upstreamStatusCode,
				out string? upstreamResponseBody,
				out string diagnostic)
				? new FlaresolverrApiResult(
					FlaresolverrApiOutcome.Success,
					response.StatusCode,
					upstreamStatusCode,
					upstreamResponseBody,
					diagnostic: "Success.")
				: new FlaresolverrApiResult(
					FlaresolverrApiOutcome.MalformedPayload,
					response.StatusCode,
					upstreamStatusCode: null,
					upstreamResponseBody: null,
					diagnostic);
		}
		catch (HttpRequestException exception)
		{
			return new FlaresolverrApiResult(
				FlaresolverrApiOutcome.TransportFailure,
				statusCode: null,
				upstreamStatusCode: null,
				upstreamResponseBody: null,
				diagnostic: $"Transport failure: {exception.Message}");
		}
		catch (IOException exception)
		{
			return new FlaresolverrApiResult(
				FlaresolverrApiOutcome.TransportFailure,
				statusCode: null,
				upstreamStatusCode: null,
				upstreamResponseBody: null,
				diagnostic: $"Transport I/O failure: {exception.Message}");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new FlaresolverrApiResult(
				FlaresolverrApiOutcome.Cancelled,
				statusCode: null,
				upstreamStatusCode: null,
				upstreamResponseBody: null,
				diagnostic: "Request canceled by caller.");
		}
		catch (OperationCanceledException exception)
		{
			return new FlaresolverrApiResult(
				FlaresolverrApiOutcome.TransportFailure,
				statusCode: null,
				upstreamStatusCode: null,
				upstreamResponseBody: null,
				diagnostic: $"Transport timeout/cancellation failure: {exception.Message}");
		}
	}

	/// <summary>
	/// Reads response body text.
	/// </summary>
	/// <param name="response">HTTP response.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Response text, or empty when content is absent.</returns>
	private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(response);
		if (response.Content is null)
		{
			return string.Empty;
		}

		return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Parses and validates wrapper payload text.
	/// </summary>
	/// <param name="responseBody">Raw response body text.</param>
	/// <param name="upstreamStatusCode">Extracted upstream status code when successful.</param>
	/// <param name="upstreamResponseBody">Extracted upstream response body when successful.</param>
	/// <param name="diagnostic">Validation diagnostic when invalid.</param>
	/// <returns><see langword="true"/> when payload is valid; otherwise <see langword="false"/>.</returns>
	private static bool TryParseWrapperPayload(
		string responseBody,
		out int upstreamStatusCode,
		out string? upstreamResponseBody,
		out string diagnostic)
	{
		upstreamStatusCode = default;
		upstreamResponseBody = null;

		try
		{
			using JsonDocument jsonDocument = JsonDocument.Parse(responseBody);
			JsonElement root = jsonDocument.RootElement;
			if (root.ValueKind != JsonValueKind.Object)
			{
				diagnostic = "Malformed payload: root node must be an object.";
				return false;
			}

			if (!root.TryGetProperty("status", out JsonElement statusElement) ||
				statusElement.ValueKind != JsonValueKind.String)
			{
				diagnostic = "Malformed payload: root status field is missing or not a string.";
				return false;
			}

			string? statusText = statusElement.GetString();
			if (!string.Equals(statusText, WrapperOkStatus, StringComparison.OrdinalIgnoreCase))
			{
				diagnostic = "Malformed payload: root status must be 'ok'.";
				return false;
			}

			if (!root.TryGetProperty("solution", out JsonElement solutionElement) ||
				solutionElement.ValueKind != JsonValueKind.Object)
			{
				diagnostic = "Malformed payload: solution node is missing or not an object.";
				return false;
			}

			if (!solutionElement.TryGetProperty("status", out JsonElement solutionStatusElement) ||
				solutionStatusElement.ValueKind != JsonValueKind.Number ||
				!solutionStatusElement.TryGetInt32(out upstreamStatusCode))
			{
				diagnostic = "Malformed payload: solution.status is missing or not an integer.";
				return false;
			}

			if (!solutionElement.TryGetProperty("response", out JsonElement solutionResponseElement) ||
				solutionResponseElement.ValueKind != JsonValueKind.String)
			{
				diagnostic = "Malformed payload: solution.response is missing or not a string.";
				return false;
			}

			string? responseText = solutionResponseElement.GetString();
			if (responseText is null)
			{
				diagnostic = "Malformed payload: solution.response string value was null.";
				return false;
			}

			upstreamResponseBody = responseText;
			diagnostic = "Success.";
			return true;
		}
		catch (JsonException exception)
		{
			diagnostic = $"Malformed payload: JSON parse failure ({exception.Message}).";
			return false;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_ownsHttpClient)
		{
			_httpClient.Dispose();
		}
	}
}
