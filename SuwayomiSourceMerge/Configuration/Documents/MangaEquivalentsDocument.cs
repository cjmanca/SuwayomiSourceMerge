namespace SuwayomiSourceMerge.Configuration.Documents;

/// <summary>
/// Represents the full contents of <c>manga_equivalents.yml</c>.
/// </summary>
/// <remarks>
/// Each group binds one canonical display title to zero or more alternate aliases that should be treated
/// as the same manga during grouping and mount generation.
/// </remarks>
public sealed class MangaEquivalentsDocument
{
	/// <summary>
	/// Gets or sets configured canonical/alias groups.
	/// </summary>
	/// <remarks>
	/// Keep this list deterministic in order when editing by hand because bootstrap and validation
	/// diagnostics refer to index-based paths.
	/// </summary>
	public List<MangaEquivalentGroup>? Groups
	{
		get; init;
	}
}

/// <summary>
/// Represents one canonical manga title and its alias values.
/// </summary>
public sealed class MangaEquivalentGroup
{
	/// <summary>
	/// Gets or sets the canonical display title used for merged output naming.
	/// </summary>
	public string? Canonical
	{
		get; init;
	}

	/// <summary>
	/// Gets or sets alternate title spellings that map to <see cref="Canonical"/>.
	/// </summary>
	/// <remarks>
	/// Aliases are compared using normalization rules, so punctuation and casing differences may collapse
	/// to the same key during validation.
	/// </summary>
	public List<string>? Aliases
	{
		get; init;
	}
}
