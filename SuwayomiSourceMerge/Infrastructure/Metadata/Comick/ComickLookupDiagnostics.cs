namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Shared diagnostic tokens used by Comick lookup-mode flows.
/// </summary>
internal static class ComickLookupDiagnostics
{
	/// <summary>
	/// Diagnostic emitted when cache-only lookup mode cannot resolve a required cache entry.
	/// </summary>
	public const string CacheOnlyMiss = "Cache miss with cache-only lookup mode.";
}
