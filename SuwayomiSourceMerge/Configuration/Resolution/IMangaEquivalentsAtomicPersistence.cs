using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Persists <see cref="MangaEquivalentsDocument"/> updates atomically with deterministic diagnostics.
/// </summary>
internal interface IMangaEquivalentsAtomicPersistence
{
	/// <summary>
	/// Writes one updated manga-equivalents document to disk atomically.
	/// </summary>
	/// <param name="targetPath">Target <c>manga_equivalents.yml</c> path.</param>
	/// <param name="document">Document to persist.</param>
	/// <param name="diagnostic">Deterministic write diagnostic on failure.</param>
	/// <returns><see langword="true"/> when persistence succeeds; otherwise <see langword="false"/>.</returns>
	bool TryPersistDocumentAtomically(string targetPath, MangaEquivalentsDocument document, out string? diagnostic);
}
