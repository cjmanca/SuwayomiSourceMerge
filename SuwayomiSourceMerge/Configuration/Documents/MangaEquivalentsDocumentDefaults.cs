namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default values for manga equivalence mappings.
/// </summary>
public static class MangaEquivalentsDocumentDefaults
{
    /// <summary>
    /// Creates an empty equivalence document.
    /// </summary>
    /// <returns>An empty document with no groups.</returns>
    public static MangaEquivalentsDocument Create()
    {
        return new MangaEquivalentsDocument
        {
            Groups = []
        };
    }
}
