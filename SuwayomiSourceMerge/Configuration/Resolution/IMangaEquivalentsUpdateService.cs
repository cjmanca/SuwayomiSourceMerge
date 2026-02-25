using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Applies deterministic, validated updates to <c>manga_equivalents.yml</c>.
/// </summary>
/// <remarks>
/// Implementations are responsible for conflict detection, scene-tag-aware validation, and atomic persistence.
/// </remarks>
internal interface IMangaEquivalentsUpdateService
{
	/// <summary>
	/// Updates one manga-equivalents document using incoming title metadata.
	/// </summary>
	/// <param name="request">Update request payload.</param>
	/// <returns>Deterministic update result describing mutation and persistence outcomes.</returns>
	MangaEquivalentsUpdateResult Update(MangaEquivalentsUpdateRequest request);
}
