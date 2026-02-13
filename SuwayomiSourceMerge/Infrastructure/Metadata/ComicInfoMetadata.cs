namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Represents metadata extracted from ComicInfo.xml for details.json generation.
/// </summary>
internal sealed class ComicInfoMetadata
{

	/// <summary>
	/// Initializes a new instance of the <see cref="ComicInfoMetadata"/> class.
	/// </summary>
	/// <param name="series">Series value from ComicInfo.xml.</param>
	/// <param name="writer">Writer value from ComicInfo.xml.</param>
	/// <param name="penciller">Penciller value from ComicInfo.xml.</param>
	/// <param name="summary">Summary value from ComicInfo.xml.</param>
	/// <param name="genre">Genre value from ComicInfo.xml.</param>
	/// <param name="status">Status value from ComicInfo.xml or PublishingStatusTachiyomi fallback.</param>
	/// <exception cref="ArgumentNullException">Thrown when any string argument is <see langword="null"/>.</exception>
	public ComicInfoMetadata(
		string series,
		string writer,
		string penciller,
		string summary,
		string genre,
		string status)
	{
		ArgumentNullException.ThrowIfNull(series);
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(penciller);
		ArgumentNullException.ThrowIfNull(summary);
		ArgumentNullException.ThrowIfNull(genre);
		ArgumentNullException.ThrowIfNull(status);

		Series = series;
		Writer = writer;
		Penciller = penciller;
		Summary = summary;
		Genre = genre;
		Status = status;
	}

	/// <summary>
	/// Gets the series value from ComicInfo.xml.
	/// </summary>
	public string Series
	{
		get;
	}

	/// <summary>
	/// Gets the writer value from ComicInfo.xml.
	/// </summary>
	public string Writer
	{
		get;
	}

	/// <summary>
	/// Gets the penciller value from ComicInfo.xml.
	/// </summary>
	public string Penciller
	{
		get;
	}

	/// <summary>
	/// Gets the summary value from ComicInfo.xml.
	/// </summary>
	public string Summary
	{
		get;
	}

	/// <summary>
	/// Gets the genre value from ComicInfo.xml.
	/// </summary>
	public string Genre
	{
		get;
	}

	/// <summary>
	/// Gets the status value from ComicInfo.xml or PublishingStatusTachiyomi fallback.
	/// </summary>
	public string Status
	{
		get;
	}
}
