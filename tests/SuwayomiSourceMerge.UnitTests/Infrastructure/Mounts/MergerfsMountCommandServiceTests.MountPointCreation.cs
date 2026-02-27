namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Processes;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Mountpoint directory creation coverage for <see cref="MergerfsMountCommandService"/>.
/// </summary>
public sealed partial class MergerfsMountCommandServiceTests
{
	/// <summary>
	/// Verifies mount actions create missing mountpoint directories before invoking mergerfs.
	/// </summary>
	[Fact]
	public void ApplyAction_Expected_ShouldCreateMissingMountPointDirectory_BeforeMountCommand()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Canonical Title");
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
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.True(Directory.Exists(mountPoint));
		Assert.Single(executor.Requests);
		Assert.Equal("mergerfs", executor.Requests[0].FileName);
	}

	/// <summary>
	/// Verifies mount actions still execute when mountpoint directory already exists.
	/// </summary>
	[Fact]
	public void ApplyAction_Edge_ShouldMountSuccessfully_WhenMountPointDirectoryAlreadyExists()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Canonical Title");
		Directory.CreateDirectory(mountPoint);
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
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Single(executor.Requests);
		Assert.Equal("mergerfs", executor.Requests[0].FileName);
	}

	/// <summary>
	/// Verifies mount actions fail without invoking mergerfs when mountpoint directory creation fails.
	/// </summary>
	[Fact]
	public void ApplyAction_Failure_ShouldReturnFailure_WithoutExecutingMountCommand_WhenMountPointDirectoryCreationFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string occupiedPath = Path.Combine(temporaryDirectory.Path, "occupied");
		File.WriteAllText(occupiedPath, "occupied");
		string mountPoint = Path.Combine(occupiedPath, "Canonical Title");
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
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Failure, result.Outcome);
		Assert.Contains("Failed to ensure mountpoint directory", result.Diagnostic, StringComparison.Ordinal);
		Assert.Empty(executor.Requests);
	}

	/// <summary>
	/// Verifies fatal mountpoint-ensure exceptions are rethrown without command execution.
	/// </summary>
	[Fact]
	public void ApplyAction_Failure_ShouldRethrow_WhenMountPointDirectoryCreationThrowsFatalException()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Fatal Ensure Title");
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
		MergerfsMountCommandService service = new(
			executor,
			static _ => throw new OutOfMemoryException("fatal-mountpoint-ensure"));
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Mount,
			mountPoint,
			"suwayomi_hash",
			"/state/linkA=RW:/state/linkB=RO",
			MountReconciliationReason.MissingMount);

		Assert.Throws<OutOfMemoryException>(
			() => service.ApplyAction(
				action,
				"allow_other",
				TimeSpan.FromSeconds(5),
				TimeSpan.FromMilliseconds(10),
				cleanupHighPriority: false,
				cleanupPriorityIoniceClass: 3,
				cleanupPriorityNiceValue: -20));

		Assert.Empty(executor.Requests);
	}

	/// <summary>
	/// Verifies bad-mountpoint failures perform a one-time retry and can recover.
	/// </summary>
	[Fact]
	public void ApplyAction_ShouldRetryAndSucceed_WhenBadMountPointErrorOccurs()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Retry Title");
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				1,
				string.Empty,
				$"fuse: bad mount point `{mountPoint}`: No such file or directory",
				false,
				false,
				TimeSpan.FromMilliseconds(5)),
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
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Equal(2, executor.Requests.Count);
		Assert.Equal("mergerfs", executor.Requests[0].FileName);
		Assert.Equal("mergerfs", executor.Requests[1].FileName);
	}

	/// <summary>
	/// Verifies bad-mountpoint retries still report failure when the retry command fails.
	/// </summary>
	[Fact]
	public void ApplyAction_ShouldReturnFailure_WhenBadMountPointRetryFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string mountPoint = Path.Combine(temporaryDirectory.Path, "merged", "Retry Failure Title");
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				1,
				string.Empty,
				$"fuse: bad mount point `{mountPoint}`: No such file or directory",
				false,
				false,
				TimeSpan.FromMilliseconds(5)),
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				1,
				string.Empty,
				$"fuse: bad mount point `{mountPoint}`: No such file or directory",
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
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Failure, result.Outcome);
		Assert.Contains("bad-mountpoint retry", result.Diagnostic, StringComparison.Ordinal);
		Assert.Equal(2, executor.Requests.Count);
	}
}
