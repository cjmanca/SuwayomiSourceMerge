using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Executes direct Comick API requests for search and comic-detail endpoints.
/// </summary>
internal sealed class ComickDirectApiClient : IComickDirectApiClient, IDisposable
{
	/// <summary>
	/// Relative path for title search requests.
	/// </summary>
	private const string SearchPath = "v1.0/search/";

	/// <summary>
	/// Relative path prefix for comic-detail requests.
	/// </summary>
	private const string ComicPath = "comic/";

	/// <summary>
	/// HTTP header token used by Cloudflare challenge responses.
	/// </summary>
	private const string CloudflareMitigatedHeaderName = "cf-mitigated";

	/// <summary>
	/// Challenge marker used in Cloudflare challenge pages.
	/// </summary>
	private const string ChallengeMarker = "challenge";

	/// <summary>
	/// HTML marker that appears in challenge pages.
	/// </summary>
	private const string CloudflareChallengeBodyMarker = "cf_chl_opt";

	/// <summary>
	/// Challenge-script marker used in Cloudflare pages.
	/// </summary>
	private const string CloudflareChallengeScriptMarker = "/cdn-cgi/challenge-platform";

	/// <summary>
	/// Human-readable Cloudflare challenge marker used in challenge title text.
	/// </summary>
	private const string CloudflareChallengeTitleMarker = "Just a moment";

	/// <summary>
	/// JSON serializer options for typed Comick payload parsing.
	/// </summary>
	private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
	{
		PropertyNameCaseInsensitive = false
	};

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

