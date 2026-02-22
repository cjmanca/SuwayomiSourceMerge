namespace SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Represents typed Comick search response payload.
/// </summary>
internal sealed class ComickSearchResponse
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ComickSearchResponse"/> class.
	/// </summary>
	/// <param name="comics">Search result items.</param>
	public ComickSearchResponse(IReadOnlyList<ComickSearchComic> comics)
	{
		ArgumentNullException.ThrowIfNull(comics);
		Comics = comics;
	}

	/// <summary>
	/// Gets typed search result items.
	/// </summary>
	public IReadOnlyList<ComickSearchComic> Comics
	{
		get;
	}
}
