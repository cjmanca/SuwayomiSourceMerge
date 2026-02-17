using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;

namespace SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Executes external commands with deterministic timeout, cancellation, and bounded output capture.
/// </summary>
internal sealed class ExternalCommandExecutor : IExternalCommandExecutor
{
	/// <summary>
	/// Character-buffer size used by output capture readers.
	/// </summary>
	private const int CaptureBufferSize = 2048;

	/// <summary>
	/// Minimum best-effort wait duration used when attempting to drain output after timeout/cancellation.
	/// </summary>
	private const int MinCaptureWaitMilliseconds = 50;

	/// <summary>
	/// Factory used to create process facade instances for each execution attempt.
	/// </summary>
	private readonly Func<IProcessFacade> _processFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExternalCommandExecutor"/> class.
	/// </summary>
	public ExternalCommandExecutor()
		: this(static () => new SystemProcessFacade())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExternalCommandExecutor"/> class
	/// with an explicit process-facade factory for deterministic testing.
	/// </summary>
	/// <param name="processFactory">Factory that creates one process facade per execution.</param>
	internal ExternalCommandExecutor(Func<IProcessFacade> processFactory)
	{
		_processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
	}

	/// <inheritdoc />
	public ExternalCommandResult Execute(ExternalCommandRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request);
		ValidateRequest(request);

		ProcessStartInfo startInfo = BuildStartInfo(request);
		Stopwatch stopwatch = Stopwatch.StartNew();

		using IProcessFacade process = _processFactory();
		process.ConfigureStartInfo(startInfo);

		if (!TryStartProcess(process, stopwatch, out ExternalCommandResult? startFailureResult))
		{
			return startFailureResult!;
		}

		BoundedOutputBuffer standardOutputBuffer = new(request.MaxOutputCharacters);
		BoundedOutputBuffer standardErrorBuffer = new(request.MaxOutputCharacters);
		Task standardOutputTask = CaptureReaderAsync(process.StandardOutputReader, standardOutputBuffer);
		Task standardErrorTask = CaptureReaderAsync(process.StandardErrorReader, standardErrorBuffer);

		bool exited = TryWaitForExit(process, request, cancellationToken, stopwatch, out ExternalCommandOutcome terminalOutcome);
		if (!exited)
		{
			TryKillProcess(process);
		}

		if (exited)
		{
			WaitForCaptureCompletion(standardOutputTask, maxWaitMilliseconds: null);
			WaitForCaptureCompletion(standardErrorTask, maxWaitMilliseconds: null);
		}
		else
		{
			int captureWaitMilliseconds = CalculateCaptureWaitMilliseconds(request.PollInterval);
			WaitForCaptureCompletion(standardOutputTask, captureWaitMilliseconds);
			WaitForCaptureCompletion(standardErrorTask, captureWaitMilliseconds);
		}

		(string standardOutput, bool isStandardOutputTruncated) = standardOutputBuffer.GetSnapshot();
		(string standardError, bool isStandardErrorTruncated) = standardErrorBuffer.GetSnapshot();

		if (!exited)
		{
			return new ExternalCommandResult(
				terminalOutcome,
				ExternalCommandFailureKind.None,
				null,
				standardOutput,
				standardError,
				isStandardOutputTruncated,
				isStandardErrorTruncated,
				stopwatch.Elapsed);
		}

		int exitCode = process.ExitCode;
		ExternalCommandOutcome outcome = exitCode == 0
			? ExternalCommandOutcome.Success
			: ExternalCommandOutcome.NonZeroExit;

