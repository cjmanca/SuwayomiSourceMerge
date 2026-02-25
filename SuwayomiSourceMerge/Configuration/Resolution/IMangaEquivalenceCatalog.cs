namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Defines mutable runtime manga-equivalence catalog behavior used by metadata-driven update flows.
/// </summary>
/// <remarks>
/// Implementations expose canonical-title lookup behavior and deterministic in-process catalog refresh
/// operations after <c>manga_equivalents.yml</c> updates.
/// </remarks>
internal interface IMangaEquivalenceCatalog : IMangaEquivalenceService
{
	/// <summary>
	/// Applies one deterministic manga-equivalents update and refreshes the runtime resolver when applicable.
	/// </summary>
	/// <param name="request">Update request payload.</param>
	/// <returns>Catalog update outcome with underlying updater diagnostics.</returns>
	MangaEquivalenceCatalogUpdateResult Update(MangaEquivalentsUpdateRequest request);
}
