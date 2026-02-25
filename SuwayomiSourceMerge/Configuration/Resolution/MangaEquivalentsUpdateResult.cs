namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Represents one deterministic result from a manga-equivalents update operation.
/// </summary>
internal sealed class MangaEquivalentsUpdateResult
{
	/// <summary>
	/// Sentinel value used when no specific group index is affected.
	/// </summary>
	public const int NoAffectedGroupIndex = -1;

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentsUpdateResult"/> class.
	/// </summary>
	/// <param name="outcome">Outcome classification.</param>
	/// <param name="mangaEquivalentsYamlPath">Path for the updated target document.</param>
	/// <param name="affectedGroupIndex">Affected group index or <see cref="NoAffectedGroupIndex"/>.</param>
	/// <param name="addedAliasCount">Number of aliases added to the updated/created group.</param>
	/// <param name="diagnostic">Optional deterministic diagnostic text.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="mangaEquivalentsYamlPath"/> is null, empty, or whitespace.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="addedAliasCount"/> is negative.</exception>
	public MangaEquivalentsUpdateResult(
		MangaEquivalentsUpdateOutcome outcome,
		string mangaEquivalentsYamlPath,
		int affectedGroupIndex,
		int addedAliasCount,
		string? diagnostic)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaEquivalentsYamlPath);
		if (addedAliasCount < 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(addedAliasCount),
				addedAliasCount,
				"Added alias count must be >= 0.");
		}

		Outcome = outcome;
		MangaEquivalentsYamlPath = mangaEquivalentsYamlPath;
		AffectedGroupIndex = affectedGroupIndex;
		AddedAliasCount = addedAliasCount;
		Diagnostic = diagnostic;
	}

	/// <summary>
	/// Gets outcome classification.
	/// </summary>
	public MangaEquivalentsUpdateOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets target <c>manga_equivalents.yml</c> path.
	/// </summary>
	public string MangaEquivalentsYamlPath
	{
		get;
	}

	/// <summary>
	/// Gets affected group index or <see cref="NoAffectedGroupIndex"/>.
	/// </summary>
	public int AffectedGroupIndex
	{
		get;
	}

	/// <summary>
	/// Gets number of aliases added to the affected group.
	/// </summary>
	public int AddedAliasCount
	{
		get;
	}

	/// <summary>
	/// Gets optional deterministic diagnostic text.
	/// </summary>
	public string? Diagnostic
	{
		get;
	}
}
