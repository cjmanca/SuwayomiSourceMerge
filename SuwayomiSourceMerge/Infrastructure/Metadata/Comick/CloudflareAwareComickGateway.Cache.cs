using System.Net;
using System.Text.Json;

namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Response-cache helpers for <see cref="CloudflareAwareComickGateway"/>.
/// </summary>
internal sealed partial class CloudflareAwareComickGateway
{
	/// <summary>
	/// Executes one request with cache read-through and write-through behavior.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="endpointKind">Comick endpoint kind.</param>
	/// <param name="requestKey">Trimmed request key.</param>
	/// <param name="endpointUri">Absolute endpoint URI.</param>
	/// <param name="payloadParser">Typed payload parser used by live routing paths.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <param name="lookupMode">Lookup mode controlling cache-only versus cache-then-live behavior.</param>
	/// <returns>Comick result from cache or live routing.</returns>
	private async Task<ComickDirectApiResult<TPayload>> ExecuteWithCacheAsync<TPayload>(
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		Uri endpointUri,
		Func<string, (bool Success, TPayload? Payload, string Diagnostic)> payloadParser,
		CancellationToken cancellationToken,
		ComickLookupMode lookupMode)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentNullException.ThrowIfNull(payloadParser);

		DateTimeOffset nowUtc = _utcNowProvider().ToUniversalTime();
		if (TryReadCachedResult(
			endpointKind,
			requestKey,
			nowUtc,
			endpointUri,
			out ComickDirectApiResult<TPayload>? cachedResult,
			out string cacheReadDetail))
		{
			LogCacheHit(endpointUri, endpointKind, requestKey, cachedResult!.Outcome, cacheReadDetail);
			return cachedResult;
		}

