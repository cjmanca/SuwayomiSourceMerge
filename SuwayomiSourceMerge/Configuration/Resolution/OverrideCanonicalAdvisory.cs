namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Describes one resolver advisory emitted when tagged-only override titles are preserved.
/// </summary>
internal sealed class OverrideCanonicalAdvisory
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideCanonicalAdvisory"/> class.
	/// </summary>
	/// <param name="normalizedKey">Normalized key represented by the advisory.</param>
	/// <param name="selectedTitle">Selected canonical title for the normalized key.</param>
	/// <param name="selectedDirectoryPath">Directory path for the selected title entry.</param>
	/// <param name="suggestedStrippedTitle">Suggested stripped display title for manual rename guidance.</param>
	/// <exception cref="ArgumentException">Thrown when required values are null, empty, or whitespace.</exception>
	public OverrideCanonicalAdvisory(
		string normalizedKey,
		string selectedTitle,
		string selectedDirectoryPath,
		string suggestedStrippedTitle)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(selectedTitle);
		ArgumentException.ThrowIfNullOrWhiteSpace(selectedDirectoryPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(suggestedStrippedTitle);

		NormalizedKey = normalizedKey;
		SelectedTitle = selectedTitle;
		SelectedDirectoryPath = selectedDirectoryPath;
		SuggestedStrippedTitle = suggestedStrippedTitle;
	}

	/// <summary>
	/// Gets normalized key represented by this advisory.
	/// </summary>
	public string NormalizedKey
	{
		get;
	}

	/// <summary>
	/// Gets selected canonical title for this normalized key.
	/// </summary>
	public string SelectedTitle
	{
		get;
	}

	/// <summary>
	/// Gets selected title directory path.
	/// </summary>
	public string SelectedDirectoryPath
	{
		get;
	}

	/// <summary>
	/// Gets suggested stripped display title for manual rename guidance.
	/// </summary>
	public string SuggestedStrippedTitle
	{
		get;
	}
}
