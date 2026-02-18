using SuwayomiSourceMerge.Infrastructure.Processes;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Applies mount reconciliation actions by invoking mergerfs and unmount commands.
/// </summary>
internal sealed class MergerfsMountCommandService : IMergerfsMountCommandService
{
	/// <summary>
	/// Error tokens indicating busy/device-busy command failures.
	/// </summary>
	private static readonly string[] _busyTokens =
	[
		"busy",
		"resource busy",
		"device or resource busy",
		"target is busy"
	];

	/// <summary>
	/// Error tokens indicating unavailable priority-wrapper tools.
	/// </summary>
	private static readonly string[] _toolUnavailableTokens =
	[
		"not found",
		"no such file"
	];

	/// <summary>
	/// Error tokens indicating wrapper execution lacks required permissions/capabilities.
	/// </summary>
	private static readonly string[] _wrapperPermissionDeniedTokens =
	[
		"permission denied",
		"operation not permitted",
		"cap_sys_nice"
	];

	/// <summary>
	/// Error tokens indicating mountpoint-not-found failures reported by mergerfs/fuse.
	/// </summary>
	private static readonly string[] _badMountPointTokens =
	[
		"bad mount point"
	];

	/// <summary>
	/// Command used for timeout-bounded mounted-path readiness probing.
	/// </summary>
	private const string ReadinessProbeCommand = "ls";

	/// <summary>
	/// Command executor dependency.
	/// </summary>
	private readonly IExternalCommandExecutor _commandExecutor;

	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsMountCommandService"/> class.
	/// </summary>
	public MergerfsMountCommandService()
		: this(new ExternalCommandExecutor())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="MergerfsMountCommandService"/> class.
	/// </summary>
	/// <param name="commandExecutor">Command executor dependency.</param>
	internal MergerfsMountCommandService(IExternalCommandExecutor commandExecutor)
	{
		_commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
	}

	/// <inheritdoc />
	public MountActionApplyResult ApplyAction(
		MountReconciliationAction action,
		string mergerfsOptionsBase,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		bool cleanupHighPriority,
		int cleanupPriorityIoniceClass,
		int cleanupPriorityNiceValue,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentException.ThrowIfNullOrWhiteSpace(mergerfsOptionsBase);
		ValidateTiming(commandTimeout, pollInterval);
		cancellationToken.ThrowIfCancellationRequested();

		return action.Kind switch
		{
			MountReconciliationActionKind.Mount => ExecuteMountCommand(
				action,
				mergerfsOptionsBase,
				commandTimeout,
				pollInterval,
				cancellationToken),
			MountReconciliationActionKind.Remount => ExecuteRemountCommand(
				action,
				mergerfsOptionsBase,
				commandTimeout,
				pollInterval,
				cleanupHighPriority,
				cleanupPriorityIoniceClass,
				cleanupPriorityNiceValue,
				cancellationToken),
			MountReconciliationActionKind.Unmount => UnmountMountPoint(
				action.MountPoint,
				commandTimeout,
				pollInterval,
				cleanupHighPriority,
				cleanupPriorityIoniceClass,
				cleanupPriorityNiceValue,
				cancellationToken),
			_ => throw new ArgumentOutOfRangeException(
				nameof(action),
				action.Kind,
				"Unsupported reconciliation action kind.")
		};
	}

	/// <inheritdoc />
	public MountActionApplyResult UnmountMountPoint(
		string mountPoint,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		bool cleanupHighPriority,
		int cleanupPriorityIoniceClass,
		int cleanupPriorityNiceValue,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPoint);
		ValidateTiming(commandTimeout, pollInterval);
		ValidateCleanupPriority(cleanupPriorityIoniceClass, cleanupPriorityNiceValue);
		cancellationToken.ThrowIfCancellationRequested();

		MountReconciliationAction action = new(
			MountReconciliationActionKind.Unmount,
			mountPoint,
			desiredIdentity: null,
			mountPayload: null,
			MountReconciliationReason.StaleMount);

		(MountActionApplyOutcome Outcome, string Diagnostic)? firstFailure = null;
		bool sawBusy = false;
		string? firstNonBusyFailureDiagnostic = null;

