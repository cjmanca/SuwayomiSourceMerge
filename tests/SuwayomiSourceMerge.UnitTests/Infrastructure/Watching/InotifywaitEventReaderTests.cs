namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Watching;

using SuwayomiSourceMerge.Infrastructure.Processes;
using SuwayomiSourceMerge.Infrastructure.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="InotifywaitEventReader"/>.
/// </summary>
public sealed class InotifywaitEventReaderTests
{
	/// <summary>
	/// Verifies stdout lines are parsed into typed inotify event records.
	/// </summary>
	[Fact]
	public void Poll_Expected_ShouldParseEvents_WhenCommandSucceeds()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string overrideRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				"/ssm/sources/a/b/c|CREATE,ISDIR\n/ssm/override/t/details.json|CLOSE_WRITE",
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.FromMilliseconds(10)));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([sourcesRoot, overrideRoot], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Equal(2, result.Events.Count);
		Assert.True(result.Events[0].IsDirectory);
		Assert.True((result.Events[0].EventMask & InotifyEventMask.Create) != 0);
		Assert.True((result.Events[1].EventMask & InotifyEventMask.CloseWrite) != 0);
		Assert.Empty(result.Warnings);
		Assert.NotNull(executor.LastRequest);
		Assert.Equal("inotifywait", executor.LastRequest!.FileName);
	}

	/// <summary>
	/// Verifies inotify timeout exit code returns a non-fatal timed-out result.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldReturnTimedOut_WhenInotifyTimeoutOccurs()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				2,
				string.Empty,
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.FromSeconds(5)));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.TimedOut, result.Outcome);
		Assert.Empty(result.Events);
		Assert.Empty(result.Warnings);
	}

	/// <summary>
	/// Verifies executor timeout budget includes setup/completion headroom above requested inotify timeout.
	/// </summary>
	[Theory]
	[InlineData(5, 300, 305)]
	[InlineData(20, 15, 35)]
	public void Poll_Edge_ShouldApplyConfiguredExecutorTimeoutBuffer(
		int pollSeconds,
		int requestTimeoutBufferSeconds,
		int expectedExecutorTimeoutSeconds)
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				2,
				string.Empty,
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.FromSeconds(pollSeconds)));
		InotifywaitEventReader reader = new(executor, requestTimeoutBufferSeconds);

		_ = reader.Poll([sourcesRoot], TimeSpan.FromSeconds(pollSeconds));

		Assert.NotNull(executor.LastRequest);
		Assert.Equal(TimeSpan.FromSeconds(expectedExecutorTimeoutSeconds), executor.LastRequest!.Timeout);
	}

	/// <summary>
	/// Verifies default constructor timeout buffer is applied to executor timeout.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldApplyDefaultExecutorTimeoutBuffer_WhenNotConfigured()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.NonZeroExit,
				ExternalCommandFailureKind.None,
				2,
				string.Empty,
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.FromSeconds(5)));
		InotifywaitEventReader reader = new(executor);

		_ = reader.Poll([sourcesRoot], TimeSpan.FromSeconds(5));

		Assert.NotNull(executor.LastRequest);
		Assert.Equal(TimeSpan.FromSeconds(305), executor.LastRequest!.Timeout);
	}

	/// <summary>
	/// Verifies invalid constructor timeout-buffer values are rejected.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenRequestTimeoutBufferSecondsIsInvalid()
	{
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.Zero));

		Assert.Throws<ArgumentOutOfRangeException>(() => new InotifywaitEventReader(executor, 0));
	}

	/// <summary>
	/// Verifies executor timeout without stderr is treated as non-fatal timed-out poll.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldReturnTimedOut_WhenExecutorTimeoutOccursWithoutStandardError()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.TimedOut,
				ExternalCommandFailureKind.None,
				null,
				string.Empty,
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.FromSeconds(7)));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.TimedOut, result.Outcome);
		Assert.Empty(result.Events);
		Assert.Empty(result.Warnings);
	}

	/// <summary>
	/// Verifies missing inotifywait tool maps to tool-not-found poll outcome.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldReturnToolNotFound_WhenToolIsMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.StartFailed,
				ExternalCommandFailureKind.ToolNotFound,
				null,
				string.Empty,
				"not found",
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.Zero));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.ToolNotFound, result.Outcome);
		Assert.Empty(result.Events);
		Assert.NotEmpty(result.Warnings);
	}

	/// <summary>
	/// Verifies executor timeout with stderr is treated as command failure with warnings.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldReturnCommandFailed_WhenExecutorTimeoutIncludesStandardError()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.TimedOut,
				ExternalCommandFailureKind.None,
				null,
				string.Empty,
				"timed out while initializing watches",
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.FromSeconds(7)));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.CommandFailed, result.Outcome);
		Assert.Empty(result.Events);
		Assert.Equal(2, result.Warnings.Count);
		Assert.Contains(result.Warnings, warning => warning.Contains("inotifywait poll failed", StringComparison.Ordinal));
		Assert.Contains(result.Warnings, warning => warning.Contains("timed out while initializing watches", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies malformed lines are ignored with warnings instead of throwing.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldIgnoreMalformedOutputLines_WithWarnings()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				"bad-line\n/ssm/sources/a/b/c|CREATE,ISDIR\nalso-bad",
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.Zero));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Single(result.Events);
		Assert.Equal(2, result.Warnings.Count);
	}

	/// <summary>
	/// Verifies missing roots are skipped while existing roots are still polled.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldSkipMissingRootsWithWarnings_WhenMixedRootsProvided()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string existingRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string missingRoot = Path.Combine(temporaryDirectory.Path, "missing");
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				"/ssm/sources/a/b/c|CREATE,ISDIR",
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.Zero));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([existingRoot, missingRoot], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Single(result.Events);
		Assert.Contains(result.Warnings, warning => warning.Contains("Skipping missing watch root", StringComparison.Ordinal));
		Assert.NotNull(executor.LastRequest);
		Assert.Contains(existingRoot, executor.LastRequest!.Arguments);
		Assert.DoesNotContain(missingRoot, executor.LastRequest.Arguments);
	}

	/// <summary>
	/// Verifies all-missing roots return success with warnings and skip command execution.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldReturnSuccessWithoutCommandExecution_WhenAllRootsMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string missingRootA = Path.Combine(temporaryDirectory.Path, "missing-a");
		string missingRootB = Path.Combine(temporaryDirectory.Path, "missing-b");
		FakeExternalCommandExecutor executor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				string.Empty,
				string.Empty,
				isStandardOutputTruncated: false,
				isStandardErrorTruncated: false,
				TimeSpan.Zero));
		InotifywaitEventReader reader = new(executor);

		InotifyPollResult result = reader.Poll([missingRootA, missingRootB], TimeSpan.FromSeconds(5));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Empty(result.Events);
		Assert.Contains(result.Warnings, warning => warning.Contains("No existing watch roots", StringComparison.Ordinal));
		Assert.Equal(0, executor.ExecuteCallCount);
		Assert.Null(executor.LastRequest);
	}

	/// <summary>
	/// Verifies invalid arguments are rejected.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldThrow_WhenTimeoutInvalid()
	{
		InotifywaitEventReader reader = new(
			new FakeExternalCommandExecutor(
				new ExternalCommandResult(
					ExternalCommandOutcome.Success,
					ExternalCommandFailureKind.None,
					0,
					string.Empty,
					string.Empty,
					isStandardOutputTruncated: false,
					isStandardErrorTruncated: false,
					TimeSpan.Zero)));

		Assert.Throws<ArgumentOutOfRangeException>(() => reader.Poll(["/ssm/sources"], TimeSpan.Zero));
	}

	/// <summary>
	/// Minimal command executor fake returning one configured result.
	/// </summary>
	private sealed class FakeExternalCommandExecutor : IExternalCommandExecutor
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FakeExternalCommandExecutor"/> class.
		/// </summary>
		/// <param name="result">Result returned by <see cref="Execute"/>.</param>
		public FakeExternalCommandExecutor(ExternalCommandResult result)
		{
			Result = result ?? throw new ArgumentNullException(nameof(result));
		}

		/// <summary>
		/// Gets returned command result.
		/// </summary>
		public ExternalCommandResult Result
		{
			get;
		}

		/// <summary>
		/// Gets last request passed to <see cref="Execute"/>.
		/// </summary>
		public ExternalCommandRequest? LastRequest
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets number of execute calls.
		/// </summary>
		public int ExecuteCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public ExternalCommandResult Execute(ExternalCommandRequest request, CancellationToken cancellationToken = default)
		{
			ExecuteCallCount++;
			LastRequest = request;
			return Result;
		}
	}
}
