namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Defines deterministic mergerfs branch-link planning behavior.
/// </summary>
internal interface IMergerfsBranchPlanningService
{
	/// <summary>
	/// Builds a deterministic mergerfs branch-link plan for one canonical title group.
	/// </summary>
	/// <param name="request">Planning request containing override and source branch inputs.</param>
	/// <returns>Deterministic branch-link planning output.</returns>
	MergerfsBranchPlan Plan(MergerfsBranchPlanningRequest request);
}
