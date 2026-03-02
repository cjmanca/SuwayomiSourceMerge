using System.Text.Json;

using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Comick response-cache JSON parse and persistence helpers for <see cref="FileBackedMetadataStateStore"/>.
/// </summary>
internal sealed partial class FileBackedMetadataStateStore
{
	/// <summary>
	/// JSON property name for persisted Comick API cache entries.
	/// </summary>
	private const string ComickApiCachePropertyName = "comick_api_cache";

	/// <summary>
	/// Cache-entry JSON property name for endpoint kind.
	/// </summary>
	private const string ComickApiCacheEndpointKindPropertyName = "endpoint_kind";

	/// <summary>
	/// Cache-entry JSON property name for request key.
	/// </summary>
	private const string ComickApiCacheRequestKeyPropertyName = "request_key";

	/// <summary>
	/// Cache-entry JSON property name for request outcome.
	/// </summary>
	private const string ComickApiCacheOutcomePropertyName = "outcome";

	/// <summary>
	/// Cache-entry JSON property name for optional status code.
	/// </summary>
	private const string ComickApiCacheStatusCodePropertyName = "status_code";

	/// <summary>
	/// Cache-entry JSON property name for optional diagnostic text.
	/// </summary>
	private const string ComickApiCacheDiagnosticPropertyName = "diagnostic";

	/// <summary>
	/// Cache-entry JSON property name for optional payload JSON.
	/// </summary>
	private const string ComickApiCachePayloadJsonPropertyName = "payload_json";

	/// <summary>
	/// Cache-entry JSON property name for expiry timestamp.
	/// </summary>
	private const string ComickApiCacheExpiresAtPropertyName = "expires_at_unix_seconds";

	/// <summary>
	/// Reads the optional Comick API cache section from persisted metadata state.
	/// </summary>
	/// <param name="root">JSON root element.</param>
	/// <returns>Parsed cache entries; malformed entries are skipped.</returns>
	private static IReadOnlyCollection<ComickApiCacheEntry> ReadOptionalComickApiCache(JsonElement root)
	{
		if (!root.TryGetProperty(ComickApiCachePropertyName, out JsonElement cacheElement))
		{
			return Array.Empty<ComickApiCacheEntry>();
		}

		if (cacheElement.ValueKind != JsonValueKind.Array)
		{
			return Array.Empty<ComickApiCacheEntry>();
		}

		List<ComickApiCacheEntry> entries = [];
		foreach (JsonElement entryElement in cacheElement.EnumerateArray())
		{
			if (TryParseComickApiCacheEntry(entryElement, out ComickApiCacheEntry? entry) && entry is not null)
			{
				entries.Add(entry);
			}
		}

		return entries;
	}

