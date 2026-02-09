namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Represents canonical manga title equivalence mappings.
/// </summary>
public sealed class MangaEquivalentsDocument
{
    /// <summary>
    /// Gets or sets equivalence groups.
    /// </summary>
    public List<MangaEquivalentGroup>? Groups
    {
        get; init;
    }
}

/// <summary>
/// Represents one canonical title and aliases.
/// </summary>
public sealed class MangaEquivalentGroup
{
    /// <summary>
    /// Gets or sets canonical display title.
    /// </summary>
    public string? Canonical
    {
        get; init;
    }

    /// <summary>
    /// Gets or sets aliases for canonical title.
    /// </summary>
    public List<string>? Aliases
    {
        get; init;
    }
}
