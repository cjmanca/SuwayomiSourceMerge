namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Represents one alternate title candidate used for manga-equivalents updates.
/// </summary>
internal sealed class MangaEquivalentAlternateTitle
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentAlternateTitle"/> class.
	/// </summary>
	/// <param name="title">Alternate title text.</param>
	/// <param name="language">Optional language code for title selection.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null, empty, or whitespace.</exception>
	public MangaEquivalentAlternateTitle(string title, string? language)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);

		Title = title.Trim();
		Language = string.IsNullOrWhiteSpace(language)
			? null
			: language.Trim();
	}

	/// <summary>
	/// Gets alternate title text.
	/// </summary>
	public string Title
	{
		get;
	}

	/// <summary>
	/// Gets optional language code for this title.
	/// </summary>
	public string? Language
	{
		get;
	}
}
