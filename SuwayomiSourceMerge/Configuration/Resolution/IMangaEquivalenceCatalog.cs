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
	/// Attempts to resolve the full equivalent-title set for the provided input title.
	/// </summary>
	/// <param name="inputTitle">Input title used to resolve one equivalence group.</param>
	/// <param name="equivalentTitles">
	/// Resolved equivalent titles in deterministic group order (canonical first, then aliases) when found;
	/// otherwise an empty list.
	/// </param>
	/// <returns><see langword="true"/> when the input maps to one equivalence group; otherwise <see langword="false"/>.</returns>
	bool TryGetEquivalentTitles(string inputTitle, out IReadOnlyList<string> equivalentTitles);

	/// <summary>
	/// Applies one deterministic manga-equivalents update and refreshes the runtime resolver when applicable.
	/// </summary>
	/// <param name="request">Update request payload.</param>
	/// <returns>Catalog update outcome with underlying updater diagnostics.</returns>
	MangaEquivalenceCatalogUpdateResult Update(MangaEquivalentsUpdateRequest request);
}
