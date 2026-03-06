using System.ComponentModel;
using System.Diagnostics;

using SuwayomiSourceMerge.Infrastructure.Processes;

namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Captures mount snapshots using the <c>findmnt</c> command.
/// </summary>
internal sealed class FindmntMountSnapshotService : IMountSnapshotService
{
	/// <summary>
	/// Command name used to capture mount state.
	/// </summary>
	private const string FindmntCommand = "findmnt";

	/// <summary>
	/// Character count used when trimming warning diagnostics.
	/// </summary>
	private const int MaxWarningTextLength = 256;

	/// <summary>
	/// Minimum best-effort wait used when draining output tasks after timeout kill.
	/// </summary>
	private const int MinCaptureWaitMilliseconds = 50;

	/// <summary>
	/// Shared command argument list used for snapshot capture.
	/// </summary>
	private static readonly IReadOnlyList<string> _findmntArguments = ["-n", "-P", "-o", "TARGET,FSTYPE,SOURCE,OPTIONS"];

	/// <summary>
	/// Default command timeout used for snapshot capture.
	/// </summary>
	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Default command polling interval used for snapshot capture.
	/// </summary>
	private static readonly TimeSpan _defaultPollInterval = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Factory used to create one process facade per snapshot capture.
	/// </summary>
	private readonly Func<IProcessFacade> _processFactory;

	/// <summary>
	/// Timeout used for <c>findmnt</c> command execution.
	/// </summary>
	private readonly TimeSpan _timeout;

	/// <summary>
	/// Poll interval used for <c>findmnt</c> command execution.
	/// </summary>
	private readonly TimeSpan _pollInterval;

