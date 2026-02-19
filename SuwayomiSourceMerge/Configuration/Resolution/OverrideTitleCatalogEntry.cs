namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Represents one discovered override title directory entry used for canonical arbitration.
/// </summary>
internal sealed class OverrideTitleCatalogEntry
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideTitleCatalogEntry"/> class.
	/// </summary>
	/// <param name="title">Exact directory-name title text.</param>
	/// <param name="directoryPath">Absolute directory path for the discovered title.</param>
	/// <param name="normalizedKey">Normalized comparison key derived from <paramref name="title"/>.</param>
	/// <param name="strippedTitle">Title text after trailing scene-tag suffix stripping.</param>
	/// <param name="isSuffixTagged">
	/// <see langword="true"/> when <paramref name="title"/> contains a removable trailing scene-tag suffix.
	/// </param>
	/// <exception cref="ArgumentException">Thrown when required text values are null, empty, or whitespace.</exception>
	public OverrideTitleCatalogEntry(
		string title,
		string directoryPath,
		string normalizedKey,
		string strippedTitle,
		bool isSuffixTagged)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);
		ArgumentNullException.ThrowIfNull(strippedTitle);

		Title = title.Trim();
		DirectoryPath = Path.GetFullPath(directoryPath);
		NormalizedKey = normalizedKey;
		StrippedTitle = strippedTitle.Trim();
		IsSuffixTagged = isSuffixTagged;
	}

	/// <summary>
	/// Gets exact directory-name title text.
	/// </summary>
	public string Title
	{
		get;
	}

	/// <summary>
	/// Gets absolute directory path for this title entry.
	/// </summary>
	public string DirectoryPath
	{
		get;
	}

	/// <summary>
	/// Gets normalized comparison key derived from <see cref="Title"/>.
	/// </summary>
	public string NormalizedKey
	{
		get;
	}

	/// <summary>
	/// Gets title text after trailing scene-tag suffix stripping.
	/// </summary>
	public string StrippedTitle
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether <see cref="Title"/> includes a removable trailing scene-tag suffix.
	/// </summary>
	public bool IsSuffixTagged
	{
		get;
	}
}
