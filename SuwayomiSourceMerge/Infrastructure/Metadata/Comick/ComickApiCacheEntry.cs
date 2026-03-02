using System.Text.Json;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents one persisted Comick API response cache entry.
/// </summary>
internal sealed class ComickApiCacheEntry
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ComickApiCacheEntry"/> class.
	/// </summary>
	/// <param name="endpointKind">Endpoint kind represented by this cache entry.</param>
	/// <param name="requestKey">Trimmed request key for lookup matching.</param>
	/// <param name="outcome">Cached Comick outcome classification.</param>
	/// <param name="statusCode">Optional integer HTTP status code.</param>
	/// <param name="diagnostic">Optional diagnostic string.</param>
	/// <param name="payloadJson">Optional cached payload JSON.</param>
	/// <param name="expiresAtUtc">Cache entry expiry timestamp.</param>
	public ComickApiCacheEntry(
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		ComickDirectApiOutcome outcome,
		int? statusCode,
		string? diagnostic,
		JsonElement? payloadJson,
		DateTimeOffset expiresAtUtc)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
		if (statusCode is < 100 or > 599)
		{
			throw new ArgumentOutOfRangeException(
				nameof(statusCode),
				statusCode,
				"Status code must be null or between 100 and 599.");
		}

		EndpointKind = endpointKind;
		RequestKey = requestKey.Trim();
		Outcome = outcome;
		StatusCode = statusCode;
		Diagnostic = string.IsNullOrWhiteSpace(diagnostic)
			? null
			: diagnostic.Trim();
		PayloadJson = payloadJson?.Clone();
		ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
	}

	/// <summary>
	/// Gets the endpoint kind represented by this cache entry.
	/// </summary>
	public ComickApiCacheEndpointKind EndpointKind
	{
		get;
	}

	/// <summary>
	/// Gets the trimmed request key used for cache matching.
	/// </summary>
	public string RequestKey
	{
		get;
	}

	/// <summary>
	/// Gets the cached outcome classification.
	/// </summary>
	public ComickDirectApiOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets the optional integer HTTP status code.
	/// </summary>
	public int? StatusCode
	{
		get;
	}

	/// <summary>
	/// Gets the optional diagnostic text.
	/// </summary>
	public string? Diagnostic
	{
		get;
	}

	/// <summary>
	/// Gets the optional payload JSON.
	/// </summary>
	public JsonElement? PayloadJson
	{
		get;
	}

	/// <summary>
	/// Gets the cache-entry expiry timestamp in UTC.
	/// </summary>
	public DateTimeOffset ExpiresAtUtc
	{
		get;
	}
}
