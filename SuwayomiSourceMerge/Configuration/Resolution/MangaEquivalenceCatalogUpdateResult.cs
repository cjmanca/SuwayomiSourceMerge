namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Represents one deterministic runtime manga-equivalence catalog update result.
/// </summary>
internal sealed class MangaEquivalenceCatalogUpdateResult
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalenceCatalogUpdateResult"/> class.
	/// </summary>
	/// <param name="outcome">Catalog update outcome classification.</param>
	/// <param name="updateResult">Underlying manga-equivalents updater result.</param>
	/// <param name="diagnostic">Optional deterministic diagnostic for catalog-level failure mapping.</param>
	public MangaEquivalenceCatalogUpdateResult(
		MangaEquivalenceCatalogUpdateOutcome outcome,
		MangaEquivalentsUpdateResult updateResult,
		string? diagnostic)
	{
		ArgumentNullException.ThrowIfNull(updateResult);

		Outcome = outcome;
		UpdateResult = updateResult;
		Diagnostic = diagnostic;
	}

	/// <summary>
	/// Gets catalog update outcome classification.
	/// </summary>
	public MangaEquivalenceCatalogUpdateOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets underlying manga-equivalents updater result.
	/// </summary>
	public MangaEquivalentsUpdateResult UpdateResult
	{
		get;
	}

	/// <summary>
	/// Gets optional deterministic catalog-level diagnostic text.
	/// </summary>
	public string? Diagnostic
	{
		get;
	}
}
