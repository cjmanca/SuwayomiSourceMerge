using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves canonical manga titles from configured equivalence mappings.
/// </summary>
/// <remarks>
/// Implementations consume <see cref="MangaEquivalentsDocument"/> data and expose lookup operations
/// for runtime grouping flows.
/// </remarks>
internal interface IMangaEquivalenceService
{
	/// <summary>
	/// Attempts to resolve a canonical title for the provided input title.
	/// </summary>
	/// <param name="inputTitle">Input title to resolve.</param>
	/// <param name="canonicalTitle">Resolved canonical title when found; otherwise an empty string.</param>
	/// <returns><see langword="true"/> when a canonical mapping exists; otherwise <see langword="false"/>.</returns>
	bool TryResolveCanonicalTitle(string inputTitle, out string canonicalTitle);

	/// <summary>
	/// Resolves a canonical title or returns the original input when no mapping exists.
	/// </summary>
	/// <param name="inputTitle">Input title to resolve.</param>
	/// <returns>The mapped canonical title when found; otherwise <paramref name="inputTitle"/> unchanged.</returns>
	string ResolveCanonicalOrInput(string inputTitle);
}
