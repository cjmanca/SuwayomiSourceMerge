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
}