		Uri requestUri = new(
			_options.BaseUri,
			$"{SearchPath}?q={Uri.EscapeDataString(query.Trim())}");
		return ExecuteGetAsync(
			requestUri,
			TryParseSearchPayload,
			cancellationToken);
	}

	/// <inheritdoc />
	public Task<ComickDirectApiResult<ComickComicResponse>> GetComicAsync(
		string slug,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);

		Uri requestUri = new(
			_options.BaseUri,
			$"{ComicPath}{Uri.EscapeDataString(slug.Trim())}/");
		return ExecuteGetAsync(
			requestUri,
			TryParseComicPayload,
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

			if ((statusCode == HttpStatusCode.Forbidden || statusCode == HttpStatusCode.ServiceUnavailable) &&
				IsCloudflareChallenge(response, responseBody))
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

	/// <summary>
	/// Parses and validates search payload text.
	/// </summary>
	/// <param name="responseBody">Raw response body text.</param>
	/// <returns>Typed parse tuple.</returns>
	private static (bool Success, ComickSearchResponse? Payload, string Diagnostic) TryParseSearchPayload(string responseBody)
	{
		try
		{
			List<ComickSearchComic>? comics = JsonSerializer.Deserialize<List<ComickSearchComic>>(
				responseBody,
				_jsonSerializerOptions);
			if (comics is null)
			{
				return (false, null, "Malformed payload: search root array was null.");
			}

			ComickSearchResponse payload = new(comics);
			return TryValidateSearchPayload(payload, out string diagnostic)
				? (true, payload, "Success.")
				: (false, null, diagnostic);
		}
		catch (JsonException exception)
		{
			return (false, null, $"Malformed payload: JSON parse failure ({exception.Message}).");
		}
	}

	/// <summary>
	/// Parses and validates comic-detail payload text.
	/// </summary>
	/// <param name="responseBody">Raw response body text.</param>
	/// <returns>Typed parse tuple.</returns>
	private static (bool Success, ComickComicResponse? Payload, string Diagnostic) TryParseComicPayload(string responseBody)
	{
		try
		{
			ComickComicResponse? payload = JsonSerializer.Deserialize<ComickComicResponse>(
				responseBody,
				_jsonSerializerOptions);
			if (payload is null)
			{
				return (false, null, "Malformed payload: comic payload root was null.");
			}

			return TryValidateComicPayload(payload, out string diagnostic)
				? (true, payload, "Success.")
				: (false, null, diagnostic);
		}
		catch (JsonException exception)
		{
			return (false, null, $"Malformed payload: JSON parse failure ({exception.Message}).");
		}
	}

	/// <summary>
	/// Validates required search payload fields.
	/// </summary>
	/// <param name="payload">Payload to validate.</param>
	/// <param name="diagnostic">Validation diagnostic when invalid.</param>
	/// <returns><see langword="true"/> when payload is valid; otherwise <see langword="false"/>.</returns>
	private static bool TryValidateSearchPayload(ComickSearchResponse payload, out string diagnostic)
	{
		ArgumentNullException.ThrowIfNull(payload);

		if (payload.Comics is null)
		{
			diagnostic = "Malformed payload: search items collection missing.";
			return false;
		}

		for (int index = 0; index < payload.Comics.Count; index++)
		{
			ComickSearchComic comic = payload.Comics[index];
			if (string.IsNullOrWhiteSpace(comic.Hid) ||
				string.IsNullOrWhiteSpace(comic.Slug) ||
				string.IsNullOrWhiteSpace(comic.Title))
			{
				diagnostic = $"Malformed payload: search item at index {index} is missing required identity fields.";
				return false;
			}

			if (comic.MdTitles is null || comic.MdCovers is null || comic.Statistics is null)
			{
				diagnostic = $"Malformed payload: search item at index {index} is missing required nested collections.";
				return false;
			}

			IReadOnlyList<ComickTitleAlias> titleAliases = comic.MdTitles;
			IReadOnlyList<ComickCover> covers = comic.MdCovers;
			for (int titleIndex = 0; titleIndex < titleAliases.Count; titleIndex++)
			{
				if (string.IsNullOrWhiteSpace(titleAliases[titleIndex].Title))
				{
					diagnostic = $"Malformed payload: search item at index {index} has empty md_titles[{titleIndex}].title.";
					return false;
				}
			}

			for (int coverIndex = 0; coverIndex < covers.Count; coverIndex++)
			{
				if (string.IsNullOrWhiteSpace(covers[coverIndex].B2Key))
				{
					diagnostic = $"Malformed payload: search item at index {index} has empty md_covers[{coverIndex}].b2key.";
					return false;
				}
			}
		}

		diagnostic = "Success.";
		return true;
	}

	/// <summary>
	/// Validates required comic-detail payload fields.
	/// </summary>
	/// <param name="payload">Payload to validate.</param>
	/// <param name="diagnostic">Validation diagnostic when invalid.</param>
	/// <returns><see langword="true"/> when payload is valid; otherwise <see langword="false"/>.</returns>
	private static bool TryValidateComicPayload(ComickComicResponse payload, out string diagnostic)
	{
		ArgumentNullException.ThrowIfNull(payload);

		if (payload.Comic is null)
		{
			diagnostic = "Malformed payload: comic node missing.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(payload.Comic.Hid) ||
			string.IsNullOrWhiteSpace(payload.Comic.Slug) ||
			string.IsNullOrWhiteSpace(payload.Comic.Title))
		{
			diagnostic = "Malformed payload: comic identity fields are missing.";
			return false;
		}

		if (payload.Comic.Links is null ||
			payload.Comic.Statistics is null ||
			payload.Comic.Recommendations is null ||
			payload.Comic.RelateFrom is null ||
			payload.Comic.MdTitles is null ||
			payload.Comic.MdCovers is null ||
			payload.Comic.GenreMappings is null)
		{
			diagnostic = "Malformed payload: comic nested collections are missing.";
			return false;
		}

		IReadOnlyList<ComickTitleAlias> titleAliases = payload.Comic.MdTitles;
		IReadOnlyList<ComickCover> covers = payload.Comic.MdCovers;
		for (int aliasIndex = 0; aliasIndex < titleAliases.Count; aliasIndex++)
		{
			if (string.IsNullOrWhiteSpace(titleAliases[aliasIndex].Title))
			{
				diagnostic = $"Malformed payload: comic md_titles[{aliasIndex}].title is empty.";
				return false;
			}
		}

		for (int coverIndex = 0; coverIndex < covers.Count; coverIndex++)
		{
			if (string.IsNullOrWhiteSpace(covers[coverIndex].B2Key))
			{
				diagnostic = $"Malformed payload: comic md_covers[{coverIndex}].b2key is empty.";
				return false;
			}
		}

		diagnostic = "Success.";
		return true;
	}

	/// <summary>
	/// Detects Cloudflare challenge responses from headers or body markers.
	/// </summary>
	/// <param name="response">HTTP response.</param>
	/// <param name="responseBody">Response body text.</param>
	/// <returns><see langword="true"/> when response matches challenge markers; otherwise <see langword="false"/>.</returns>
	private static bool IsCloudflareChallenge(HttpResponseMessage response, string responseBody)
	{
		ArgumentNullException.ThrowIfNull(response);

		if (response.Headers.TryGetValues(CloudflareMitigatedHeaderName, out IEnumerable<string>? values))
		{
			foreach (string value in values)
			{
				if (value.Contains(ChallengeMarker, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
		}

		if (string.IsNullOrWhiteSpace(responseBody))
		{
			return false;
		}

		return responseBody.Contains(CloudflareChallengeBodyMarker, StringComparison.OrdinalIgnoreCase)
			|| responseBody.Contains(CloudflareChallengeScriptMarker, StringComparison.OrdinalIgnoreCase)
			|| responseBody.Contains(CloudflareChallengeTitleMarker, StringComparison.OrdinalIgnoreCase);
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
