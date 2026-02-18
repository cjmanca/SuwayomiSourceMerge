namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Processes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Mergerfs options thread-cap composition coverage for <see cref="MergerfsMountCommandService"/>.
/// </summary>
public sealed partial class MergerfsMountCommandServiceTests
{
	/// <summary>
	/// Verifies mount options include a default threads token when none is configured.
	/// </summary>
	[Fact]
	public void ApplyAction_Expected_ShouldAppendDefaultThreadsOption_WhenThreadsIsMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Thread Cap Title");
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Mount,
			mountPoint,
			"suwayomi_hash",
			"/state/linkA=RW:/state/linkB=RO",
			MountReconciliationReason.MissingMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			"allow_other,use_ino",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Contains(
			$"allow_other,use_ino,threads={MergerfsOptionComposer.DefaultThreadCount},fsname=suwayomi_hash",
			executor.Requests[0].Arguments);
	}

	/// <summary>
	/// Verifies explicit threads configuration is preserved and not overridden.
	/// </summary>
	[Fact]
	public void ApplyAction_Edge_ShouldPreserveConfiguredThreadsOption_WhenThreadsIsAlreadySpecified()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Configured Threads Title");
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Mount,
			mountPoint,
			"suwayomi_hash",
			"/state/linkA=RW:/state/linkB=RO",
			MountReconciliationReason.MissingMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			"allow_other,threads=4,use_ino",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Contains("allow_other,threads=4,use_ino,fsname=suwayomi_hash", executor.Requests[0].Arguments);
		string mergedOptionsArgument = executor.Requests[0].Arguments[1];
		Assert.DoesNotContain(
			$"threads={MergerfsOptionComposer.DefaultThreadCount}",
			mergedOptionsArgument,
			StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies malformed option separators still normalize to a thread-capped mount command.
	/// </summary>
	[Fact]
	public void ApplyAction_FailurePath_ShouldNormalizeOptionSeparators_WhenOptionsContainOnlyCommas()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Comma Options Title");
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Mount,
			mountPoint,
			"suwayomi_hash",
			"/state/linkA=RW:/state/linkB=RO",
			MountReconciliationReason.MissingMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			",,,",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Contains(
			$"threads={MergerfsOptionComposer.DefaultThreadCount},fsname=suwayomi_hash",
			executor.Requests[0].Arguments);
	}
}
