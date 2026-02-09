namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Represents scene tag values used for title normalization.
/// </summary>
public sealed class SceneTagsDocument
{
    /// <summary>
    /// Gets or sets configured scene tags.
    /// </summary>
    public List<string>? Tags
    {
        get; init;
    }
}
