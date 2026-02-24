namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Supplies the inputs required to ensure <c>cover.jpg</c> metadata for one canonical title.
/// </summary>
internal sealed class OverrideCoverRequest
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideCoverRequest"/> class.
	/// </summary>
	/// <param name="preferredOverrideDirectoryPath">Preferred override directory path where <c>cover.jpg</c> should be written.</param>
	/// <param name="allOverrideDirectoryPaths">All override title directory paths used to detect existing <c>cover.jpg</c> files.</param>
	/// <param name="coverKey">Comick cover key value from <c>md_covers[0].b2key</c>.</param>
	/// <exception cref="ArgumentException">Thrown when required string arguments are missing or invalid.</exception>
	/// <exception cref="ArgumentNullException">Thrown when required collections are <see langword="null"/>.</exception>
	public OverrideCoverRequest(
		string preferredOverrideDirectoryPath,
		IReadOnlyList<string> allOverrideDirectoryPaths,
		string coverKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredOverrideDirectoryPath);
		ArgumentNullException.ThrowIfNull(allOverrideDirectoryPaths);
		ArgumentException.ThrowIfNullOrWhiteSpace(coverKey);

		if (allOverrideDirectoryPaths.Count == 0)
		{
			throw new ArgumentException(
				"All override directory paths must contain at least one entry.",
				nameof(allOverrideDirectoryPaths));
		}

		string[] overrideDirectories = new string[allOverrideDirectoryPaths.Count];
		for (int index = 0; index < allOverrideDirectoryPaths.Count; index++)
		{
			string? path = allOverrideDirectoryPaths[index];
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new ArgumentException(
					$"All override directory paths must not contain null, empty, or whitespace values. Invalid item at index {index}.",
					nameof(allOverrideDirectoryPaths));
			}

			overrideDirectories[index] = Path.GetFullPath(path);
		}

		PreferredOverrideDirectoryPath = Path.GetFullPath(preferredOverrideDirectoryPath);
		AllOverrideDirectoryPaths = overrideDirectories;
		CoverKey = coverKey.Trim();
	}

	/// <summary>
	/// Gets the preferred override directory path where <c>cover.jpg</c> should be written.
	/// </summary>
	public string PreferredOverrideDirectoryPath
	{
		get;
	}

	/// <summary>
	/// Gets all override title directory paths used to detect existing <c>cover.jpg</c> files.
	/// </summary>
	public IReadOnlyList<string> AllOverrideDirectoryPaths
	{
		get;
	}

	/// <summary>
	/// Gets Comick cover-key input from <c>md_covers[0].b2key</c>.
	/// </summary>
	public string CoverKey
	{
		get;
	}
}
