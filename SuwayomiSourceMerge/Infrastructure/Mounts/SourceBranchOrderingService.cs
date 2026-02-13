using SuwayomiSourceMerge.Configuration.Resolution;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Orders source branch candidates using configured source priority and deterministic tie-breakers.
/// </summary>
internal sealed class SourceBranchOrderingService
{
	/// <summary>
	/// Path comparer used for source-path de-duplication and primary path tie-break ordering.
	/// </summary>
	private static readonly StringComparer PATH_COMPARER = PathSafetyPolicy.GetPathComparer();

	/// <summary>
	/// Orders source branch candidates and removes duplicate paths.
	/// </summary>
	/// <param name="sourceBranches">Source branch candidates to order.</param>
	/// <param name="sourcePriorityService">Source-priority service used for configured priority lookups.</param>
	/// <returns>Deterministically ordered and de-duplicated source branch candidates.</returns>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="sourceBranches"/> or <paramref name="sourcePriorityService"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">Thrown when <paramref name="sourceBranches"/> contains null entries.</exception>
	public IReadOnlyList<MergerfsSourceBranchCandidate> Order(
		IReadOnlyList<MergerfsSourceBranchCandidate> sourceBranches,
		ISourcePriorityService sourcePriorityService)
	{
		ArgumentNullException.ThrowIfNull(sourceBranches);
		ArgumentNullException.ThrowIfNull(sourcePriorityService);

		if (sourceBranches.Count == 0)
		{
			return [];
		}

		OrderedSourceBranch[] ordered = new OrderedSourceBranch[sourceBranches.Count];
		for (int index = 0; index < sourceBranches.Count; index++)
		{
			MergerfsSourceBranchCandidate? candidate = sourceBranches[index];
			if (candidate is null)
			{
				throw new ArgumentException(
					$"Source branches must not contain null items. Null item at index {index}.",
					nameof(sourceBranches));
			}

			ordered[index] = new OrderedSourceBranch(
				candidate,
				sourcePriorityService.GetPriorityOrDefault(candidate.SourceName, int.MaxValue));
		}

		OrderedSourceBranch[] sorted = ordered
			.OrderBy(item => item.Priority)
			.ThenBy(item => item.Candidate.SourceName, StringComparer.Ordinal)
			.ThenBy(item => item.Candidate.SourcePath, PATH_COMPARER)
			.ThenBy(item => item.Candidate.SourcePath, StringComparer.Ordinal)
			.ToArray();

		HashSet<string> seenPaths = new(PATH_COMPARER);
		List<MergerfsSourceBranchCandidate> deduplicated = [];
		foreach (OrderedSourceBranch item in sorted)
		{
			if (!seenPaths.Add(item.Candidate.SourcePath))
			{
				continue;
			}

			deduplicated.Add(item.Candidate);
		}

		return deduplicated;
	}

	/// <summary>
	/// Holds one source candidate with computed priority for sorting.
	/// </summary>
	private readonly record struct OrderedSourceBranch(MergerfsSourceBranchCandidate Candidate, int Priority);
}
