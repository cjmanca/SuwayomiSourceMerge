using System.IO;
using System.Net;
using System.Net.Http.Headers;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Executes direct Comick API requests for search and comic-detail endpoints.
/// </summary>
internal sealed class ComickDirectApiClient : IComickDirectApiClient, IDisposable
{
	/// <summary>
	/// Shared HTTP client dependency.
	/// </summary>
	private readonly HttpClient _httpClient;

	/// <summary>
	/// Resolved client options.
	/// </summary>
	private readonly ComickDirectApiClientOptions _options;

	/// <summary>
	/// Indicates whether this instance owns and must dispose the HTTP client.
	/// </summary>
	private readonly bool _ownsHttpClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiClient"/> class using default options.
	/// </summary>
	public ComickDirectApiClient()
		: this(
			new ComickDirectApiClientOptions(),
			httpClient: null)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ComickDirectApiClient"/> class.
	/// </summary>
	/// <param name="options">Direct client options.</param>
	/// <param name="httpClient">
	/// Optional HTTP client.
	/// When <see langword="null"/>, one internal client is created and disposed by this instance.
	/// </param>
	internal ComickDirectApiClient(
		ComickDirectApiClientOptions options,
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
	public Task<ComickDirectApiResult<ComickSearchResponse>> SearchAsync(
		string query,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(query);

		Uri requestUri = ComickEndpointUriBuilder.BuildSearchUri(_options.BaseUri, query);
		return ExecuteGetAsync(
			requestUri,
			ComickPayloadParser.TryParseSearchPayload,
			cancellationToken);
	}

	/// <inheritdoc />
	public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
		string slug,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);

		Uri requestUri = ComickEndpointUriBuilder.BuildComicUri(_options.BaseUri, slug);
		return ExecuteGetAsync(
			requestUri,
			ComickPayloadParser.TryParseComicPayload,
			cancellationToken);
	}

	/// <summary>
	/// Executes one GET request and maps result outcomes using endpoint-specific payload parsing.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="requestUri">Request URI.</param>
	/// <param name="payloadParser">Payload parser delegate.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Typed request result.</returns>
	private async Task<ComickDirectApiResult<TPayload>> ExecuteGetAsync<TPayload>(
		Uri requestUri,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser,
		CancellationToken cancellationToken)
	{
		using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

		try
		{
			using HttpResponseMessage response = await _httpClient
				.SendAsync(
					request,
					HttpCompletionOption.ResponseHeadersRead,
					cancellationToken)
				.ConfigureAwait(false);
			string responseBody = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
			HttpStatusCode statusCode = response.StatusCode;
			if (statusCode == HttpStatusCode.NotFound)
			{
				return new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.NotFound,
					payload: default,
					statusCode,
					diagnostic: "Resource not found.");
			}

			if (ComickPayloadParser.IsCloudflareCandidateStatus(statusCode) &&
				ComickPayloadParser.IsCloudflareChallenge(response, responseBody))
			{
				return new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.CloudflareBlocked,
					payload: default,
					statusCode,
					diagnostic: "Cloudflare challenge detected.");
			}

			if (!response.IsSuccessStatusCode)
			{
				return new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.HttpFailure,
					payload: default,
					statusCode,
					diagnostic: $"HTTP failure status code: {(int)statusCode}.");
			}

			(bool parseSuccess, TPayload? payload, string diagnostic) = payloadParser(responseBody);
			return parseSuccess
				? new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.Success,
					payload,
					statusCode,
					"Success.")
				: new ComickDirectApiResult<TPayload>(
					ComickDirectApiOutcome.MalformedPayload,
					payload: default,
					statusCode,
					diagnostic);
		}
		catch (HttpRequestException exception)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.TransportFailure,
				payload: default,
				statusCode: null,
				diagnostic: $"Transport failure: {exception.Message}");
		}
		catch (IOException exception)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.TransportFailure,
				payload: default,
				statusCode: null,
				diagnostic: $"Transport I/O failure: {exception.Message}");
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.Cancelled,
				payload: default,
				statusCode: null,
				diagnostic: "Request canceled by caller.");
		}
		catch (OperationCanceledException exception)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.TransportFailure,
				payload: default,
				statusCode: null,
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

	/// <inheritdoc />
	public void Dispose()
	{
		if (_ownsHttpClient)
		{
			_httpClient.Dispose();
		}
	}
}
