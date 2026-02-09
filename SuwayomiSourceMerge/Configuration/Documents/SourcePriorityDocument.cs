namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Represents ordered source priority configuration.
/// </summary>
public sealed class SourcePriorityDocument
{
    /// <summary>
    /// Gets or sets source names in priority order (top to bottom).
    /// </summary>
    public List<string>? Sources
    {
        get; init;
    }
}
