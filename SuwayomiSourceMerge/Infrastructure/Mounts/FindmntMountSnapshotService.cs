using SuwayomiSourceMerge.Infrastructure.Processes;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Captures mount snapshots using the <c>findmnt</c> command.
/// </summary>
internal sealed class FindmntMountSnapshotService : IMountSnapshotService
{
	/// <summary>
	/// Warning code emitted when <c>findmnt</c> command execution fails.
	/// </summary>
	private const string COMMAND_FAILURE_WARNING_CODE = "MOUNT-SNAP-001";

	/// <summary>
	/// Warning code emitted when a <c>findmnt -P</c> line cannot be parsed safely.
	/// </summary>
	private const string PARSE_WARNING_CODE = "MOUNT-SNAP-002";

	/// <summary>
	/// Command name used to capture mount state.
	/// </summary>
	private const string FINDMNT_COMMAND = "findmnt";

	/// <summary>
	/// Maximum warning diagnostic text length.
	/// </summary>
	private const int MAX_WARNING_TEXT_LENGTH = 256;

	/// <summary>
	/// Shared command argument list used for snapshot capture.
	/// </summary>
	private static readonly IReadOnlyList<string> FINDMNT_ARGUMENTS = ["-rn", "-P", "-o", "TARGET,FSTYPE,SOURCE,OPTIONS"];

	/// <summary>
	/// Default command timeout used for snapshot capture.
	/// </summary>
	private static readonly TimeSpan DEFAULT_TIMEOUT = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Default command polling interval used for snapshot capture.
	/// </summary>
	private static readonly TimeSpan DEFAULT_POLL_INTERVAL = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Default output capture size limit used for snapshot capture.
	/// </summary>
	private const int DEFAULT_MAX_OUTPUT_CHARACTERS = 65536;

	/// <summary>
	/// Command executor used to run <c>findmnt</c>.
	/// </summary>
	private readonly IExternalCommandExecutor _commandExecutor;

	/// <summary>
	/// Timeout used for <c>findmnt</c> command execution.
	/// </summary>
	private readonly TimeSpan _timeout;

	/// <summary>
	/// Poll interval used for <c>findmnt</c> command execution.
	/// </summary>
	private readonly TimeSpan _pollInterval;

	/// <summary>
	/// Output capture limit used for <c>findmnt</c> command execution.
	/// </summary>
	private readonly int _maxOutputCharacters;

	/// <summary>
	/// Initializes a new instance of the <see cref="FindmntMountSnapshotService"/> class.
	/// </summary>
	public FindmntMountSnapshotService()
		: this(
			new ExternalCommandExecutor(),
			DEFAULT_TIMEOUT,
			DEFAULT_POLL_INTERVAL,
			DEFAULT_MAX_OUTPUT_CHARACTERS)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FindmntMountSnapshotService"/> class with explicit command execution settings.
	/// </summary>
	/// <param name="commandExecutor">Command executor used to run <c>findmnt</c>.</param>
	/// <param name="timeout">Snapshot command timeout.</param>
	/// <param name="pollInterval">Snapshot command poll interval.</param>
	/// <param name="maxOutputCharacters">Maximum captured characters per output stream.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="commandExecutor"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when numeric/timing inputs are not positive.</exception>
	internal FindmntMountSnapshotService(
		IExternalCommandExecutor commandExecutor,
		TimeSpan timeout,
		TimeSpan pollInterval,
		int maxOutputCharacters)
	{
		_commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));

		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
		}

		if (pollInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Poll interval must be greater than zero.");
		}

		if (maxOutputCharacters <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maxOutputCharacters),
				maxOutputCharacters,
				"Max output characters must be greater than zero.");
		}

		_timeout = timeout;
		_pollInterval = pollInterval;
		_maxOutputCharacters = maxOutputCharacters;
	}

	/// <inheritdoc />
	public MountSnapshot Capture()
	{
		ExternalCommandResult commandResult = _commandExecutor.Execute(
			new ExternalCommandRequest
			{
				FileName = FINDMNT_COMMAND,
				Arguments = FINDMNT_ARGUMENTS,
				Timeout = _timeout,
				PollInterval = _pollInterval,
				MaxOutputCharacters = _maxOutputCharacters
			});

		if (commandResult.Outcome != ExternalCommandOutcome.Success)
		{
			return new MountSnapshot(
				[],
				[
					BuildCommandFailureWarning(commandResult)
				]);
		}

		List<MountSnapshotEntry> entries = [];
		List<MountSnapshotWarning> warnings = [];
		string[] lines = commandResult.StandardOutput.Split('\n');

		for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
		{
			string line = lines[lineIndex].TrimEnd('\r');
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			if (FindmntSnapshotLineParser.TryParse(line, out MountSnapshotEntry? entry, out string? warningMessage))
			{
				entries.Add(entry!);
				continue;
			}

			warnings.Add(
				new MountSnapshotWarning(
					PARSE_WARNING_CODE,
					$"Skipped malformed findmnt output line {lineIndex + 1}: {warningMessage}."));
		}

		MountSnapshotEntry[] orderedEntries = entries
			.OrderBy(entry => entry.MountPoint, StringComparer.Ordinal)
			.ToArray();

		return new MountSnapshot(orderedEntries, warnings);
	}

	/// <summary>
	/// Builds a warning payload for a failed <c>findmnt</c> command execution.
	/// </summary>
	/// <param name="commandResult">Failed command result.</param>
	/// <returns>Warning instance describing the command failure.</returns>
	private static MountSnapshotWarning BuildCommandFailureWarning(ExternalCommandResult commandResult)
	{
		ArgumentNullException.ThrowIfNull(commandResult);

		string stderrDiagnostic = BuildTrimmedDiagnostic(commandResult.StandardError);
		string message =
			$"findmnt snapshot capture failed: outcome={commandResult.Outcome} failure_kind={commandResult.FailureKind} exit_code={commandResult.ExitCode?.ToString() ?? "<none>"} stderr={stderrDiagnostic}";
		return new MountSnapshotWarning(COMMAND_FAILURE_WARNING_CODE, message);
	}

	/// <summary>
	/// Trims diagnostics for warning readability.
	/// </summary>
	/// <param name="value">Diagnostic text to trim.</param>
	/// <returns>Single-line, length-bounded warning diagnostic text.</returns>
	private static string BuildTrimmedDiagnostic(string value)
	{
		ArgumentNullException.ThrowIfNull(value);

		string singleLine = value
			.Replace('\r', ' ')
			.Replace('\n', ' ')
			.Trim();
		if (singleLine.Length <= MAX_WARNING_TEXT_LENGTH)
		{
			return singleLine;
		}

		return $"{singleLine[..MAX_WARNING_TEXT_LENGTH]}...";
	}
}
