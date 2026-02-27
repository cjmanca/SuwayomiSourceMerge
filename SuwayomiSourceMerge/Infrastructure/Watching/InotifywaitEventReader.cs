using System.Globalization;

using SuwayomiSourceMerge.Infrastructure.Processes;

namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Uses one-shot <c>inotifywait</c> polling to collect filesystem events.
/// </summary>
internal sealed class InotifywaitEventReader : IInotifyEventReader
{
	/// <summary>
	/// Poll interval passed to the process executor.
	/// </summary>
	private static readonly TimeSpan _executorPollInterval = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Default additional timeout buffer applied above inotify timeout.
	/// </summary>
	private const int DefaultRequestTimeoutBufferSeconds = 300;

	/// <summary>
	/// Maximum captured output characters per stream for one poll.
	/// </summary>
	private const int MaxOutputCharacters = 1_048_576;

	/// <summary>
	/// Exit code returned by inotifywait when no events occurred before timeout.
	/// </summary>
	private const int InotifyTimeoutExitCode = 2;

	/// <summary>
	/// Event names passed to inotifywait.
	/// </summary>
	private static readonly string[] _watchEvents =
	[
		"create",
		"moved_to",
		"close_write",
		"attrib",
		"delete",
		"moved_from"
	];

	/// <summary>
	/// External process executor dependency.
	/// </summary>
	private readonly IExternalCommandExecutor _commandExecutor;

	/// <summary>
	/// Additional timeout buffer applied above requested inotify timeout.
	/// </summary>
	private readonly TimeSpan _requestTimeoutBuffer;