		LogCacheMiss(endpointUri, endpointKind, requestKey, cacheReadDetail);
		if (lookupMode == ComickLookupMode.CacheOnly)
		{
			return new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.NotFound,
				payload: default,
				statusCode: HttpStatusCode.NotFound,
				diagnostic: ComickLookupDiagnostics.CacheOnlyMiss,
				isCacheOnlyMiss: true);
		}

		ComickDirectApiResult<TPayload> liveResult = await ExecuteWithRoutingAsync(
			endpointUri,
			payloadParser,
			cancellationToken).ConfigureAwait(false);
		TryPersistCacheEntry(endpointKind, requestKey, endpointUri, liveResult);
		return liveResult;
	}

	/// <summary>
	/// Attempts to read one valid unexpired cache entry and map it to a typed Comick result.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="endpointKind">Comick endpoint kind.</param>
	/// <param name="requestKey">Trimmed request key.</param>
	/// <param name="nowUtc">Current UTC timestamp.</param>
	/// <param name="endpointUri">Absolute endpoint URI.</param>
	/// <param name="cachedResult">Mapped cached result when successful.</param>
	/// <param name="cacheReadDetail">Cache read detail token for diagnostics.</param>
	/// <returns><see langword="true"/> when a valid cache result is returned; otherwise <see langword="false"/>.</returns>
	private bool TryReadCachedResult<TPayload>(
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		DateTimeOffset nowUtc,
		Uri endpointUri,
		out ComickDirectApiResult<TPayload>? cachedResult,
		out string cacheReadDetail)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
		ArgumentNullException.ThrowIfNull(endpointUri);

		cachedResult = null;
		MetadataStateSnapshot snapshot = TryReadMetadataStateSnapshot(
			endpointUri,
			operation: "cache_read",
			operationKind: MetadataStateStoreOperationKind.Cache);
		ComickApiCacheEntry? cacheEntry = snapshot.ComickCache
			.Where(
				entry => entry.EndpointKind == endpointKind &&
					string.Equals(entry.RequestKey, requestKey, StringComparison.Ordinal))
			.OrderByDescending(static entry => entry.ExpiresAtUtc)
			.FirstOrDefault();
		if (cacheEntry is null)
		{
			cacheReadDetail = "not_found";
			return false;
		}

		// TTL boundary is inclusive: an entry is stale at its exact expiry timestamp.
		if (cacheEntry.ExpiresAtUtc <= nowUtc)
		{
			cacheReadDetail = "expired";
			return false;
		}

		if (cacheEntry.Outcome == ComickDirectApiOutcome.NotFound)
		{
			// A cached NotFound outcome remains semantically NotFound even if persisted status code data is malformed.
			// Fall back to 404 to preserve stable behavior for cached NotFound entries.
			cachedResult = new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.NotFound,
				payload: default,
				statusCode: cacheEntry.StatusCode is int notFoundStatusCode
					? TryConvertToHttpStatusCode(notFoundStatusCode) ?? HttpStatusCode.NotFound
					: HttpStatusCode.NotFound,
				diagnostic: cacheEntry.Diagnostic ?? "Cached not-found result.");
			cacheReadDetail = "not_found_hit";
			return true;
		}

		if (cacheEntry.Outcome != ComickDirectApiOutcome.Success)
		{
			cacheReadDetail = "unsupported_outcome";
			return false;
		}

		if (cacheEntry.PayloadJson is not JsonElement payloadJson)
		{
			cacheReadDetail = "missing_payload";
			return false;
		}

		try
		{
			TPayload? payload = payloadJson.Deserialize<TPayload>();
			if (payload is null)
			{
				cacheReadDetail = "payload_null";
				return false;
			}

			HttpStatusCode? cachedStatusCode = cacheEntry.StatusCode is int successStatusCode
				? TryConvertToHttpStatusCode(successStatusCode)
				: HttpStatusCode.OK;
			if (cachedStatusCode is null)
			{
				cacheReadDetail = "invalid_status_code";
				return false;
			}

			cachedResult = new ComickDirectApiResult<TPayload>(
				ComickDirectApiOutcome.Success,
				payload,
				cachedStatusCode,
				cacheEntry.Diagnostic ?? "Cached success result.");
			cacheReadDetail = "success_hit";
			return true;
		}
		catch (Exception exception) when (exception is JsonException or NotSupportedException)
		{
			cacheReadDetail = "payload_deserialize_failed";
			return false;
		}
	}

	/// <summary>
	/// Attempts to persist a cache entry for eligible outcomes.
	/// </summary>
	/// <typeparam name="TPayload">Typed payload type.</typeparam>
	/// <param name="endpointKind">Comick endpoint kind.</param>
	/// <param name="requestKey">Trimmed request key.</param>
	/// <param name="endpointUri">Absolute endpoint URI.</param>
	/// <param name="result">Result to evaluate for cache persistence.</param>
	private void TryPersistCacheEntry<TPayload>(
		ComickApiCacheEndpointKind endpointKind,
		string requestKey,
		Uri endpointUri,
		ComickDirectApiResult<TPayload> result)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(requestKey);
		ArgumentNullException.ThrowIfNull(endpointUri);
		ArgumentNullException.ThrowIfNull(result);

		if (!IsCacheableOutcome(result.Outcome))
		{
			LogCacheSkipped(endpointUri, endpointKind, requestKey, result.Outcome, "outcome_not_cacheable");
			return;
		}

		JsonElement? payloadJson = null;
		if (result.Outcome == ComickDirectApiOutcome.Success)
		{
			if (result.Payload is null)
			{
				LogCacheSkipped(endpointUri, endpointKind, requestKey, result.Outcome, "missing_success_payload");
				return;
			}

			try
			{
				payloadJson = JsonSerializer.SerializeToElement(result.Payload);
			}
			catch (Exception exception) when (!IsFatalException(exception))
			{
				LogCacheSkipped(endpointUri, endpointKind, requestKey, result.Outcome, "payload_serialize_failed");
				return;
			}
		}

		DateTimeOffset persistedAtUtc = _utcNowProvider().ToUniversalTime();
		ComickApiCacheEntry entry = new(
			endpointKind,
			requestKey,
			result.Outcome,
			result.StatusCode is HttpStatusCode statusCode ? (int)statusCode : null,
			result.Diagnostic,
			payloadJson,
			persistedAtUtc + _options.MetadataApiCacheTtl);

		bool persisted = TryTransformMetadataStateSnapshot(
			endpointUri,
			operation: "cache_persist_transform",
			operationKind: MetadataStateStoreOperationKind.Cache,
			current =>
			{
				List<ComickApiCacheEntry> retainedEntries = [];
				foreach (ComickApiCacheEntry existingEntry in current.ComickCache)
				{
					// Use the same inclusive expiry boundary used by cache reads.
					if (existingEntry.ExpiresAtUtc <= persistedAtUtc)
					{
						continue;
					}

					if (existingEntry.EndpointKind == endpointKind &&
						string.Equals(existingEntry.RequestKey, requestKey, StringComparison.Ordinal))
					{
						continue;
					}

					retainedEntries.Add(existingEntry);
				}

				retainedEntries.Add(entry);
				return new MetadataStateSnapshot(
					current.TitleCooldownsUtc,
					current.StickyFlaresolverrUntilUtc,
					retainedEntries);
			});
		if (persisted)
		{
			LogCachePersisted(endpointUri, endpointKind, requestKey, entry.Outcome, entry.ExpiresAtUtc);
		}
	}

	/// <summary>
	/// Determines whether one Comick outcome should be persisted in the response cache.
	/// </summary>
	/// <param name="outcome">Comick outcome.</param>
	/// <returns><see langword="true"/> when cacheable; otherwise <see langword="false"/>.</returns>
	private static bool IsCacheableOutcome(ComickDirectApiOutcome outcome)
	{
		// Cache only stable outcomes so transient/error paths continue probing live APIs.
		return outcome == ComickDirectApiOutcome.Success || outcome == ComickDirectApiOutcome.NotFound;
	}
}
