namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Represents one deterministic manga-equivalents update operation request.
/// </summary>
internal sealed class MangaEquivalentsUpdateRequest
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalentsUpdateRequest"/> class.
	/// </summary>
	/// <param name="mangaEquivalentsYamlPath">Absolute or relative path to <c>manga_equivalents.yml</c>.</param>
	/// <param name="mainTitle">Comick main title text to include in deduped candidate titles.</param>
	/// <param name="alternateTitles">Comick alternate title values.</param>
	/// <param name="preferredLanguage">Preferred language code used for canonical-title selection.</param>
	/// <exception cref="ArgumentException">
	/// Thrown when <paramref name="mangaEquivalentsYamlPath"/>, <paramref name="mainTitle"/>, or
	/// <paramref name="preferredLanguage"/> is null, empty, or whitespace.
	/// </exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="alternateTitles"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="alternateTitles"/> contains <see langword="null"/> entries.</exception>
	public MangaEquivalentsUpdateRequest(
		string mangaEquivalentsYamlPath,
		string mainTitle,
		IReadOnlyList<MangaEquivalentAlternateTitle> alternateTitles,
		string preferredLanguage)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaEquivalentsYamlPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(mainTitle);
		ArgumentNullException.ThrowIfNull(alternateTitles);
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);

		for (int index = 0; index < alternateTitles.Count; index++)
		{
			if (alternateTitles[index] is null)
			{
				throw new ArgumentException(
					$"Alternate titles must not contain null entries. Invalid item at index {index}.",
					nameof(alternateTitles));
			}
		}

		MangaEquivalentsYamlPath = Path.GetFullPath(mangaEquivalentsYamlPath);
		MainTitle = mainTitle.Trim();
		AlternateTitles = alternateTitles.ToArray();
		PreferredLanguage = preferredLanguage.Trim();
	}

	/// <summary>
	/// Gets resolved path to <c>manga_equivalents.yml</c>.
	/// </summary>
	public string MangaEquivalentsYamlPath
	{
		get;
	}

	/// <summary>
	/// Gets main title text.
	/// </summary>
	public string MainTitle
	{
		get;
	}

	/// <summary>
	/// Gets alternate title values.
	/// </summary>
	public IReadOnlyList<MangaEquivalentAlternateTitle> AlternateTitles
	{
		get;
	}

	/// <summary>
	/// Gets preferred language code used for canonical-title selection.
	/// </summary>
	public string PreferredLanguage
	{
		get;
	}
}
