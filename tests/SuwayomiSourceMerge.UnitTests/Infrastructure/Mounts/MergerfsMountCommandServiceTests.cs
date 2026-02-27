namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="MergerfsMountCommandService"/>.
/// </summary>
public sealed partial class MergerfsMountCommandServiceTests
{
	/// <summary>
	/// Verifies mount command composition includes fsname with desired identity.
	/// </summary>
	[Fact]
	public void ApplyAction_Expected_ShouldIncludeFsnameInMergerfsOptions_WhenMounting()
	{
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
			"/ssm/merged/Title",
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
		Assert.Contains("allow_other,threads=1,fsname=suwayomi_hash", executor.Requests[0].Arguments);
	}

	/// <summary>
	/// Verifies unmount falls back through fusermount tools and succeeds with final umount.
	/// </summary>
	[Fact]
	public void UnmountMountPoint_Edge_ShouldFallbackAcrossUnmountCommands_WhenEarlierToolsUnavailable()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);

		MountActionApplyResult result = service.UnmountMountPoint(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Equal(3, executor.Requests.Count);
		Assert.Equal("fusermount3", executor.Requests[0].FileName);
		Assert.Equal("fusermount", executor.Requests[1].FileName);
		Assert.Equal("umount", executor.Requests[2].FileName);
	}

	/// <summary>
	/// Verifies timeout and busy stderr conditions map to busy outcomes.
	/// </summary>
	[Fact]
	public void ApplyAction_Failure_ShouldReturnBusy_WhenCommandIndicatesBusyCondition()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				16,
				string.Empty,
				"Device or resource busy",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				16,
				string.Empty,
				"Device or resource busy",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				16,
				string.Empty,
				"Device or resource busy",
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Unmount,
			"/ssm/merged/Title",
			desiredIdentity: null,
			mountPayload: null,
			MountReconciliationReason.StaleMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Busy, result.Outcome);
	}

	/// <summary>
	/// Verifies busy conditions written to stdout are treated as busy when stderr is empty.
	/// </summary>
	[Fact]
	public void ApplyAction_Edge_ShouldReturnBusy_WhenBusyConditionIsReportedOnStdoutAndStderrIsEmpty()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				16,
				"Device or resource busy",
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				16,
				"Device or resource busy",
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				16,
				"Device or resource busy",
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Unmount,
			"/ssm/merged/Title",
			desiredIdentity: null,
			mountPayload: null,
			MountReconciliationReason.StaleMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Busy, result.Outcome);
	}

	/// <summary>
	/// Verifies non-busy stderr takes precedence over stdout busy text.
	/// </summary>
	[Fact]
	public void ApplyAction_Failure_ShouldReturnFailure_WhenStderrIsNonBusyEvenIfStdoutContainsBusyToken()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				1,
				"Device or resource busy",
				"permission denied",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Unmount,
			"/ssm/merged/Title",
			desiredIdentity: null,
			mountPayload: null,
			MountReconciliationReason.StaleMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Failure, result.Outcome);
		Assert.Contains("permission denied", result.Diagnostic, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies mixed busy and non-busy unmount failures return non-busy failure outcomes.
	/// </summary>
	[Fact]
	public void ApplyAction_Failure_ShouldReturnFailure_WhenBusyAndNonBusyUnmountFailuresAreMixed()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				16,
				string.Empty,
				"Device or resource busy",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.StartFailure,
				null,
				string.Empty,
				"failed to start",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Unmount,
			"/ssm/merged/Title",
			desiredIdentity: null,
			mountPayload: null,
			MountReconciliationReason.StaleMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Failure, result.Outcome);
		Assert.Contains("StartFailed", result.Diagnostic, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies tool-missing start failures map to failure outcomes when no unmount command is available.
	/// </summary>
	[Fact]
	public void ApplyAction_Failure_ShouldReturnFailure_WhenNoUnmountCommandIsAvailable()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);
		MountReconciliationAction action = new(
			MountReconciliationActionKind.Unmount,
			"/ssm/merged/Title",
			desiredIdentity: null,
			mountPayload: null,
			MountReconciliationReason.StaleMount);

		MountActionApplyResult result = service.ApplyAction(
			action,
			"allow_other",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: false,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Failure, result.Outcome);
	}

	/// <summary>
	/// Verifies configured ionice/nice values are used for cleanup wrapper command execution.
	/// </summary>
	[Fact]
	public void UnmountMountPoint_Expected_ShouldUseConfiguredPriorityWrapperArguments_WhenCleanupHighPriorityEnabled()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);

		MountActionApplyResult result = service.UnmountMountPoint(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: true,
			cleanupPriorityIoniceClass: 2,
			cleanupPriorityNiceValue: -10);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Single(executor.Requests);
		Assert.Equal("ionice", executor.Requests[0].FileName);
		Assert.Equal("-c2", executor.Requests[0].Arguments[0]);
		Assert.Equal("nice", executor.Requests[0].Arguments[1]);
		Assert.Equal("-n", executor.Requests[0].Arguments[2]);
		Assert.Equal("-10", executor.Requests[0].Arguments[3]);
		Assert.Equal("fusermount3", executor.Requests[0].Arguments[4]);
	}

	/// <summary>
	/// Verifies wrapper-not-found failures fallback to plain command execution.
	/// </summary>
	[Fact]
	public void UnmountMountPoint_Edge_ShouldFallbackToPlainCommand_WhenPriorityWrapperToolIsUnavailable()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);

		MountActionApplyResult result = service.UnmountMountPoint(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: true,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Equal(2, executor.Requests.Count);
		Assert.Equal("ionice", executor.Requests[0].FileName);
		Assert.Equal("fusermount3", executor.Requests[1].FileName);
	}

	/// <summary>
	/// Verifies wrapper startup failures (non-tool-not-found) fallback to plain command execution.
	/// </summary>
	[Fact]
	public void UnmountMountPoint_Edge_ShouldFallbackToPlainCommand_WhenPriorityWrapperStartupFails()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.StartFailure,
				null,
				string.Empty,
				"failed to start ionice",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);

		MountActionApplyResult result = service.UnmountMountPoint(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: true,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Equal(2, executor.Requests.Count);
		Assert.Equal("ionice", executor.Requests[0].FileName);
		Assert.Equal("fusermount3", executor.Requests[1].FileName);
	}

	/// <summary>
	/// Verifies wrapper permission failures fallback to plain command execution.
	/// </summary>
	[Fact]
	public void UnmountMountPoint_Edge_ShouldFallbackToPlainCommand_WhenPriorityWrapperPermissionIsDenied()
	{
		RecordingCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				1,
				string.Empty,
				"ionice: ioprio_set failed: Operation not permitted",
				false,
				false,
				TimeSpan.FromMilliseconds(1)),
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(1)));
		MergerfsMountCommandService service = new(executor);

		MountActionApplyResult result = service.UnmountMountPoint(
			"/ssm/merged/Title",
			TimeSpan.FromSeconds(5),
			TimeSpan.FromMilliseconds(10),
			cleanupHighPriority: true,
			cleanupPriorityIoniceClass: 3,
			cleanupPriorityNiceValue: -20);

		Assert.Equal(MountActionApplyOutcome.Success, result.Outcome);
		Assert.Equal(2, executor.Requests.Count);
		Assert.Equal("ionice", executor.Requests[0].FileName);
		Assert.Equal("fusermount3", executor.Requests[1].FileName);
	}

	/// <summary>
	/// Verifies invalid cleanup priority values are rejected deterministically.
	/// </summary>
	[Fact]
	public void UnmountMountPoint_Failure_ShouldThrow_WhenCleanupPriorityValuesAreOutOfRange()
	{
		MergerfsMountCommandService service = new(new RecordingCommandExecutor());

		Assert.Throws<ArgumentOutOfRangeException>(
			() => service.UnmountMountPoint(
				"/ssm/merged/Title",
				TimeSpan.FromSeconds(5),
				TimeSpan.FromMilliseconds(10),
				cleanupHighPriority: true,
				cleanupPriorityIoniceClass: 0,
				cleanupPriorityNiceValue: -20));
	}

	/// <summary>
	/// Fake command executor that records requests and returns queued results.
	/// </summary>
	private sealed class RecordingCommandExecutor : IExternalCommandExecutor
	{
		/// <summary>
		/// Queued command results.
		/// </summary>
		private readonly Queue<ExternalCommandResult> _results;

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingCommandExecutor"/> class.
		/// </summary>
		/// <param name="results">Queued results.</param>
		public RecordingCommandExecutor(params ExternalCommandResult[] results)
		{
			_results = new Queue<ExternalCommandResult>(results);
			Requests = [];
		}

		/// <summary>
		/// Gets recorded requests.
		/// </summary>
		public List<ExternalCommandRequest> Requests
		{
			get;
		}

		/// <inheritdoc />
		public ExternalCommandResult Execute(ExternalCommandRequest request, CancellationToken cancellationToken = default)
		{
			Requests.Add(request);
			if (_results.Count == 0)
			{
				return new ExternalCommandResult(
					ExternalCommandOutcome.Success,
					ExternalCommandFailureKind.None,
					0,
					string.Empty,
					string.Empty,
					false,
					false,
					TimeSpan.Zero);
			}

			return _results.Dequeue();
		}
	}
}
