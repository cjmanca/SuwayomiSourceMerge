namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies warning-level diagnostics for low-visibility merge-input scenarios.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Verifies warning diagnostics are emitted when no source volumes are discovered.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldLogWarning_WhenNoSourceVolumesAreDiscovered()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.VolumeDiscoveryService.SourceVolumePaths = [];
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan([]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.warning" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning &&
				entry.Message.Contains("No source volumes were discovered", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies warning diagnostics are emitted when a pass resolves to zero desired mounts.
	/// </summary>
	[Fact]
	public void RunMergePass_Edge_ShouldLogWarning_WhenPassProducesZeroDesiredMounts()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(
			temporaryDirectory,
			excludedSources: ["sourcea"]);
		fixture.ReconciliationService.NextPlanFactory = _ => new MountReconciliationPlan([]);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("startup", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.warning" &&
				entry.Level == global::SuwayomiSourceMerge.Infrastructure.Logging.LogLevel.Warning &&
				entry.Message.Contains("produced zero desired mounts", StringComparison.Ordinal));
	}
}