		return new ExternalCommandResult(
			outcome,
			ExternalCommandFailureKind.None,
			exitCode,
			standardOutput,
			standardError,
			isStandardOutputTruncated,
			isStandardErrorTruncated,
			stopwatch.Elapsed);
	}

	/// <summary>
	/// Validates command request fields before process startup.
	/// </summary>
	/// <param name="request">Request to validate.</param>
	private static void ValidateRequest(ExternalCommandRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.FileName))
		{
			throw new ArgumentException(
				"File name must not be null, empty, or whitespace.",
				nameof(ExternalCommandRequest.FileName));
		}

		if (request.Arguments is null)
		{
			throw new ArgumentNullException(nameof(ExternalCommandRequest.Arguments));
		}

		if (request.Timeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(ExternalCommandRequest.Timeout),
				request.Timeout,
				"Timeout must be greater than zero.");
		}

		if (request.PollInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(ExternalCommandRequest.PollInterval),
				request.PollInterval,
				"Poll interval must be greater than zero.");
		}

		if (request.MaxOutputCharacters <= 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(ExternalCommandRequest.MaxOutputCharacters),
				request.MaxOutputCharacters,
				"Max output characters must be greater than zero.");
		}

		for (int index = 0; index < request.Arguments.Count; index++)
		{
			if (request.Arguments[index] is null)
			{
				throw new ArgumentException(
					$"Command arguments must not contain null values. Null argument at index {index}.",
					nameof(ExternalCommandRequest.Arguments));
			}
		}
	}

	/// <summary>
	/// Builds process startup settings from a validated command request.
	/// </summary>
	/// <param name="request">Validated command request.</param>
	/// <returns>Configured <see cref="ProcessStartInfo"/> instance.</returns>
	private static ProcessStartInfo BuildStartInfo(ExternalCommandRequest request)
	{
		ProcessStartInfo startInfo = new()
		{
			FileName = request.FileName,
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};

		foreach (string argument in request.Arguments)
		{
			startInfo.ArgumentList.Add(argument);
		}

		return startInfo;
	}

	/// <summary>
	/// Attempts to start the configured process and converts startup failures into typed results.
	/// </summary>
	/// <param name="process">Process facade to start.</param>
	/// <param name="stopwatch">Stopwatch measuring elapsed execution time.</param>
	/// <param name="result">When startup fails, receives the typed failure result.</param>
	/// <returns><see langword="true"/> when startup succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TryStartProcess(
		IProcessFacade process,
		Stopwatch stopwatch,
		out ExternalCommandResult? result)
	{
		try
		{
			if (!process.Start())
			{
				result = new ExternalCommandResult(
					ExternalCommandOutcome.StartFailed,
					ExternalCommandFailureKind.StartFailure,
					null,
					string.Empty,
					string.Empty,
					false,
					false,
					stopwatch.Elapsed);
				return false;
			}

			result = null;
			return true;
		}
		catch (Exception exception)
		{
			result = new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ClassifyStartFailure(exception),
				null,
				string.Empty,
				string.Empty,
				false,
				false,
				stopwatch.Elapsed);
			return false;
		}
	}

	/// <summary>
	/// Supervises process completion with polling until exit, timeout, or cancellation occurs.
	/// </summary>
	/// <param name="process">Process facade being supervised.</param>
	/// <param name="request">Request containing timeout and polling settings.</param>
	/// <param name="cancellationToken">Cancellation token for early termination.</param>
	/// <param name="stopwatch">Stopwatch tracking elapsed runtime.</param>
	/// <param name="terminalOutcome">Returns the terminal outcome when supervision ends.</param>
	/// <returns><see langword="true"/> when process exited; otherwise <see langword="false"/>.</returns>
	private static bool TryWaitForExit(
		IProcessFacade process,
		ExternalCommandRequest request,
		CancellationToken cancellationToken,
		Stopwatch stopwatch,
		out ExternalCommandOutcome terminalOutcome)
	{
		while (true)
		{
			TimeSpan remaining = request.Timeout - stopwatch.Elapsed;
			int waitMilliseconds = CalculateWaitMilliseconds(request.PollInterval, remaining);
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

			if (cancellationToken.IsCancellationRequested)
			{
				if (TryDetectExitNow(process))
				{
					terminalOutcome = ExternalCommandOutcome.Success;
					return true;
				}

				terminalOutcome = ExternalCommandOutcome.Cancelled;
				return false;
			}

			if (stopwatch.Elapsed >= request.Timeout)
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
	/// Performs an immediate best-effort exit probe without additional waiting.
	/// </summary>
	/// <param name="process">Process facade to probe.</param>
	/// <returns><see langword="true"/> when the process is observed exited; otherwise <see langword="false"/>.</returns>
	private static bool TryDetectExitNow(IProcessFacade process)
	{
		return process.WaitForExit(0) || process.HasExited;
	}

	/// <summary>
	/// Calculates a bounded wait duration in milliseconds for one supervision poll iteration.
	/// </summary>
	/// <param name="pollInterval">Configured poll interval.</param>
	/// <param name="remaining">Remaining timeout window.</param>
	/// <returns>Positive millisecond value suitable for <see cref="IProcessFacade.WaitForExit(int)"/>.</returns>
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
	/// Calculates a bounded wait duration for timeout/cancellation output-drain attempts.
	/// </summary>
	/// <param name="pollInterval">Configured process poll interval.</param>
	/// <returns>Bounded output-drain wait duration in milliseconds.</returns>
	private static int CalculateCaptureWaitMilliseconds(TimeSpan pollInterval)
	{
		return Math.Max(
			MinCaptureWaitMilliseconds,
			CalculateWaitMilliseconds(pollInterval, TimeSpan.FromSeconds(1)));
	}

	/// <summary>
	/// Captures output from a reader into a bounded buffer until the stream ends.
	/// </summary>
	/// <param name="reader">Reader supplying stream text.</param>
	/// <param name="buffer">Bounded destination buffer.</param>
	/// <returns>Capture task that completes when the stream is drained.</returns>
	private static async Task CaptureReaderAsync(TextReader reader, BoundedOutputBuffer buffer)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentNullException.ThrowIfNull(buffer);

		char[] chunk = ArrayPool<char>.Shared.Rent(CaptureBufferSize);

		try
		{
			while (true)
			{
				int read = await reader.ReadAsync(chunk.AsMemory(0, CaptureBufferSize)).ConfigureAwait(false);
				if (read <= 0)
				{
					return;
				}

				buffer.Append(chunk, read);
			}
		}
		catch (ObjectDisposedException)
		{
			// Best-effort capture should not fail command execution.
		}
		catch (InvalidOperationException)
		{
			// Best-effort capture should not fail command execution.
		}
		catch (IOException)
		{
			// Best-effort capture should not fail command execution.
		}
		finally
		{
			ArrayPool<char>.Shared.Return(chunk);
		}
	}

	/// <summary>
	/// Waits for capture completion either indefinitely or for a bounded duration.
	/// </summary>
	/// <param name="captureTask">Capture task to wait on.</param>
	/// <param name="maxWaitMilliseconds">
	/// Optional bounded wait in milliseconds. When <see langword="null"/>, waits until completion.
	/// </param>
	private static void WaitForCaptureCompletion(Task captureTask, int? maxWaitMilliseconds)
	{
		ArgumentNullException.ThrowIfNull(captureTask);

		try
		{
			if (maxWaitMilliseconds.HasValue)
			{
				captureTask.Wait(maxWaitMilliseconds.Value);
			}
			else
			{
				captureTask.GetAwaiter().GetResult();
			}
		}
		catch (ObjectDisposedException)
		{
			// Best-effort capture should not fail command execution.
		}
		catch (InvalidOperationException)
		{
			// Best-effort capture should not fail command execution.
		}
		catch (IOException)
		{
			// Best-effort capture should not fail command execution.
		}
		catch (AggregateException exception)
		{
			AggregateException flattenedException = exception.Flatten();
			if (ContainsFatalException(flattenedException.InnerExceptions))
			{
				throw;
			}

			TraceUnexpectedCaptureException(flattenedException);
		}
		catch (Exception exception)
		{
			if (IsFatalException(exception))
			{
				throw;
			}

			TraceUnexpectedCaptureException(exception);
		}
	}

	/// <summary>
	/// Determines whether a collection of exceptions contains at least one fatal exception.
	/// </summary>
	/// <param name="exceptions">Exceptions to inspect.</param>
	/// <returns><see langword="true"/> when any exception is fatal; otherwise <see langword="false"/>.</returns>
	private static bool ContainsFatalException(IReadOnlyCollection<Exception> exceptions)
	{
		foreach (Exception exception in exceptions)
		{
			if (IsFatalException(exception))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Determines whether an exception should be treated as fatal.
	/// </summary>
	/// <param name="exception">Exception to inspect.</param>
	/// <returns><see langword="true"/> when exception is fatal; otherwise <see langword="false"/>.</returns>
	private static bool IsFatalException(Exception exception)
	{
		return exception is OutOfMemoryException
			|| exception is StackOverflowException
			|| exception is AccessViolationException;
	}

	/// <summary>
	/// Emits a trace warning for unexpected non-fatal output-capture completion failures.
	/// </summary>
	/// <param name="exception">Unexpected exception to trace.</param>
	private static void TraceUnexpectedCaptureException(Exception exception)
	{
		Trace.TraceWarning(
			"External command capture completion ignored unexpected non-fatal exception: {0}",
			exception);
	}

	/// <summary>
	/// Attempts to terminate the running process and its child processes.
	/// </summary>
	/// <param name="process">Process facade to terminate.</param>
	private static void TryKillProcess(IProcessFacade process)
	{
		try
		{
			if (!process.HasExited)
			{
				process.Kill(entireProcessTree: true);
			}
		}
		catch
		{
			// Best-effort termination should not fail command execution.
		}
	}

	/// <summary>
	/// Classifies process startup failures into deterministic failure kinds.
	/// </summary>
	/// <param name="exception">Startup exception to classify.</param>
	/// <returns>Classified failure kind.</returns>
	private static ExternalCommandFailureKind ClassifyStartFailure(Exception exception)
	{
		if (exception is Win32Exception win32Exception && IsToolNotFoundException(win32Exception))
		{
			return ExternalCommandFailureKind.ToolNotFound;
		}

		return ExternalCommandFailureKind.StartFailure;
	}

	/// <summary>
	/// Determines whether a startup exception indicates a missing executable.
	/// </summary>
	/// <param name="exception">Startup exception.</param>
	/// <returns><see langword="true"/> when exception indicates missing tool; otherwise <see langword="false"/>.</returns>
	private static bool IsToolNotFoundException(Win32Exception exception)
	{
		if (exception.NativeErrorCode == 2)
		{
			return true;
		}

		return exception.Message.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
			|| exception.Message.Contains("cannot find the file", StringComparison.OrdinalIgnoreCase)
			|| exception.Message.Contains("cannot find the path", StringComparison.OrdinalIgnoreCase);
	}
}
