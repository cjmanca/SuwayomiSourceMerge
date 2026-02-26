namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Supplies one per-title metadata coordinator request payload.
/// </summary>
internal sealed class ComickMetadataCoordinatorRequest
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ComickMetadataCoordinatorRequest"/> class.
	/// </summary>
	/// <param name="preferredOverrideDirectoryPath">Preferred override directory path for artifact writes.</param>
	/// <param name="allOverrideDirectoryPaths">All override title directory paths for existing-artifact discovery.</param>
	/// <param name="orderedSourceDirectoryPaths">Ordered source title directory paths used for details fallback generation.</param>
	/// <param name="displayTitle">Display title used for Comick lookup and details generation.</param>
	/// <param name="metadataOrchestration">Metadata orchestration options for cooldown/routing/language behavior.</param>
	public ComickMetadataCoordinatorRequest(
		string preferredOverrideDirectoryPath,
		IReadOnlyList<string> allOverrideDirectoryPaths,
		IReadOnlyList<string> orderedSourceDirectoryPaths,
		string displayTitle,
		MetadataOrchestrationOptions metadataOrchestration)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredOverrideDirectoryPath);
		ArgumentNullException.ThrowIfNull(allOverrideDirectoryPaths);
		ArgumentNullException.ThrowIfNull(orderedSourceDirectoryPaths);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);
		ArgumentNullException.ThrowIfNull(metadataOrchestration);

		if (allOverrideDirectoryPaths.Count == 0)
		{
			throw new ArgumentException(
				"All override directory paths must contain at least one entry.",
				nameof(allOverrideDirectoryPaths));
		}

		string[] overrideDirectoryPaths = new string[allOverrideDirectoryPaths.Count];
		for (int index = 0; index < allOverrideDirectoryPaths.Count; index++)
		{
			string? path = allOverrideDirectoryPaths[index];
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new ArgumentException(
					$"All override directory paths must not contain null, empty, or whitespace values. Invalid item at index {index}.",
					nameof(allOverrideDirectoryPaths));
			}

			overrideDirectoryPaths[index] = Path.GetFullPath(path);
		}

		string[] sourceDirectoryPaths = new string[orderedSourceDirectoryPaths.Count];
		for (int index = 0; index < orderedSourceDirectoryPaths.Count; index++)
		{
			string? path = orderedSourceDirectoryPaths[index];
			if (string.IsNullOrWhiteSpace(path))
			{
				throw new ArgumentException(
					$"Ordered source directory paths must not contain null, empty, or whitespace values. Invalid item at index {index}.",
					nameof(orderedSourceDirectoryPaths));
			}

			sourceDirectoryPaths[index] = Path.GetFullPath(path);
		}

		PreferredOverrideDirectoryPath = Path.GetFullPath(preferredOverrideDirectoryPath);
		AllOverrideDirectoryPaths = overrideDirectoryPaths;
		OrderedSourceDirectoryPaths = sourceDirectoryPaths;
		DisplayTitle = displayTitle.Trim();
		MetadataOrchestration = metadataOrchestration;
	}

	/// <summary>
	/// Gets preferred override directory path for artifact writes.
	/// </summary>
	public string PreferredOverrideDirectoryPath
	{
		get;
	}

	/// <summary>
	/// Gets all override title directory paths for existing-artifact discovery.
	/// </summary>
	public IReadOnlyList<string> AllOverrideDirectoryPaths
	{
		get;
	}

	/// <summary>
	/// Gets ordered source title directory paths used for details fallback generation.
	/// </summary>
	public IReadOnlyList<string> OrderedSourceDirectoryPaths
	{
		get;
	}

	/// <summary>
	/// Gets display title used for Comick lookup and details generation.
	/// </summary>
	public string DisplayTitle
	{
		get;
	}

	/// <summary>
	/// Gets metadata orchestration options for cooldown/routing/language behavior.
	/// </summary>
	public MetadataOrchestrationOptions MetadataOrchestration
	{
		get;
	}
}