		ExternalCommandRequest[] requests =
		[
			CreateRequest("fusermount3", ["-uz", mountPoint], commandTimeout, pollInterval),
			CreateRequest("fusermount", ["-uz", mountPoint], commandTimeout, pollInterval),
			CreateRequest("umount", ["-l", mountPoint], commandTimeout, pollInterval)
		];

		for (int index = 0; index < requests.Length; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			ExternalCommandResult commandResult = ExecuteCommandWithOptionalPriority(
				requests[index],
				cleanupHighPriority,
				cleanupPriorityIoniceClass,
				cleanupPriorityNiceValue,
				cancellationToken);

			if (commandResult.Outcome == ExternalCommandOutcome.StartFailed &&
				commandResult.FailureKind == ExternalCommandFailureKind.ToolNotFound)
			{
				continue;
			}

			(MountActionApplyOutcome outcome, string diagnostic) = ClassifyCommandResult(commandResult);
			if (outcome == MountActionApplyOutcome.Success)
			{
				return new MountActionApplyResult(action, MountActionApplyOutcome.Success, diagnostic);
			}

			sawBusy = sawBusy || outcome == MountActionApplyOutcome.Busy;
			if (outcome == MountActionApplyOutcome.Failure)
			{
				firstNonBusyFailureDiagnostic ??= diagnostic;
			}
			firstFailure ??= (outcome, diagnostic);
		}

		if (firstFailure.HasValue)
		{
			if (!string.IsNullOrWhiteSpace(firstNonBusyFailureDiagnostic))
			{
				return new MountActionApplyResult(
					action,
					MountActionApplyOutcome.Failure,
					firstNonBusyFailureDiagnostic);
			}

			MountActionApplyOutcome finalOutcome = sawBusy
				? MountActionApplyOutcome.Busy
				: firstFailure.Value.Outcome;
			return new MountActionApplyResult(action, finalOutcome, firstFailure.Value.Diagnostic);
		}

