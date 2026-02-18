namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Watching;

using SuwayomiSourceMerge.Infrastructure.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Bounded poll-buffer behavior coverage for <see cref="PersistentInotifywaitEventReader"/>.
/// </summary>
public sealed partial class PersistentInotifywaitEventReaderTests
{
	private const string OverflowWarningToken = "Persistent inotify poll buffers overflowed.";

	/// <summary>
	/// Verifies poll results preserve warnings and events when both stay under configured caps.
	/// </summary>
	[Fact]
	public void Poll_Expected_ShouldReturnAllWarningsAndEvents_WhenUnderBufferCaps()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeMonitorSession session = new(sourcesRoot, recursive: true, isRunning: true);
		session.EnqueueWarning("warning-1");
		session.EnqueueWarning("warning-2");
		session.EnqueueEvent(new InotifyEventRecord(Path.Combine(sourcesRoot, "SourceA"), InotifyEventMask.Create | InotifyEventMask.IsDirectory, "CREATE,ISDIR"));
		session.EnqueueEvent(new InotifyEventRecord(Path.Combine(sourcesRoot, "SourceB"), InotifyEventMask.CloseWrite, "CLOSE_WRITE"));
		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: true, StartResult.FromStartedSession(session));

		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Full,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Equal(2, result.Events.Count);
		Assert.Equal(2, result.Warnings.Count);
		Assert.DoesNotContain(result.Warnings, warning => warning.Contains(OverflowWarningToken, StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies warning overflow drops oldest warning entries and emits one overflow summary warning.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldDropOldestWarningsAndEmitOverflowSummary_WhenWarningBufferOverflows()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeMonitorSession session = new(sourcesRoot, recursive: true, isRunning: true);
		for (int index = 0; index < 1300; index++)
		{
			session.EnqueueWarning($"warning-{index:D4}");
		}

		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: true, StartResult.FromStartedSession(session));
		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Full,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));

		Assert.Equal(1024, result.Warnings.Count);
		Assert.Contains(result.Warnings, warning => warning.Contains(OverflowWarningToken, StringComparison.Ordinal));
		Assert.DoesNotContain(result.Warnings, static warning => warning == "warning-0000");
		Assert.Contains(result.Warnings, static warning => warning == "warning-1299");
	}

	/// <summary>
	/// Verifies concurrent event and warning overflow remains bounded and preserves newest items.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldKeepBoundedNewestItems_WhenWarningsAndEventsOverflowTogether()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeMonitorSession session = new(sourcesRoot, recursive: true, isRunning: true);
		for (int index = 0; index < 2000; index++)
		{
			session.EnqueueWarning($"warning-{index:D4}");
		}

		for (int index = 0; index < 5000; index++)
		{
			session.EnqueueEvent(new InotifyEventRecord(
				Path.Combine(sourcesRoot, $"title-{index:D4}"),
				InotifyEventMask.Create | InotifyEventMask.IsDirectory,
				"CREATE,ISDIR"));
		}

		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: true, StartResult.FromStartedSession(session));
		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Full,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Equal(4096, result.Events.Count);
		Assert.Equal(1024, result.Warnings.Count);
		Assert.Contains(result.Warnings, warning => warning.Contains(OverflowWarningToken, StringComparison.Ordinal));
		Assert.DoesNotContain(result.Events, static item => item.Path.EndsWith("title-0000", StringComparison.Ordinal));
		Assert.Contains(result.Events, static item => item.Path.EndsWith("title-4999", StringComparison.Ordinal));
		Assert.DoesNotContain(result.Warnings, static warning => warning == "warning-0000");
		Assert.Contains(result.Warnings, static warning => warning == "warning-1999");
	}

	/// <summary>
	/// Verifies overflow summary warning is retained when warning buffer is already full and event overflow occurs.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldRetainOverflowSummaryAndOverflowOnlyWarningCount_WhenWarningBufferIsFullAndEventsOverflow()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		FakeMonitorSession session = new(sourcesRoot, recursive: true, isRunning: true);
		for (int index = 0; index < 1024; index++)
		{
			session.EnqueueWarning($"warning-{index:D4}");
		}

		for (int index = 0; index < 5000; index++)
		{
			session.EnqueueEvent(new InotifyEventRecord(
				Path.Combine(sourcesRoot, $"title-{index:D4}"),
				InotifyEventMask.Create | InotifyEventMask.IsDirectory,
				"CREATE,ISDIR"));
		}

		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: true, StartResult.FromStartedSession(session));
		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Full,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		InotifyPollResult result = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Equal(1024, result.Warnings.Count);
		Assert.Contains(result.Warnings, warning => warning.Contains("dropped_events='", StringComparison.Ordinal));
		Assert.Contains(result.Warnings, warning => warning.Contains("dropped_warnings='0'", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies warning-overflow summaries are emitted when invalid root normalization overflows and poll exits early.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldEmitOverflowSummary_WhenInvalidRootNormalizationOverflowsAndNoRootsNormalize()
	{
		List<string> invalidRoots = [];
		for (int index = 0; index < 1300; index++)
		{
			invalidRoots.Add($"invalid\0root-{index:D4}");
		}

		FakeSessionFactory sessionFactory = new();
		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Full,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		InotifyPollResult result = reader.Poll(invalidRoots, TimeSpan.FromMilliseconds(1));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Empty(result.Events);
		Assert.Equal(1024, result.Warnings.Count);
		Assert.Contains(result.Warnings, warning => warning.Contains(OverflowWarningToken, StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies warning-overflow summaries are emitted when many missing roots overflow warnings before no-existing-roots early return.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldEmitOverflowSummary_WhenMissingRootWarningsOverflowAndNoRootsExist()
	{
		using TemporaryDirectory temporaryDirectory = new();
		List<string> missingRoots = [];
		for (int index = 0; index < 1300; index++)
		{
			missingRoots.Add(Path.Combine(temporaryDirectory.Path, "missing", $"root-{index:D4}"));
		}

		FakeSessionFactory sessionFactory = new();
		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Full,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		InotifyPollResult result = reader.Poll(missingRoots, TimeSpan.FromMilliseconds(1));

		Assert.Equal(InotifyPollOutcome.Success, result.Outcome);
		Assert.Empty(result.Events);
		Assert.Equal(1024, result.Warnings.Count);
		Assert.Contains(result.Warnings, warning => warning.Contains(OverflowWarningToken, StringComparison.Ordinal));
	}
}
