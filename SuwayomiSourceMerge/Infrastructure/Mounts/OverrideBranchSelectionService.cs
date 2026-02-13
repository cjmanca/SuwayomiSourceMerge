namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Selects preferred and additional override branches for one canonical title.
/// </summary>
internal sealed class OverrideBranchSelectionService
{
	/// <summary>
	/// Override volume directory name that has preferred-branch precedence.
	/// </summary>
	private const string PRIORITY_VOLUME_NAME = "priority";

	/// <summary>
	/// Path comparer used for de-duplication and equality checks.
	/// </summary>
	private static readonly StringComparer PATH_COMPARER = PathSafetyPolicy.GetPathComparer();

	/// <summary>
	/// Selects deterministic override branch paths using the preferred-then-existing policy.
	/// </summary>
	/// <param name="canonicalTitle">Canonical title used for per-title override directory resolution.</param>
	/// <param name="overrideVolumePaths">Absolute override volume roots.</param>
	/// <returns>Deterministic override selection result with preferred entry first.</returns>
	/// <exception cref="ArgumentException">Thrown when required values are missing or invalid.</exception>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="overrideVolumePaths"/> is <see langword="null"/>.</exception>
	public OverrideBranchSelectionResult Select(
		string canonicalTitle,
		IReadOnlyList<string> overrideVolumePaths)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTitle);
		ArgumentNullException.ThrowIfNull(overrideVolumePaths);

		string[] normalizedOverrideVolumes = NormalizeOverrideVolumes(overrideVolumePaths);
		string preferredVolumePath = ResolvePreferredVolumePath(normalizedOverrideVolumes);
		string preferredOverridePath = BuildTitlePath(preferredVolumePath, canonicalTitle);

		List<OverrideBranchSelectionEntry> orderedEntries =
		[
			new OverrideBranchSelectionEntry(
				preferredVolumePath,
				preferredOverridePath,
				isPreferred: true)
		];

		HashSet<string> seenTitlePaths = new(PATH_COMPARER)
		{
			preferredOverridePath
		};

		foreach (string overrideVolumePath in normalizedOverrideVolumes)
		{
			if (PATH_COMPARER.Equals(overrideVolumePath, preferredVolumePath))
			{
				continue;
			}

			string candidateTitlePath = BuildTitlePath(overrideVolumePath, canonicalTitle);
			if (!Directory.Exists(candidateTitlePath))
			{
				continue;
			}

			if (!seenTitlePaths.Add(candidateTitlePath))
			{
				continue;
			}

			orderedEntries.Add(
				new OverrideBranchSelectionEntry(
					overrideVolumePath,
					candidateTitlePath,
					isPreferred: false));
		}

		return new OverrideBranchSelectionResult(preferredOverridePath, orderedEntries);
	}

	/// <summary>
	/// Normalizes, de-duplicates, and orders override volume paths.
	/// </summary>
	/// <param name="overrideVolumePaths">Raw override volume paths.</param>
	/// <returns>Normalized and ordered override volume paths.</returns>
	private static string[] NormalizeOverrideVolumes(IReadOnlyList<string> overrideVolumePaths)
	{
		if (overrideVolumePaths.Count == 0)
		{
			throw new ArgumentException(
				"Override volume paths must contain at least one entry.",
				nameof(overrideVolumePaths));
		}

		List<string> normalizedPaths = new(overrideVolumePaths.Count);
		for (int index = 0; index < overrideVolumePaths.Count; index++)
		{
			string? volumePath = overrideVolumePaths[index];
			if (string.IsNullOrWhiteSpace(volumePath))
			{
				throw new ArgumentException(
					$"Override volume path at index {index} must not be null, empty, or whitespace.",
					nameof(overrideVolumePaths));
			}

			string trimmedVolumePath = volumePath.Trim();
			if (!Path.IsPathRooted(trimmedVolumePath))
			{
				throw new ArgumentException(
					$"Override volume path at index {index} must be an absolute path.",
					nameof(overrideVolumePaths));
			}

			normalizedPaths.Add(Path.GetFullPath(trimmedVolumePath));
		}

		string[] orderedPaths = normalizedPaths
			.OrderBy(path => path, PATH_COMPARER)
			.ThenBy(path => path, StringComparer.Ordinal)
			.ToArray();

		HashSet<string> seenPaths = new(PATH_COMPARER);
		List<string> deduplicatedPaths = new(orderedPaths.Length);
		foreach (string path in orderedPaths)
		{
			if (!seenPaths.Add(path))
			{
				continue;
			}

			deduplicatedPaths.Add(path);
		}

		return deduplicatedPaths.ToArray();
	}

	/// <summary>
	/// Resolves the preferred override volume path.
	/// </summary>
	/// <param name="orderedOverrideVolumes">Normalized and ordered override volumes.</param>
	/// <returns>Preferred override volume path.</returns>
	private static string ResolvePreferredVolumePath(IReadOnlyList<string> orderedOverrideVolumes)
	{
		foreach (string volumePath in orderedOverrideVolumes)
		{
			string volumeName = Path.GetFileName(Path.TrimEndingDirectorySeparator(volumePath));
			if (string.Equals(volumeName, PRIORITY_VOLUME_NAME, StringComparison.OrdinalIgnoreCase))
			{
				return volumePath;
			}
		}

		return orderedOverrideVolumes[0];
	}

	/// <summary>
	/// Builds a normalized title path under one override volume.
	/// </summary>
	/// <param name="overrideVolumePath">Absolute override volume path.</param>
	/// <param name="canonicalTitle">Canonical title segment.</param>
	/// <returns>Absolute per-title override path.</returns>
	private static string BuildTitlePath(string overrideVolumePath, string canonicalTitle)
	{
		string escapedTitleSegment = PathSafetyPolicy.EscapeReservedSegment(canonicalTitle);
		string candidateTitlePath = Path.GetFullPath(Path.Combine(overrideVolumePath, escapedTitleSegment));
		return PathSafetyPolicy.EnsureStrictChildPath(
			overrideVolumePath,
			candidateTitlePath,
			nameof(canonicalTitle));
	}
}