		return new MountActionApplyResult(
			action,
			MountActionApplyOutcome.Failure,
			"No unmount command was available on PATH.");
	}

	/// <inheritdoc />
	public MountReadinessProbeResult ProbeMountPointReadiness(
		string mountPoint,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPoint);
		ValidateTiming(commandTimeout, pollInterval);
		cancellationToken.ThrowIfCancellationRequested();

		ExternalCommandRequest request = CreateRequest(
			ReadinessProbeCommand,
			["-A", mountPoint],
			commandTimeout,
			pollInterval);
		ExternalCommandResult commandResult = _commandExecutor.Execute(request, cancellationToken);
		if (commandResult.Outcome == ExternalCommandOutcome.Success)
		{
			return MountReadinessProbeResult.Ready("Readiness probe command succeeded.");
		}

		if (commandResult.Outcome == ExternalCommandOutcome.TimedOut)
		{
			return MountReadinessProbeResult.NotReady("Readiness probe command timed out.");
		}

		if (commandResult.Outcome == ExternalCommandOutcome.Cancelled)
		{
			return MountReadinessProbeResult.NotReady("Readiness probe command was cancelled.");
		}

		if (commandResult.Outcome == ExternalCommandOutcome.NonZeroExit)
		{
			string diagnostic = string.IsNullOrWhiteSpace(commandResult.StandardError)
				? commandResult.StandardOutput.Trim()
				: commandResult.StandardError.Trim();
			return MountReadinessProbeResult.NotReady(
				$"Readiness probe command exited non-zero ({commandResult.ExitCode?.ToString() ?? "<none>"}): {diagnostic}");
		}

		return MountReadinessProbeResult.NotReady(
			$"Readiness probe command failed: outcome={commandResult.Outcome} failure_kind={commandResult.FailureKind}.");
	}

	/// <summary>
	/// Executes one mount command action.
	/// </summary>
	/// <param name="action">Mount action.</param>
	/// <param name="mergerfsOptionsBase">Base mergerfs options string.</param>
	/// <param name="commandTimeout">Per-command timeout.</param>
	/// <param name="pollInterval">Per-command process poll interval.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Apply result.</returns>
	private MountActionApplyResult ExecuteMountCommand(
		MountReconciliationAction action,
		string mergerfsOptionsBase,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		CancellationToken cancellationToken)
	{
		if (!TryEnsureMountPointDirectory(action.MountPoint, out string ensureDiagnostic))
		{
			return new MountActionApplyResult(action, MountActionApplyOutcome.Failure, ensureDiagnostic);
		}

		string options = MergerfsOptionComposer.ComposeMountOptions(mergerfsOptionsBase, action.DesiredIdentity!);
		ExternalCommandRequest request = CreateRequest(
			"mergerfs",
			["-o", options, action.MountPayload!, action.MountPoint],
			commandTimeout,
			pollInterval);

		ExternalCommandResult commandResult = _commandExecutor.Execute(request, cancellationToken);
		(MountActionApplyOutcome outcome, string diagnostic) = ClassifyCommandResult(commandResult);
		if (ShouldRetryMountAfterBadMountPoint(commandResult))
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (!TryEnsureMountPointDirectory(action.MountPoint, out string retryEnsureDiagnostic))
			{
				return new MountActionApplyResult(
					action,
					MountActionApplyOutcome.Failure,
					$"Initial mount failed with bad mount point. Retry mountpoint directory creation also failed. initial_mount='{diagnostic}' retry_directory_creation='{retryEnsureDiagnostic}'");
			}

			ExternalCommandResult retryResult = _commandExecutor.Execute(request, cancellationToken);
			(MountActionApplyOutcome retryOutcome, string retryDiagnostic) = ClassifyCommandResult(retryResult);
			if (retryOutcome == MountActionApplyOutcome.Success)
			{
				return new MountActionApplyResult(action, MountActionApplyOutcome.Success, "Command succeeded after bad-mountpoint retry.");
			}

			return new MountActionApplyResult(
				action,
				retryOutcome,
				$"Mount failed after bad-mountpoint retry. initial='{diagnostic}' retry='{retryDiagnostic}'");
		}

		return new MountActionApplyResult(action, outcome, diagnostic);
	}

	/// <summary>
	/// Determines whether a mount command should be retried after a bad-mountpoint failure.
	/// </summary>
	/// <param name="commandResult">Command result to inspect.</param>
	/// <returns><see langword="true"/> when a retry should be attempted; otherwise <see langword="false"/>.</returns>
	private static bool ShouldRetryMountAfterBadMountPoint(ExternalCommandResult commandResult)
	{
		ArgumentNullException.ThrowIfNull(commandResult);
		if (commandResult.Outcome != ExternalCommandOutcome.NonZeroExit)
		{
			return false;
		}

		return ContainsAnyToken(commandResult.StandardError, _badMountPointTokens);
	}

	/// <summary>
	/// Ensures the action mountpoint directory exists before invoking mergerfs.
	/// </summary>
	/// <param name="mountPoint">Mountpoint directory path.</param>
	/// <param name="diagnostic">Failure diagnostic text.</param>
	/// <returns><see langword="true"/> when the directory exists or is created; otherwise <see langword="false"/>.</returns>
	private static bool TryEnsureMountPointDirectory(string mountPoint, out string diagnostic)
	{
		try
		{
			_ = Directory.CreateDirectory(mountPoint);
			diagnostic = string.Empty;
			return true;
		}
		catch (Exception exception)
		{
			diagnostic = $"Failed to ensure mountpoint directory '{mountPoint}': {exception.GetType().Name}: {exception.Message}";
			return false;
		}
	}

	/// <summary>
	/// Executes one remount action as unmount then mount.
	/// </summary>
	/// <param name="action">Remount action.</param>
	/// <param name="mergerfsOptionsBase">Base mergerfs options string.</param>
	/// <param name="commandTimeout">Per-command timeout.</param>
	/// <param name="pollInterval">Per-command process poll interval.</param>
	/// <param name="cleanupHighPriority">Whether cleanup priority wrappers should be attempted for unmount.</param>
	/// <param name="cleanupPriorityIoniceClass">Ionice class value used for cleanup wrapper execution.</param>
	/// <param name="cleanupPriorityNiceValue">Nice value used for cleanup wrapper execution.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Apply result.</returns>
	private MountActionApplyResult ExecuteRemountCommand(
		MountReconciliationAction action,
		string mergerfsOptionsBase,
		TimeSpan commandTimeout,
		TimeSpan pollInterval,
		bool cleanupHighPriority,
		int cleanupPriorityIoniceClass,
		int cleanupPriorityNiceValue,
		CancellationToken cancellationToken)
	{
		MountActionApplyResult unmountResult = UnmountMountPoint(
			action.MountPoint,
			commandTimeout,
			pollInterval,
			cleanupHighPriority,
			cleanupPriorityIoniceClass,
			cleanupPriorityNiceValue,
			cancellationToken);
		if (unmountResult.Outcome != MountActionApplyOutcome.Success)
		{
			return new MountActionApplyResult(
				action,
				unmountResult.Outcome,
				$"Remount unmount phase failed: {unmountResult.Diagnostic}");
		}

		MountActionApplyResult mountResult = ExecuteMountCommand(
			action,
			mergerfsOptionsBase,
			commandTimeout,
			pollInterval,
			cancellationToken);
		if (mountResult.Outcome == MountActionApplyOutcome.Success)
		{
			return mountResult;
		}

		return new MountActionApplyResult(
			action,
			mountResult.Outcome,
			$"Remount mount phase failed: {mountResult.Diagnostic}");
	}

	/// <summary>
	/// Executes one command optionally wrapped with best-effort cleanup priority helpers.
	/// </summary>
	/// <param name="request">Command request.</param>
	/// <param name="cleanupHighPriority">Whether cleanup priority wrappers should be attempted.</param>
	/// <param name="cleanupPriorityIoniceClass">Ionice class value used for cleanup wrapper execution.</param>
	/// <param name="cleanupPriorityNiceValue">Nice value used for cleanup wrapper execution.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Command result.</returns>
	private ExternalCommandResult ExecuteCommandWithOptionalPriority(
		ExternalCommandRequest request,
		bool cleanupHighPriority,
		int cleanupPriorityIoniceClass,
		int cleanupPriorityNiceValue,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateCleanupPriority(cleanupPriorityIoniceClass, cleanupPriorityNiceValue);

		if (!cleanupHighPriority)
		{
			return _commandExecutor.Execute(request, cancellationToken);
		}

		List<string> arguments = [$"-c{cleanupPriorityIoniceClass}", "nice", "-n", cleanupPriorityNiceValue.ToString(), request.FileName];
		arguments.AddRange(request.Arguments);
		ExternalCommandRequest priorityRequest = CreateRequest(
			"ionice",
			arguments,
			request.Timeout,
			request.PollInterval);

		ExternalCommandResult priorityResult = _commandExecutor.Execute(priorityRequest, cancellationToken);
		if (!ShouldFallbackFromPriorityWrapper(priorityResult))
		{
			return priorityResult;
		}

		return _commandExecutor.Execute(request, cancellationToken);
	}

	/// <summary>
	/// Determines whether to fallback from a cleanup-priority wrapper to the plain command.
	/// </summary>
	/// <param name="priorityResult">Priority wrapper command result.</param>
	/// <returns><see langword="true"/> when the plain command should be retried; otherwise <see langword="false"/>.</returns>
	private static bool ShouldFallbackFromPriorityWrapper(ExternalCommandResult priorityResult)
	{
		ArgumentNullException.ThrowIfNull(priorityResult);

		if (priorityResult.Outcome == ExternalCommandOutcome.StartFailed)
		{
			return true;
		}

		if (priorityResult.Outcome != ExternalCommandOutcome.NonZeroExit)
		{
			return false;
		}

		string combined = string.Concat(priorityResult.StandardOutput, " ", priorityResult.StandardError);
		return ContainsAnyToken(combined, _toolUnavailableTokens) ||
			ContainsAnyToken(combined, _wrapperPermissionDeniedTokens);
	}

	/// <summary>
	/// Returns whether input text contains any token from the provided list.
	/// </summary>
	/// <param name="input">Input text to inspect.</param>
	/// <param name="tokens">Token list.</param>
	/// <returns><see langword="true"/> when any token is present; otherwise <see langword="false"/>.</returns>
	private static bool ContainsAnyToken(string input, IReadOnlyList<string> tokens)
	{
		for (int index = 0; index < tokens.Count; index++)
		{
			if (input.Contains(tokens[index], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Validates cleanup priority wrapper arguments.
	/// </summary>
	/// <param name="cleanupPriorityIoniceClass">Ionice class value.</param>
	/// <param name="cleanupPriorityNiceValue">Nice value.</param>
	private static void ValidateCleanupPriority(int cleanupPriorityIoniceClass, int cleanupPriorityNiceValue)
	{
		if (cleanupPriorityIoniceClass < 1 || cleanupPriorityIoniceClass > 3)
		{
			throw new ArgumentOutOfRangeException(nameof(cleanupPriorityIoniceClass), cleanupPriorityIoniceClass, "Ionice class must be between 1 and 3.");
		}

		if (cleanupPriorityNiceValue < -20 || cleanupPriorityNiceValue > 19)
		{
			throw new ArgumentOutOfRangeException(nameof(cleanupPriorityNiceValue), cleanupPriorityNiceValue, "Nice value must be between -20 and 19.");
		}
	}

	/// <summary>
	/// Classifies one command result into mount-apply outcome and diagnostic.
	/// </summary>
	/// <param name="commandResult">Command result.</param>
	/// <returns>Outcome and diagnostic text.</returns>
	private static (MountActionApplyOutcome Outcome, string Diagnostic) ClassifyCommandResult(ExternalCommandResult commandResult)
	{
		ArgumentNullException.ThrowIfNull(commandResult);

		if (commandResult.Outcome == ExternalCommandOutcome.Success)
		{
			return (MountActionApplyOutcome.Success, "Command succeeded.");
		}

		if (commandResult.Outcome == ExternalCommandOutcome.TimedOut ||
			commandResult.Outcome == ExternalCommandOutcome.Cancelled)
		{
			return (MountActionApplyOutcome.Busy, $"Command {commandResult.Outcome}.");
		}

		if (commandResult.Outcome == ExternalCommandOutcome.NonZeroExit)
		{
			string stderr = commandResult.StandardError.Trim();
			if (ContainsBusyToken(stderr))
			{
				return (MountActionApplyOutcome.Busy, $"Command reported busy state: {stderr}");
			}

			return (
				MountActionApplyOutcome.Failure,
				$"Command exited non-zero ({commandResult.ExitCode?.ToString() ?? "<none>"}): {stderr}");
		}

		return (
			MountActionApplyOutcome.Failure,
			$"Command failed: outcome={commandResult.Outcome} failure_kind={commandResult.FailureKind}.");
	}

	/// <summary>
	/// Determines whether stderr text indicates a busy failure.
	/// </summary>
	/// <param name="text">Diagnostic text.</param>
	/// <returns><see langword="true"/> when busy tokens are present; otherwise <see langword="false"/>.</returns>
	private static bool ContainsBusyToken(string text)
	{
		for (int index = 0; index < _busyTokens.Length; index++)
		{
			if (text.Contains(_busyTokens[index], StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Validates command timeout and poll interval arguments.
	/// </summary>
	/// <param name="commandTimeout">Command timeout.</param>
	/// <param name="pollInterval">Command poll interval.</param>
	private static void ValidateTiming(TimeSpan commandTimeout, TimeSpan pollInterval)
	{
		if (commandTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(commandTimeout), commandTimeout, "Command timeout must be > 0.");
		}

		if (pollInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Poll interval must be > 0.");
		}
	}

	/// <summary>
	/// Creates one command request instance.
	/// </summary>
	/// <param name="fileName">Executable file name.</param>
	/// <param name="arguments">Command arguments.</param>
	/// <param name="commandTimeout">Command timeout.</param>
	/// <param name="pollInterval">Command poll interval.</param>
	/// <returns>Command request instance.</returns>
	private static ExternalCommandRequest CreateRequest(
		string fileName,
		IReadOnlyList<string> arguments,
		TimeSpan commandTimeout,
		TimeSpan pollInterval)
	{
		return new ExternalCommandRequest
		{
			FileName = fileName,
			Arguments = arguments,
			Timeout = commandTimeout,
			PollInterval = pollInterval
		};
	}
}
