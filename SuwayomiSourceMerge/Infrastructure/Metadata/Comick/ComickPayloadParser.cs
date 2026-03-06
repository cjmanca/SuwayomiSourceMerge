using System.Net;
using System.Text.Json;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Parses and validates Comick payloads and challenge-response markers.
/// </summary>
internal static class ComickPayloadParser
{
	/// <summary>
	/// HTTP header token used by Cloudflare challenge responses.
	/// </summary>
	private const string CloudflareMitigatedHeaderName = "cf-mitigated";

	/// <summary>
	/// Challenge marker used in Cloudflare response headers.
	/// </summary>
	private const string ChallengeMarker = "challenge";

	/// <summary>
	/// HTML marker that appears in Cloudflare challenge pages.
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
	/// Strict case-sensitive property matching is intentional to keep payload validation deterministic.
	/// </summary>
	private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
	{
		PropertyNameCaseInsensitive = false
	};

	/// <summary>
	/// Parses and validates one search payload body.
	/// </summary>
	/// <param name="responseBody">Raw response body text.</param>
	/// <returns>Typed parse tuple.</returns>
	public static (bool Success, ComickSearchResponse? Payload, string Diagnostic) TryParseSearchPayload(string responseBody)
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
	/// Parses and validates one comic-detail payload body.
	/// </summary>
	/// <param name="responseBody">Raw response body text.</param>
	/// <returns>Typed parse tuple.</returns>
	public static (bool Success, ComickComicResponse? Payload, string Diagnostic) TryParseComicPayload(string responseBody)
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
	/// Detects Cloudflare challenge responses using HTTP headers and body markers.
	/// </summary>
	/// <param name="response">HTTP response.</param>
	/// <param name="responseBody">Response body text.</param>
	/// <returns><see langword="true"/> when challenge markers are detected; otherwise <see langword="false"/>.</returns>
	public static bool IsCloudflareChallenge(HttpResponseMessage response, string responseBody)
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

		return IsCloudflareChallenge(responseBody);
	}

	/// <summary>
	/// Detects Cloudflare challenge responses using body markers only.
	/// </summary>
	/// <param name="responseBody">Response body text.</param>
	/// <returns><see langword="true"/> when challenge markers are detected; otherwise <see langword="false"/>.</returns>
	public static bool IsCloudflareChallenge(string responseBody)
	{
		if (string.IsNullOrWhiteSpace(responseBody))
		{
			return false;
		}

		return responseBody.Contains(CloudflareChallengeBodyMarker, StringComparison.OrdinalIgnoreCase)
			|| responseBody.Contains(CloudflareChallengeScriptMarker, StringComparison.OrdinalIgnoreCase)
			|| responseBody.Contains(CloudflareChallengeTitleMarker, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Classifies one status code as eligible for Cloudflare challenge interpretation.
	/// </summary>
	/// <param name="statusCode">HTTP status code.</param>
	/// <returns><see langword="true"/> when challenge detection should apply; otherwise <see langword="false"/>.</returns>
	public static bool IsCloudflareCandidateStatus(HttpStatusCode statusCode)
	{
		return statusCode is HttpStatusCode.Forbidden or HttpStatusCode.ServiceUnavailable;
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
			ComickSearchComic? comic = payload.Comics[index];
			if (comic is null)
			{
				diagnostic = $"Malformed payload: search item at index {index} is null.";
				return false;
			}

			if (string.IsNullOrWhiteSpace(comic.Slug) ||
				string.IsNullOrWhiteSpace(comic.Title))
			{
				diagnostic = $"Malformed payload: search item at index {index} is missing required match fields.";
				return false;
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

		bool hasPrimaryTitle = !string.IsNullOrWhiteSpace(payload.Comic.Title);
		bool hasAliasTitle = false;
		if (payload.Comic.MdTitles is not null)
		{
			for (int aliasIndex = 0; aliasIndex < payload.Comic.MdTitles.Count; aliasIndex++)
			{
				ComickTitleAlias? titleAlias = payload.Comic.MdTitles[aliasIndex];
				if (titleAlias is not null && !string.IsNullOrWhiteSpace(titleAlias.Title))
				{
					hasAliasTitle = true;
					break;
				}
			}
		}

		if (!hasPrimaryTitle && !hasAliasTitle)
		{
			diagnostic = "Malformed payload: comic is missing match-critical title fields.";
			return false;
		}

		diagnostic = "Success.";
		return true;
	}
}