	/// <summary>
	/// Initializes a new instance of the <see cref="InotifywaitEventReader"/> class.
	/// </summary>
	/// <param name="commandExecutor">Process executor used to invoke <c>inotifywait</c>.</param>
	/// <param name="requestTimeoutBufferSeconds">Additional timeout buffer in seconds for each inotify command request.</param>
	public InotifywaitEventReader(
		IExternalCommandExecutor commandExecutor,
		int requestTimeoutBufferSeconds = DefaultRequestTimeoutBufferSeconds)
	{
		_commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));

		if (requestTimeoutBufferSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(requestTimeoutBufferSeconds), "Request timeout buffer seconds must be > 0.");
		}

		_requestTimeoutBuffer = TimeSpan.FromSeconds(requestTimeoutBufferSeconds);
	}

	/// <inheritdoc />
	public InotifyPollResult Poll(
		IReadOnlyList<string> watchRoots,
		TimeSpan timeout,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(watchRoots);
		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be > 0.");
		}

		List<string> warnings = [];
		string[] normalizedRoots = NormalizeWatchRoots(watchRoots, warnings);
		if (normalizedRoots.Length == 0)
		{
			return new InotifyPollResult(
				InotifyPollOutcome.Success,
				[],
				warnings);
		}

		List<string> existingRoots = [];
		for (int index = 0; index < normalizedRoots.Length; index++)
		{
			string root = normalizedRoots[index];
			if (Directory.Exists(root))
			{
				existingRoots.Add(root);
			}
			else
			{
				warnings.Add($"Skipping missing watch root: {root}");
			}
		}

		if (existingRoots.Count == 0)
		{
			warnings.Add("No existing watch roots were available for inotify polling.");
			return new InotifyPollResult(
				InotifyPollOutcome.Success,
				[],
				warnings);
		}

		ExternalCommandResult commandResult = _commandExecutor.Execute(
			BuildRequest(existingRoots.ToArray(), timeout),
			cancellationToken);

		InotifyPollResult pollResult = BuildPollResult(commandResult);
		if (warnings.Count == 0)
		{
			return pollResult;
		}

		List<string> mergedWarnings = new(warnings.Count + pollResult.Warnings.Count);
		mergedWarnings.AddRange(warnings);
		mergedWarnings.AddRange(pollResult.Warnings);
		return new InotifyPollResult(pollResult.Outcome, pollResult.Events, mergedWarnings);
	}

	/// <summary>
	/// Builds one external-command request for inotifywait polling.
	/// </summary>
	/// <param name="watchRoots">Normalized watch roots.</param>
	/// <param name="timeout">Requested inotify timeout.</param>
	/// <returns>External command request.</returns>
	private ExternalCommandRequest BuildRequest(string[] watchRoots, TimeSpan timeout)
	{
		int timeoutSeconds = (int)Math.Ceiling(timeout.TotalSeconds);
		if (timeoutSeconds <= 0)
		{
			timeoutSeconds = 1;
		}

		List<string> arguments = new(20 + watchRoots.Length)
		{
			"-qq",
			"-r",
			"--timeout",
			timeoutSeconds.ToString(CultureInfo.InvariantCulture)
		};

		for (int index = 0; index < _watchEvents.Length; index++)
		{
			arguments.Add("-e");
			arguments.Add(_watchEvents[index]);
		}

		arguments.Add("--format");
		arguments.Add("%w%f|%e");
		arguments.AddRange(watchRoots);

		return new ExternalCommandRequest
		{
			FileName = "inotifywait",
			Arguments = arguments,
			Timeout = timeout + _requestTimeoutBuffer,
			PollInterval = _executorPollInterval,
			MaxOutputCharacters = MaxOutputCharacters
		};
	}

	/// <summary>
	/// Converts one command result into a typed poll result.
	/// </summary>
	/// <param name="commandResult">Command execution result.</param>
	/// <returns>Typed poll result.</returns>
	private static InotifyPollResult BuildPollResult(ExternalCommandResult commandResult)
	{
		List<string> warnings = [];
		List<InotifyEventRecord> events = [];
		ParseOutput(commandResult.StandardOutput, events, warnings);

		if (commandResult.Outcome == ExternalCommandOutcome.Success)
		{
			return new InotifyPollResult(InotifyPollOutcome.Success, events, warnings);
		}

		if (commandResult.Outcome == ExternalCommandOutcome.NonZeroExit &&
			commandResult.ExitCode == InotifyTimeoutExitCode &&
			events.Count == 0)
		{
			return new InotifyPollResult(InotifyPollOutcome.TimedOut, events, warnings);
		}

		if (commandResult.Outcome == ExternalCommandOutcome.TimedOut &&
			string.IsNullOrWhiteSpace(commandResult.StandardError))
		{
			return new InotifyPollResult(InotifyPollOutcome.TimedOut, events, warnings);
		}

		if (commandResult.Outcome == ExternalCommandOutcome.StartFailed &&
			commandResult.FailureKind == ExternalCommandFailureKind.ToolNotFound)
		{
			warnings.Add("inotifywait executable was not found.");
			return new InotifyPollResult(InotifyPollOutcome.ToolNotFound, events, warnings);
		}

		warnings.Add(BuildCommandFailureWarning(commandResult));
		if (!string.IsNullOrWhiteSpace(commandResult.StandardError))
		{
			warnings.Add(commandResult.StandardError.Trim());
		}

		return new InotifyPollResult(InotifyPollOutcome.CommandFailed, events, warnings);
	}

	/// <summary>
	/// Builds one concise command-failure warning string.
	/// </summary>
	/// <param name="commandResult">Command result.</param>
	/// <returns>Warning text.</returns>
	private static string BuildCommandFailureWarning(ExternalCommandResult commandResult)
	{
		string exitCodeText = commandResult.ExitCode?.ToString(CultureInfo.InvariantCulture) ?? "n/a";
		return string.Create(
			CultureInfo.InvariantCulture,
			$"inotifywait poll failed (outcome={commandResult.Outcome}, failure_kind={commandResult.FailureKind}, exit_code={exitCodeText}).");
	}

	/// <summary>
	/// Parses one stdout payload into event records.
	/// </summary>
	/// <param name="standardOutput">Raw standard output text.</param>
	/// <param name="events">Parsed event sink.</param>
	/// <param name="warnings">Warning sink.</param>
	private static void ParseOutput(
		string standardOutput,
		ICollection<InotifyEventRecord> events,
		ICollection<string> warnings)
	{
		if (string.IsNullOrWhiteSpace(standardOutput))
		{
			return;
		}

		string[] lines = standardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		for (int index = 0; index < lines.Length; index++)
		{
			string line = lines[index];
			if (InotifyEventRecord.TryParse(line, out InotifyEventRecord? record) && record is not null)
			{
				events.Add(record);
				continue;
			}

			warnings.Add($"Ignoring malformed inotify output line at index {index}.");
		}
	}

	/// <summary>
	/// Normalizes and de-duplicates watch roots.
	/// </summary>
	/// <param name="watchRoots">Raw watch roots.</param>
	/// <param name="warnings">Warning sink for invalid watch-root inputs.</param>
	/// <returns>Normalized watch-root paths.</returns>
	private static string[] NormalizeWatchRoots(IReadOnlyList<string> watchRoots, ICollection<string> warnings)
	{
		StringComparer comparer = OperatingSystem.IsWindows()
			? StringComparer.OrdinalIgnoreCase
			: StringComparer.Ordinal;
		HashSet<string> roots = new(comparer);
		for (int index = 0; index < watchRoots.Count; index++)
		{
			string? root = watchRoots[index];
			if (string.IsNullOrWhiteSpace(root))
			{
				continue;
			}

			try
			{
				string normalized = Path.GetFullPath(Path.TrimEndingDirectorySeparator(root));
				if (!string.IsNullOrWhiteSpace(normalized))
				{
					roots.Add(normalized);
				}
			}
			catch (Exception exception) when (!IsFatalException(exception))
			{
				warnings.Add($"Ignoring invalid watch root '{root}': {exception.GetType().Name}.");
			}
		}

		return roots.OrderBy(static path => path, StringComparer.Ordinal).ToArray();
	}

	/// <summary>
	/// Determines whether an exception is fatal and must never be swallowed.
	/// </summary>
	/// <param name="exception">Exception instance to classify.</param>
	/// <returns><see langword="true"/> when fatal; otherwise <see langword="false"/>.</returns>
	private static bool IsFatalException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return exception is OutOfMemoryException
			|| exception is StackOverflowException
			|| exception is AccessViolationException;
	}
}
