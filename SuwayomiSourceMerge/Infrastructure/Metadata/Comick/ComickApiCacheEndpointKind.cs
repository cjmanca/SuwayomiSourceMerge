namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Identifies the Comick endpoint shape associated with one cached API response.
/// </summary>
internal enum ComickApiCacheEndpointKind
{
	/// <summary>
	/// Cached result for the <c>/v1.0/search/</c> endpoint.
	/// </summary>
	Search = 0,

	/// <summary>
	/// Cached result for the <c>/comic/{slug}/</c> endpoint.
	/// </summary>
	Comic = 1
}
