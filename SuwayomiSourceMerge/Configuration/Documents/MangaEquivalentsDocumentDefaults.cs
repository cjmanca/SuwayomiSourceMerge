namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default values for manga equivalence mappings.
/// </summary>
/// <remarks>
/// The default document intentionally starts empty. Runtime matching then falls back to normalization-
/// based grouping until users provide explicit canonical mappings.
/// </remarks>
public static class MangaEquivalentsDocumentDefaults
{
	/// <summary>
	/// Creates a default equivalence document.
	/// </summary>
	/// <returns>A document with an initialized, empty <see cref="MangaEquivalentsDocument.Groups"/> list.</returns>
	public static MangaEquivalentsDocument Create()
	{
		return new MangaEquivalentsDocument
		{
			Groups = []
		};
	}
}
