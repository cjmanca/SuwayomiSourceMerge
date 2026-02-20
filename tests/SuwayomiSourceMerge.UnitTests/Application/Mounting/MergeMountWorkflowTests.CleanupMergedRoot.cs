namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies merged-root directory cleanup behavior for <see cref="MergeMountWorkflow"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Verifies cleanup removes empty merged directories and quarantines non-empty directories under config.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Expected_ShouldRemoveEmptyMergedDirectories_AndQuarantineNonEmptyDirectories()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string emptyDirectoryPath = Path.Combine(fixture.Options.MergedRootPath, "EmptyTitle", "NestedEmpty");
		string nonEmptyDirectoryPath = Path.Combine(fixture.Options.MergedRootPath, "NonEmptyTitle");
		Directory.CreateDirectory(emptyDirectoryPath);
		Directory.CreateDirectory(nonEmptyDirectoryPath);
		File.WriteAllText(Path.Combine(nonEmptyDirectoryPath, "chapter.txt"), "content");

		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		workflow.OnWorkerStarting();

		Assert.False(Directory.Exists(Path.Combine(fixture.Options.MergedRootPath, "EmptyTitle")));
		Assert.False(Directory.Exists(nonEmptyDirectoryPath));
		Assert.Empty(Directory.GetDirectories(fixture.Options.MergedRootPath));

		string quarantineRootPath = Path.Combine(fixture.Options.ConfigRootPath, "cleanup", "merged-residual");
		Assert.True(Directory.Exists(quarantineRootPath));
		string[] movedDirectoryCandidates = Directory
			.GetDirectories(quarantineRootPath, "*", SearchOption.AllDirectories)
			.Where(static path => Path.GetFileName(path).StartsWith("NonEmptyTitle", StringComparison.Ordinal))
			.ToArray();
		Assert.Single(movedDirectoryCandidates);
		Assert.True(File.Exists(Path.Combine(movedDirectoryCandidates[0], "chapter.txt")));
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.cleanup" &&
				entry.Level == LogLevel.Warning &&
				entry.Message.Contains("Moved non-empty merged directory", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies merged-root residual cleanup is skipped when managed mountpoints remain active.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldSkipMergedRootResidualCleanup_WhenManagedMountpointsRemainActive()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string stillMountedDirectoryPath = Path.Combine(fixture.Options.MergedRootPath, "StillMountedTitle");
		Directory.CreateDirectory(stillMountedDirectoryPath);
		File.WriteAllText(Path.Combine(stillMountedDirectoryPath, "chapter.txt"), "content");
		MountSnapshot activeMountSnapshot = new(
			[
				new MountSnapshotEntry(
					stillMountedDirectoryPath,
					"fuse.mergerfs",
					"source",
					"rw",
					isHealthy: true)
			],
			[]);
		// RunCleanupPass captures mount state twice (pre-unmount + post-unmount), so enqueue two snapshots.
		fixture.MountSnapshotService.EnqueueSnapshot(activeMountSnapshot);
		fixture.MountSnapshotService.EnqueueSnapshot(activeMountSnapshot);

		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		workflow.OnWorkerStarting();

		Assert.Equal(2, fixture.MountSnapshotService.CaptureCount);
		Assert.True(Directory.Exists(stillMountedDirectoryPath));
		string quarantineRootPath = Path.Combine(fixture.Options.ConfigRootPath, "cleanup", "merged-residual");
		Assert.False(Directory.Exists(quarantineRootPath));
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.cleanup" &&
				entry.Level == LogLevel.Warning &&
				entry.Message.Contains("Skipped merged-root directory cleanup", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies merged-root cleanup is skipped when mount visibility is degraded.
	/// </summary>
	[Fact]
	public void OnWorkerStarting_Edge_ShouldSkipMergedRootResidualCleanup_WhenSnapshotVisibilityIsDegraded()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string nonEmptyDirectoryPath = Path.Combine(fixture.Options.MergedRootPath, "DegradedTitle");
		Directory.CreateDirectory(nonEmptyDirectoryPath);
		File.WriteAllText(Path.Combine(nonEmptyDirectoryPath, "chapter.txt"), "content");
		fixture.MountSnapshotService.EnqueueSnapshot(new MountSnapshot(
			[],
			[
				new MountSnapshotWarning(
					"MOUNT-SNAP-900",
					"degraded visibility",
					MountSnapshotWarningSeverity.DegradedVisibility)
			]));
		fixture.MountSnapshotService.EnqueueSnapshot(new MountSnapshot([], []));

		MergeMountWorkflow workflow = fixture.CreateWorkflow();
		workflow.OnWorkerStarting();

		Assert.True(Directory.Exists(nonEmptyDirectoryPath));
		string quarantineRootPath = Path.Combine(fixture.Options.ConfigRootPath, "cleanup", "merged-residual");
		Assert.False(Directory.Exists(quarantineRootPath));
		Assert.Contains(
			fixture.Logger.Events,
			static entry => entry.EventId == "merge.workflow.cleanup" &&
				entry.Level == LogLevel.Warning &&
				entry.Message.Contains("Skipped merged-root directory cleanup because mount snapshot reliability was degraded", StringComparison.Ordinal));
	}
}
