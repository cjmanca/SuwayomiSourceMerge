namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Supplies the inputs required to generate deterministic mergerfs branch-link plans.
/// </summary>
internal sealed class MergerfsBranchPlanningRequest
{

	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsBranchPlanningRequest"/> class.
	/// </summary>
	/// <param name="groupKey">Stable group key used for branch-directory identity generation.</param>
	/// <param name="canonicalTitle">Canonical title name used when resolving per-title override directories.</param>
	/// <param name="branchLinksRootPath">Fully-qualified absolute root directory where branch-link directories are staged.</param>
	/// <param name="overrideVolumePaths">Fully-qualified absolute override volume roots used for preferred and additional override selection.</param>
	/// <param name="sourceBranches">Source branch candidates to include as read-only branches.</param>
	/// <exception cref="ArgumentException">Thrown when required values are missing or invalid.</exception>
	/// <exception cref="ArgumentNullException">Thrown when collections are <see langword="null"/>.</exception>
	public MergerfsBranchPlanningRequest(
		string groupKey,
		string canonicalTitle,
		string branchLinksRootPath,
		IReadOnlyList<string> overrideVolumePaths,
		IReadOnlyList<MergerfsSourceBranchCandidate> sourceBranches)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(groupKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTitle);
		ArgumentException.ThrowIfNullOrWhiteSpace(branchLinksRootPath);
		ArgumentNullException.ThrowIfNull(overrideVolumePaths);
		ArgumentNullException.ThrowIfNull(sourceBranches);

		string trimmedCanonicalTitle = canonicalTitle.Trim();
		if (PathSafetyPolicy.ContainsDirectorySeparator(trimmedCanonicalTitle))
		{
			throw new ArgumentException(
				"Canonical title must not contain directory separators.",
				nameof(canonicalTitle));
		}

		string normalizedBranchLinksRootPath = PathSafetyPolicy.NormalizeFullyQualifiedPath(
			branchLinksRootPath,
			nameof(branchLinksRootPath));

		if (overrideVolumePaths.Count == 0)
		{
			throw new ArgumentException(
				"Override volume paths must contain at least one entry.",
				nameof(overrideVolumePaths));
		}

		string[] overrideVolumePathArray = new string[overrideVolumePaths.Count];
		for (int index = 0; index < overrideVolumePaths.Count; index++)
		{
			string? volumePath = overrideVolumePaths[index];
			if (string.IsNullOrWhiteSpace(volumePath))
			{
				throw new ArgumentException(
					$"Override volume path at index {index} must not be null, empty, or whitespace.",
					nameof(overrideVolumePaths));
			}

			overrideVolumePathArray[index] = PathSafetyPolicy.NormalizeFullyQualifiedPath(
				volumePath,
				nameof(overrideVolumePaths));
		}

		MergerfsSourceBranchCandidate[] sourceBranchArray = new MergerfsSourceBranchCandidate[sourceBranches.Count];
		for (int index = 0; index < sourceBranches.Count; index++)
		{
			MergerfsSourceBranchCandidate? candidate = sourceBranches[index];
			if (candidate is null)
			{
				throw new ArgumentException(
					$"Source branches must not contain null items. Null item at index {index}.",
					nameof(sourceBranches));
			}

			sourceBranchArray[index] = candidate;
		}

		GroupKey = groupKey.Trim();
		CanonicalTitle = trimmedCanonicalTitle;
		BranchLinksRootPath = normalizedBranchLinksRootPath;
		OverrideVolumePaths = overrideVolumePathArray;
		SourceBranches = sourceBranchArray;
	}

	/// <summary>
	/// Gets the stable group key used for branch-directory identity generation.
	/// </summary>
	public string GroupKey
	{
		get;
	}

	/// <summary>
	/// Gets the canonical title used to derive per-title paths.
	/// </summary>
	public string CanonicalTitle
	{
		get;
	}

	/// <summary>
	/// Gets the fully-qualified absolute root directory for branch-link staging.
	/// </summary>
	public string BranchLinksRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the fully-qualified absolute override volume root paths used for override selection.
	/// </summary>
	public IReadOnlyList<string> OverrideVolumePaths
	{
		get;
	}

	/// <summary>
	/// Gets source branch candidates used for read-only source branch planning.
	/// </summary>
	public IReadOnlyList<MergerfsSourceBranchCandidate> SourceBranches
	{
		get;
	}
}
