namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Represents the result of ensuring details.json metadata for one canonical title.
/// </summary>
internal sealed class OverrideDetailsResult
{

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideDetailsResult"/> class.
	/// </summary>
	/// <param name="outcome">Terminal ensure outcome.</param>
	/// <param name="detailsJsonPath">Absolute path to the details.json target or discovered override file.</param>
	/// <param name="detailsJsonExists">Whether details.json exists on disk after the ensure operation completes.</param>
	/// <param name="sourceDetailsJsonPath">Absolute source details.json path used for seeding, when applicable.</param>
	/// <param name="comicInfoXmlPath">Absolute ComicInfo.xml path used for generation, when applicable.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="detailsJsonPath"/> is null, empty, or whitespace.
	/// </exception>
	public OverrideDetailsResult(
		OverrideDetailsOutcome outcome,
		string detailsJsonPath,
		bool detailsJsonExists,
		string? sourceDetailsJsonPath,
		string? comicInfoXmlPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(detailsJsonPath);

		Outcome = outcome;
		DetailsJsonPath = detailsJsonPath;
		DetailsJsonExists = detailsJsonExists;
		SourceDetailsJsonPath = sourceDetailsJsonPath;
		ComicInfoXmlPath = comicInfoXmlPath;
	}

	/// <summary>
	/// Gets the terminal ensure outcome.
	/// </summary>
	public OverrideDetailsOutcome Outcome
	{
		get;
	}

	/// <summary>
	/// Gets the absolute path to the details.json target or discovered existing file.
	/// </summary>
	public string DetailsJsonPath
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether details.json exists on disk after this ensure operation.
	/// </summary>
	public bool DetailsJsonExists
	{
		get;
	}

	/// <summary>
	/// Gets the absolute source details.json path used for seeding, when applicable.
	/// </summary>
	public string? SourceDetailsJsonPath
	{
		get;
	}

	/// <summary>
	/// Gets the absolute ComicInfo.xml path used for generation, when applicable.
	/// </summary>
	public string? ComicInfoXmlPath
	{
		get;
	}
}