	/// <summary>
	/// Initializes a new instance of the <see cref="FindmntMountSnapshotService"/> class.
	/// </summary>
	public FindmntMountSnapshotService()
		: this(
			static () => new SystemProcessFacade(),
			_defaultTimeout,
			_defaultPollInterval)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FindmntMountSnapshotService"/> class with explicit process settings.
	/// </summary>
	/// <param name="processFactory">Process-facade factory used for snapshot capture.</param>
	/// <param name="timeout">Snapshot command timeout.</param>
	/// <param name="pollInterval">Snapshot command poll interval.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="processFactory"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when timing inputs are not positive.</exception>
	internal FindmntMountSnapshotService(
		Func<IProcessFacade> processFactory,
		TimeSpan timeout,
		TimeSpan pollInterval)
	{
		_processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));

		if (timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than zero.");
		}

		if (pollInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(pollInterval), pollInterval, "Poll interval must be greater than zero.");
		}

		_timeout = timeout;
		_pollInterval = pollInterval;
	}

	/// <inheritdoc />
	public MountSnapshot Capture()
	{
		List<MountSnapshotEntry> entries = [];
		List<MountSnapshotWarning> warnings = [];
		Stopwatch stopwatch = Stopwatch.StartNew();

		using IProcessFacade process = _processFactory();
		process.ConfigureStartInfo(BuildStartInfo());

		if (!TryStartProcess(process, out ExternalCommandOutcome startOutcome, out ExternalCommandFailureKind startFailureKind))
		{
			return new MountSnapshot(
				[],
				[
					BuildCommandFailureWarning(startOutcome, startFailureKind, exitCode: null, string.Empty)
				]);
		}

		Task<string> stderrCaptureTask = Task.Run(() => CaptureStreamToEnd(process.StandardErrorReader));
		Task outputCaptureTask = Task.Run(() => CaptureAndParseOutputLines(process.StandardOutputReader, entries, warnings));

		bool exited = TryWaitForExit(process, stopwatch, out ExternalCommandOutcome terminalOutcome);
		if (!exited)
		{
			TryKillProcess(process);
		}

		if (exited)
		{
			WaitForCaptureCompletion(outputCaptureTask, maxWaitMilliseconds: null);
			WaitForCaptureCompletion(stderrCaptureTask, maxWaitMilliseconds: null);
		}
		else
		{
			int captureWaitMilliseconds = CalculateCaptureWaitMilliseconds(_pollInterval);
			WaitForCaptureCompletion(outputCaptureTask, captureWaitMilliseconds);
			WaitForCaptureCompletion(stderrCaptureTask, captureWaitMilliseconds);
		}

		string standardError = stderrCaptureTask.Status == TaskStatus.RanToCompletion
			? stderrCaptureTask.Result
			: string.Empty;
		if (outputCaptureTask.IsFaulted)
		{
			terminalOutcome = ExternalCommandOutcome.NonZeroExit;
			standardError = string.IsNullOrWhiteSpace(standardError)
				? $"findmnt output capture failed: {BuildTrimmedDiagnostic(outputCaptureTask.Exception?.GetBaseException().Message ?? "unknown error")}"
				: standardError;
		}

		if (!exited)
		{
			return new MountSnapshot(
				[],
				[
					BuildCommandFailureWarning(terminalOutcome, ExternalCommandFailureKind.None, exitCode: null, standardError)
				]);
		}

		if (process.ExitCode != 0)
		{
			return new MountSnapshot(
				[],
				[
					BuildCommandFailureWarning(ExternalCommandOutcome.NonZeroExit, ExternalCommandFailureKind.None, process.ExitCode, standardError)
				]);
		}

		MountSnapshotEntry[] orderedEntries = entries
			.OrderBy(static entry => entry.MountPoint, StringComparer.Ordinal)
			.ToArray();
		return new MountSnapshot(orderedEntries, warnings);
	}

	/// <summary>
	/// Builds one process-start-info payload for findmnt snapshot execution.
	/// </summary>
	/// <returns>Configured process start info.</returns>
	private static ProcessStartInfo BuildStartInfo()
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = FindmntCommand,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};

		for (int index = 0; index < _findmntArguments.Count; index++)
		{
			startInfo.ArgumentList.Add(_findmntArguments[index]);
		}

		return startInfo;
	}

	/// <summary>
	/// Attempts process startup and classifies startup failures.
	/// </summary>
	/// <param name="process">Process facade to start.</param>
	/// <param name="outcome">Classified startup outcome.</param>
	/// <param name="failureKind">Classified startup failure kind.</param>
	/// <returns><see langword="true"/> when process starts successfully; otherwise <see langword="false"/>.</returns>
	private static bool TryStartProcess(
		IProcessFacade process,
		out ExternalCommandOutcome outcome,
		out ExternalCommandFailureKind failureKind)
	{
		ArgumentNullException.ThrowIfNull(process);

		try
		{
			if (!process.Start())
			{
				outcome = ExternalCommandOutcome.StartFailed;
				failureKind = ExternalCommandFailureKind.StartFailure;
				return false;
			}

			outcome = ExternalCommandOutcome.Success;
			failureKind = ExternalCommandFailureKind.None;
			return true;
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			outcome = ExternalCommandOutcome.StartFailed;
			failureKind = ClassifyStartFailure(exception);
			return false;
		}
	}

	/// <summary>
	/// Supervises process completion with timeout polling.
	/// </summary>
	/// <param name="process">Process being supervised.</param>
	/// <param name="stopwatch">Execution stopwatch.</param>
	/// <param name="terminalOutcome">Terminal outcome classification.</param>
	/// <returns><see langword="true"/> when process exited successfully before timeout; otherwise <see langword="false"/>.</returns>
	private bool TryWaitForExit(
		IProcessFacade process,
		Stopwatch stopwatch,
		out ExternalCommandOutcome terminalOutcome)
	{
		while (true)
		{
			TimeSpan remaining = _timeout - stopwatch.Elapsed;
			int waitMilliseconds = CalculateWaitMilliseconds(_pollInterval, remaining);
			if (process.WaitForExit(waitMilliseconds))
			{
				terminalOutcome = ExternalCommandOutcome.Success;
				return true;
			}

			if (TryDetectExitNow(process))
			{
				terminalOutcome = ExternalCommandOutcome.Success;
				return true;
			}

			if (stopwatch.Elapsed >= _timeout)
			{
				if (TryDetectExitNow(process))
				{
					terminalOutcome = ExternalCommandOutcome.Success;
					return true;
				}

				terminalOutcome = ExternalCommandOutcome.TimedOut;
				return false;
			}
		}
	}

	/// <summary>
	/// Reads findmnt output line-by-line and parses entries while the process is running.
	/// </summary>
	/// <param name="reader">Output reader.</param>
	/// <param name="entries">Parsed entries destination.</param>
	/// <param name="warnings">Warning destination.</param>
	private static void CaptureAndParseOutputLines(
		TextReader reader,
		ICollection<MountSnapshotEntry> entries,
		ICollection<MountSnapshotWarning> warnings)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(warnings);
		int lineIndex = 0;
		try
		{
			while (true)
			{
				string? line = reader.ReadLine();
				if (line is null)
				{
					return;
				}

				lineIndex++;
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
					MountSnapshotWarningPolicy.Create(
						MountSnapshotWarningCodes.ParseFailure,
						$"Skipped malformed findmnt output line {lineIndex}: {warningMessage}."));
			}
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			warnings.Add(
				MountSnapshotWarningPolicy.Create(
					MountSnapshotWarningCodes.ParseFailure,
					$"Skipped findmnt output capture after read failure near line {lineIndex + 1}: {exception.GetType().Name}: {BuildTrimmedDiagnostic(exception.Message)}."));
		}
	}

	/// <summary>
	/// Captures one stream to the end as one string.
	/// </summary>
	/// <param name="reader">Reader to drain.</param>
	/// <returns>Captured stream text.</returns>
	private static string CaptureStreamToEnd(TextReader reader)
	{
		ArgumentNullException.ThrowIfNull(reader);

		try
		{
			return reader.ReadToEnd();
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			return $"stream capture failed: {exception.GetType().Name}: {exception.Message}";
		}
	}

	/// <summary>
	/// Waits for output capture task completion.
	/// </summary>
	/// <param name="task">Capture task.</param>
	/// <param name="maxWaitMilliseconds">Optional bounded wait in milliseconds.</param>
	private static void WaitForCaptureCompletion(Task task, int? maxWaitMilliseconds)
	{
		ArgumentNullException.ThrowIfNull(task);

		try
		{
			if (maxWaitMilliseconds.HasValue)
			{
				_ = task.Wait(maxWaitMilliseconds.Value);
				return;
			}
			task.Wait();
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
		}
	}

	/// <summary>
	/// Attempts to kill one process tree.
	/// </summary>
	/// <param name="process">Process to kill.</param>
	private static void TryKillProcess(IProcessFacade process)
	{
		ArgumentNullException.ThrowIfNull(process);
		try
		{
			process.Kill(entireProcessTree: true);
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
		}
	}

	/// <summary>
	/// Attempts one immediate exit probe without waiting.
	/// </summary>
	/// <param name="process">Process to probe.</param>
	/// <returns><see langword="true"/> when exited; otherwise <see langword="false"/>.</returns>
	private static bool TryDetectExitNow(IProcessFacade process)
	{
		return process.WaitForExit(0) || process.HasExited;
	}

	/// <summary>
	/// Calculates one bounded wait duration in milliseconds.
	/// </summary>
	/// <param name="pollInterval">Configured poll interval.</param>
	/// <param name="remaining">Remaining timeout window.</param>
	/// <returns>Positive wait duration in milliseconds.</returns>
	private static int CalculateWaitMilliseconds(TimeSpan pollInterval, TimeSpan remaining)
	{
		double waitMilliseconds = Math.Min(pollInterval.TotalMilliseconds, remaining.TotalMilliseconds);
		if (double.IsNaN(waitMilliseconds) || double.IsInfinity(waitMilliseconds) || waitMilliseconds <= 0)
		{
			return 1;
		}

		if (waitMilliseconds >= int.MaxValue)
		{
			return int.MaxValue;
		}

		return (int)Math.Ceiling(waitMilliseconds);
	}

	/// <summary>
	/// Calculates a bounded wait for timeout output-drain attempts.
	/// </summary>
	/// <param name="pollInterval">Configured process poll interval.</param>
	/// <returns>Drain wait duration in milliseconds.</returns>
	private static int CalculateCaptureWaitMilliseconds(TimeSpan pollInterval)
	{
		return Math.Max(
			MinCaptureWaitMilliseconds,
			CalculateWaitMilliseconds(pollInterval, TimeSpan.FromSeconds(1)));
	}

	/// <summary>
	/// Builds a warning payload for a failed <c>findmnt</c> command execution.
	/// </summary>
	/// <param name="outcome">Command outcome classification.</param>
	/// <param name="failureKind">Command failure kind classification.</param>
	/// <param name="exitCode">Optional process exit code.</param>
	/// <param name="standardError">Captured stderr diagnostics.</param>
	/// <returns>Warning instance describing command failure.</returns>
	private static MountSnapshotWarning BuildCommandFailureWarning(
		ExternalCommandOutcome outcome,
		ExternalCommandFailureKind failureKind,
		int? exitCode,
		string standardError)
	{
		string stderrDiagnostic = BuildTrimmedDiagnostic(standardError);
		string message =
			$"findmnt snapshot capture failed: outcome={outcome} failure_kind={failureKind} exit_code={exitCode?.ToString() ?? "<none>"} stderr={stderrDiagnostic}";
		return MountSnapshotWarningPolicy.Create(MountSnapshotWarningCodes.CommandFailure, message);
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
		if (singleLine.Length <= MaxWarningTextLength)
		{
			return singleLine;
		}

		return $"{singleLine[..MaxWarningTextLength]}...";
	}

	/// <summary>
	/// Classifies process startup failures.
	/// </summary>
	/// <param name="exception">Startup exception.</param>
	/// <returns>Failure kind classification.</returns>
	private static ExternalCommandFailureKind ClassifyStartFailure(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		if (exception is Win32Exception win32Exception && IsToolNotFoundException(win32Exception))
		{
			return ExternalCommandFailureKind.ToolNotFound;
		}
		return ExternalCommandFailureKind.StartFailure;
	}

	/// <summary>
	/// Determines whether one Win32 startup exception indicates command-not-found.
	/// </summary>
	/// <param name="exception">Win32 exception.</param>
	/// <returns><see langword="true"/> when command-not-found is indicated; otherwise <see langword="false"/>.</returns>
	private static bool IsToolNotFoundException(Win32Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return exception.NativeErrorCode == 2;
	}

	/// <summary>
	/// Determines whether an exception is fatal and must be rethrown.
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
