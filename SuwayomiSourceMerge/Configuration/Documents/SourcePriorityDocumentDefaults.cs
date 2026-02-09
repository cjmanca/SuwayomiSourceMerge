namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Produces default source priority configuration values.
/// </summary>
public static class SourcePriorityDocumentDefaults
{
    /// <summary>
    /// Creates an empty source priority list.
    /// </summary>
    /// <returns>A document with no explicit source ordering.</returns>
    public static SourcePriorityDocument Create()
    {
        return new SourcePriorityDocument
        {
            Sources = []
        };
    }
}