	/// <summary>
	/// Attempts to parse one Comick cache entry object.
	/// </summary>
	/// <param name="entryElement">JSON entry element.</param>
	/// <param name="entry">Parsed entry when successful.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryParseComickApiCacheEntry(JsonElement entryElement, out ComickApiCacheEntry? entry)
	{
		entry = null;
		if (entryElement.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		if (!entryElement.TryGetProperty(ComickApiCacheEndpointKindPropertyName, out JsonElement endpointKindElement) ||
			endpointKindElement.ValueKind != JsonValueKind.String ||
			!TryParseEndpointKind(endpointKindElement.GetString(), out ComickApiCacheEndpointKind endpointKind))
		{
			return false;
		}

		if (!entryElement.TryGetProperty(ComickApiCacheRequestKeyPropertyName, out JsonElement requestKeyElement) ||
			requestKeyElement.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		string? requestKey = requestKeyElement.GetString();
		if (string.IsNullOrWhiteSpace(requestKey))
		{
			return false;
		}

		if (!entryElement.TryGetProperty(ComickApiCacheOutcomePropertyName, out JsonElement outcomeElement) ||
			outcomeElement.ValueKind != JsonValueKind.String)
		{
			return false;
		}

		string? outcomeText = outcomeElement.GetString();
		if (!Enum.TryParse(outcomeText, ignoreCase: true, out ComickDirectApiOutcome outcome) ||
			!Enum.IsDefined(outcome))
		{
			return false;
		}

		if (!entryElement.TryGetProperty(ComickApiCacheExpiresAtPropertyName, out JsonElement expiresAtElement) ||
			expiresAtElement.ValueKind != JsonValueKind.Number ||
			!expiresAtElement.TryGetInt64(out long expiresAtUnixSeconds))
		{
			return false;
		}

		int? statusCode = null;
		if (entryElement.TryGetProperty(ComickApiCacheStatusCodePropertyName, out JsonElement statusCodeElement))
		{
			if (statusCodeElement.ValueKind == JsonValueKind.Null)
			{
				statusCode = null;
			}
			else if (statusCodeElement.ValueKind == JsonValueKind.Number && statusCodeElement.TryGetInt32(out int parsedStatusCode))
			{
				statusCode = parsedStatusCode;
			}
			else
			{
				return false;
			}
		}

		string? diagnostic = null;
		if (entryElement.TryGetProperty(ComickApiCacheDiagnosticPropertyName, out JsonElement diagnosticElement))
		{
			if (diagnosticElement.ValueKind == JsonValueKind.Null)
			{
				diagnostic = null;
			}
			else if (diagnosticElement.ValueKind == JsonValueKind.String)
			{
				diagnostic = diagnosticElement.GetString();
			}
			else
			{
				return false;
			}
		}

		JsonElement? payloadJson = null;
		if (entryElement.TryGetProperty(ComickApiCachePayloadJsonPropertyName, out JsonElement payloadElement) &&
			payloadElement.ValueKind != JsonValueKind.Null)
		{
			payloadJson = payloadElement.Clone();
		}

		try
		{
			entry = new ComickApiCacheEntry(
				endpointKind,
				requestKey,
				outcome,
				statusCode,
				diagnostic,
				payloadJson,
				DateTimeOffset.FromUnixTimeSeconds(expiresAtUnixSeconds));
			return true;
		}
		catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
		{
			return false;
		}
	}

	/// <summary>
	/// Writes persisted Comick API cache entries to the metadata state document.
	/// </summary>
	/// <param name="writer">JSON writer.</param>
	/// <param name="comickCache">Cache entries to persist.</param>
	private static void WriteComickApiCache(Utf8JsonWriter writer, IReadOnlyCollection<ComickApiCacheEntry> comickCache)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(comickCache);

		writer.WriteStartArray(ComickApiCachePropertyName);
		foreach (ComickApiCacheEntry entry in comickCache
			.OrderBy(static entry => entry.EndpointKind)
			.ThenBy(static entry => entry.RequestKey, StringComparer.Ordinal)
			.ThenBy(static entry => entry.ExpiresAtUtc))
		{
			writer.WriteStartObject();
			writer.WriteString(ComickApiCacheEndpointKindPropertyName, FormatEndpointKind(entry.EndpointKind));
			writer.WriteString(ComickApiCacheRequestKeyPropertyName, entry.RequestKey);
			writer.WriteString(ComickApiCacheOutcomePropertyName, entry.Outcome.ToString());
			if (entry.StatusCode is int statusCode)
			{
				writer.WriteNumber(ComickApiCacheStatusCodePropertyName, statusCode);
			}
			else
			{
				writer.WriteNull(ComickApiCacheStatusCodePropertyName);
			}

			if (entry.Diagnostic is string diagnostic)
			{
				writer.WriteString(ComickApiCacheDiagnosticPropertyName, diagnostic);
			}
			else
			{
				writer.WriteNull(ComickApiCacheDiagnosticPropertyName);
			}

			if (entry.PayloadJson is JsonElement payloadJson)
			{
				writer.WritePropertyName(ComickApiCachePayloadJsonPropertyName);
				payloadJson.WriteTo(writer);
			}
			else
			{
				writer.WriteNull(ComickApiCachePayloadJsonPropertyName);
			}

			writer.WriteNumber(
				ComickApiCacheExpiresAtPropertyName,
				entry.ExpiresAtUtc.ToUniversalTime().ToUnixTimeSeconds());
			writer.WriteEndObject();
		}

		writer.WriteEndArray();
	}

	/// <summary>
	/// Parses one persisted endpoint-kind token.
	/// </summary>
	/// <param name="value">Persisted endpoint-kind text.</param>
	/// <param name="endpointKind">Parsed endpoint kind.</param>
	/// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryParseEndpointKind(string? value, out ComickApiCacheEndpointKind endpointKind)
	{
		if (string.Equals(value, "search", StringComparison.OrdinalIgnoreCase))
		{
			endpointKind = ComickApiCacheEndpointKind.Search;
			return true;
		}

		if (string.Equals(value, "comic", StringComparison.OrdinalIgnoreCase))
		{
			endpointKind = ComickApiCacheEndpointKind.Comic;
			return true;
		}

		endpointKind = default;
		return false;
	}

	/// <summary>
	/// Formats one endpoint-kind value for persisted JSON output.
	/// </summary>
	/// <param name="endpointKind">Endpoint kind value.</param>
	/// <returns>Persisted endpoint token.</returns>
	private static string FormatEndpointKind(ComickApiCacheEndpointKind endpointKind)
	{
		return endpointKind switch
		{
			ComickApiCacheEndpointKind.Search => "search",
			ComickApiCacheEndpointKind.Comic => "comic",
			_ => throw new InvalidOperationException(
				$"Unsupported cache endpoint kind '{endpointKind}'.")
		};
	}
}
