namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Supplies the inputs required to ensure details.json metadata for one canonical title.
/// </summary>
internal sealed class OverrideDetailsRequest
{
	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideDetailsRequest"/> class.
	/// </summary>
	/// <param name="preferredOverrideDirectoryPath">Preferred override directory path where details.json should be written.</param>
	/// <param name="allOverrideDirectoryPaths">All override title directory paths used to detect existing details.json files.</param>
	/// <param name="orderedSourceDirectoryPaths">Ordered source title directory paths used for seeding and ComicInfo.xml discovery.</param>
	/// <param name="displayTitle">Canonical display title written to the details.json title field.</param>
	/// <param name="detailsDescriptionMode">Description rendering mode. Supported values are <c>text</c>, <c>br</c>, and <c>html</c>.</param>
	/// <param name="metadataOrchestration">Comick metadata orchestration settings for this request.</param>
	/// <exception cref="ArgumentException">Thrown when required string arguments are missing or invalid.</exception>
	/// <exception cref="ArgumentNullException">Thrown when required collections are <see langword="null"/>.</exception>
	public OverrideDetailsRequest(
		string preferredOverrideDirectoryPath,
		IReadOnlyList<string> allOverrideDirectoryPaths,
		IReadOnlyList<string> orderedSourceDirectoryPaths,
		string displayTitle,
		string detailsDescriptionMode,
		MetadataOrchestrationOptions metadataOrchestration)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredOverrideDirectoryPath);
		ArgumentNullException.ThrowIfNull(allOverrideDirectoryPaths);
		ArgumentNullException.ThrowIfNull(orderedSourceDirectoryPaths);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);
		ArgumentException.ThrowIfNullOrWhiteSpace(detailsDescriptionMode);
		ArgumentNullException.ThrowIfNull(metadataOrchestration);

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

		string[] sourceDirectories = new string[orderedSourceDirectoryPaths.Count];
		for (int index = 0; index < orderedSourceDirectoryPaths.Count; index++)
		{
			string? path = orderedSourceDirectoryPaths[index];
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new ArgumentException(
					$"Ordered source directory paths must not contain null, empty, or whitespace values. Invalid item at index {index}.",
					nameof(orderedSourceDirectoryPaths));
			}

			sourceDirectories[index] = Path.GetFullPath(path);
		}

		PreferredOverrideDirectoryPath = Path.GetFullPath(preferredOverrideDirectoryPath);
		AllOverrideDirectoryPaths = overrideDirectories;
		OrderedSourceDirectoryPaths = sourceDirectories;
		DisplayTitle = displayTitle.Trim();
		DetailsDescriptionMode = NormalizeDescriptionMode(detailsDescriptionMode);
		MetadataOrchestration = metadataOrchestration;
	}

	/// <summary>
	/// Gets the preferred override directory path where details.json should be written.
	/// </summary>
	public string PreferredOverrideDirectoryPath
	{
		get;
	}

	/// <summary>
	/// Gets all override title directory paths used to detect existing details.json files.
	/// </summary>
	public IReadOnlyList<string> AllOverrideDirectoryPaths
	{
		get;
	}

	/// <summary>
	/// Gets ordered source title directory paths used for seeding and ComicInfo.xml discovery.
	/// </summary>
	public IReadOnlyList<string> OrderedSourceDirectoryPaths
	{
		get;
	}

	/// <summary>
	/// Gets the canonical display title written to details.json.
	/// </summary>
	public string DisplayTitle
	{
		get;
	}

	/// <summary>
	/// Gets normalized description rendering mode.
	/// </summary>
	public string DetailsDescriptionMode
	{
		get;
	}

	/// <summary>
	/// Gets metadata orchestration options used for Comick/Flaresolverr request behavior.
	/// </summary>
	public MetadataOrchestrationOptions MetadataOrchestration
	{
		get;
	}

	/// <summary>
	/// Normalizes and validates details description mode values.
	/// </summary>
	/// <param name="detailsDescriptionMode">Description mode value to normalize.</param>
	/// <returns>Lowercase normalized mode value.</returns>
	/// <exception cref="ArgumentException">Thrown when the mode is not supported.</exception>
	private static string NormalizeDescriptionMode(string detailsDescriptionMode)
	{
		string normalized = detailsDescriptionMode.Trim().ToLowerInvariant();
		if (normalized != "text" && normalized != "br" && normalized != "html")
		{
			throw new ArgumentException(
				"Details description mode must be one of: text, br, html.",
				nameof(detailsDescriptionMode));
		}

		return normalized;
	}
}
