namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Controls whether Comick gateway lookups may execute live outbound requests after cache evaluation.
/// </summary>
internal enum ComickLookupMode
{
	/// <summary>
	/// Read from cache first, then execute live requests on cache miss.
	/// </summary>
	CacheThenLive = 0,

	/// <summary>
	/// Read from cache only and return deterministic miss outcomes when cache entries are unavailable.
	/// </summary>
	CacheOnly = 1
}
