namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents the deterministic override-branch selection result for one canonical title.
/// </summary>
internal sealed class OverrideBranchSelectionResult
{

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideBranchSelectionResult"/> class.
	/// </summary>
	/// <param name="preferredOverridePath">Fully-qualified absolute preferred override path used for new writes.</param>
	/// <param name="orderedEntries">Ordered override entries with preferred entry first.</param>
	/// <exception cref="ArgumentException">Thrown when required values are missing or invalid.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="orderedEntries"/> is <see langword="null"/>.</exception>
	public OverrideBranchSelectionResult(
		string preferredOverridePath,
		IReadOnlyList<OverrideBranchSelectionEntry> orderedEntries)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredOverridePath);
		ArgumentNullException.ThrowIfNull(orderedEntries);

		string normalizedPreferredOverridePath = PathSafetyPolicy.NormalizeFullyQualifiedPath(
			preferredOverridePath,
			nameof(preferredOverridePath));

		if (orderedEntries.Count == 0)
		{
			throw new ArgumentException(
				"Override entries must contain at least one item.",
				nameof(orderedEntries));
		}

		OverrideBranchSelectionEntry[] entryArray = new OverrideBranchSelectionEntry[orderedEntries.Count];
		for (int index = 0; index < orderedEntries.Count; index++)
		{
			OverrideBranchSelectionEntry? entry = orderedEntries[index];
			if (entry is null)
			{
				throw new ArgumentException(
					$"Override entries must not contain null items. Null item at index {index}.",
					nameof(orderedEntries));
			}

			entryArray[index] = entry;
		}

		if (!entryArray[0].IsPreferred)
		{
			throw new ArgumentException(
				"The first override entry must be marked as preferred.",
				nameof(orderedEntries));
		}

		if (!PathSafetyPolicy.ArePathsEqual(entryArray[0].TitlePath, normalizedPreferredOverridePath))
		{
			throw new ArgumentException(
				"Preferred override path must match the first override entry title path.",
				nameof(preferredOverridePath));
		}

		PreferredOverridePath = normalizedPreferredOverridePath;
		OrderedEntries = entryArray;
	}

	/// <summary>
	/// Gets the fully-qualified absolute preferred override path.
	/// </summary>
	public string PreferredOverridePath
	{
		get;
	}

	/// <summary>
	/// Gets ordered override entries with preferred entry first.
	/// </summary>
	public IReadOnlyList<OverrideBranchSelectionEntry> OrderedEntries
	{
		get;
	}
}
