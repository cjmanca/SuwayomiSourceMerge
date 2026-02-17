namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Watching;

using System.ComponentModel;

using SuwayomiSourceMerge.Infrastructure.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

public sealed partial class PersistentInotifywaitEventReaderTests
{
	/// <summary>
	/// Verifies sessions are disposed and pruned when one previously watched root is removed.
	/// </summary>
	[Fact]
	public void Poll_Expected_ShouldDisposeStaleSession_WhenWatchRootIsRemoved()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootA = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources-a")).FullName;
		string sourcesRootB = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources-b")).FullName;

		FakeMonitorSession sessionA = new(sourcesRootA, recursive: false, isRunning: true);
		FakeMonitorSession sessionB = new(sourcesRootB, recursive: false, isRunning: true);
		sessionB.EnqueueWarning("stale-root-warning");
		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRootA, recursive: false, StartResult.FromStartedSession(sessionA));
		sessionFactory.EnqueueStartResult(sourcesRootB, recursive: false, StartResult.FromStartedSession(sessionB));

		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Progressive,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		_ = reader.Poll([sourcesRootA, sourcesRootB], TimeSpan.FromMilliseconds(1));
		InotifyPollResult secondPoll = reader.Poll([sourcesRootA], TimeSpan.FromMilliseconds(1));

		Assert.False(sessionB.IsRunning);
		Assert.Equal(1, sessionFactory.GetStartCallCount(sourcesRootB, recursive: false));
		Assert.DoesNotContain(secondPoll.Warnings, warning => warning.Contains("stale-root-warning", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies pending progressive deep roots are dropped when parent roots are no longer watched.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldDropPendingDeepRoots_WhenParentRootStopsBeingWatched()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string[] deepRoots =
		[
			Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk1")).FullName,
			Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk2")).FullName,
			Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk3")).FullName,
			Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk4")).FullName,
			Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk5")).FullName,
			Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk6")).FullName,
			Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk7")).FullName
		];

		FakeSessionFactory sessionFactory = new();
		FakeMonitorSession shallowSession = new(sourcesRoot, recursive: false, isRunning: true);
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: false, StartResult.FromStartedSession(shallowSession));
		for (int index = 0; index < deepRoots.Length; index++)
		{
			FakeMonitorSession deepSession = new(deepRoots[index], recursive: true, isRunning: true);
			sessionFactory.EnqueueStartResult(deepRoots[index], recursive: true, StartResult.FromStartedSession(deepSession));
		}

		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Progressive,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		_ = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));
		int deepStartsAfterFirstPoll = deepRoots.Sum(path => sessionFactory.GetStartCallCount(path, recursive: true));

		_ = reader.Poll([], TimeSpan.FromMilliseconds(1));
		int deepStartsAfterSecondPoll = deepRoots.Sum(path => sessionFactory.GetStartCallCount(path, recursive: true));

		Assert.Equal(6, deepStartsAfterFirstPoll);
		Assert.Equal(deepStartsAfterFirstPoll, deepStartsAfterSecondPoll);
		Assert.False(shallowSession.IsRunning);
	}

	/// <summary>
	/// Verifies stopped sessions under removed roots are not restarted.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldNotRestartStaleSession_WhenRootWasRemoved()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRootA = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources-a")).FullName;
		string sourcesRootB = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources-b")).FullName;

		FakeMonitorSession sessionA = new(sourcesRootA, recursive: false, isRunning: true);
		FakeMonitorSession sessionB = new(sourcesRootB, recursive: false, isRunning: true);
		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRootA, recursive: false, StartResult.FromStartedSession(sessionA));
		sessionFactory.EnqueueStartResult(sourcesRootB, recursive: false, StartResult.FromStartedSession(sessionB));
		sessionFactory.EnqueueStartResult(
			sourcesRootB,
			recursive: false,
			StartResult.FromStartedSession(new FakeMonitorSession(sourcesRootB, recursive: false, isRunning: true)));

		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Progressive,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow);

		_ = reader.Poll([sourcesRootA, sourcesRootB], TimeSpan.FromMilliseconds(1));
		sessionB.IsRunning = false;
		_ = reader.Poll([sourcesRootA], TimeSpan.FromMilliseconds(1));

		Assert.Equal(1, sessionFactory.GetStartCallCount(sourcesRootB, recursive: false));
		Assert.False(sessionB.IsRunning);
	}

	/// <summary>
	/// Verifies the poll loop bounds each sleep interval to the caller timeout remainder.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldBoundSleepToRemainingTimeout()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;

		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(
			sourcesRoot,
			recursive: false,
			StartResult.FromStartedSession(new FakeMonitorSession(sourcesRoot, recursive: false, isRunning: true)));

		List<TimeSpan> sleepDurations = [];
		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Progressive,
			sessionFactory.TryStart,
			static () => DateTimeOffset.UtcNow,
			duration => sleepDurations.Add(duration));

		_ = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(5));

		Assert.NotEmpty(sleepDurations);
		Assert.All(
			sleepDurations,
			static duration => Assert.InRange(duration, TimeSpan.Zero, TimeSpan.FromMilliseconds(5)));
	}

	/// <summary>
	/// Verifies missing-tool classification returns true for native error code 2.
	/// </summary>
	[Fact]
	public void IsToolNotFoundStartFailure_Expected_ShouldReturnTrue_WhenNativeErrorCodeIsTwo()
	{
		Win32Exception exception = new(2, "The system cannot find the file specified.");
		Assert.True(PersistentInotifywaitEventReader.IsToolNotFoundStartFailure(exception));
	}

	/// <summary>
	/// Verifies missing-tool classification also supports common message-only signatures.
	/// </summary>
	[Fact]
	public void IsToolNotFoundStartFailure_Edge_ShouldReturnTrue_WhenMessageMatchesMissingToolSignature()
	{
		Win32Exception exception = new(13, "No such file or directory");
		Assert.True(PersistentInotifywaitEventReader.IsToolNotFoundStartFailure(exception));
	}

	/// <summary>
	/// Verifies non-missing Win32 startup failures do not map to tool-not-found.
	/// </summary>
	[Fact]
	public void IsToolNotFoundStartFailure_Failure_ShouldReturnFalse_WhenFailureIsNotMissingTool()
	{
		Win32Exception exception = new(5, "Access is denied.");
		Assert.False(PersistentInotifywaitEventReader.IsToolNotFoundStartFailure(exception));
	}
}
