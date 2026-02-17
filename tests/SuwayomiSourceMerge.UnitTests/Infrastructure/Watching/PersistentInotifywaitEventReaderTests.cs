namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Watching;

using SuwayomiSourceMerge.Infrastructure.Watching;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="PersistentInotifywaitEventReader"/>.
/// </summary>
public sealed partial class PersistentInotifywaitEventReaderTests
{
	/// <summary>
	/// Verifies failed progressive deep-session starts are requeued and retried on later polls.
	/// </summary>
	[Fact]
	public void Poll_Expected_ShouldRetryDeepSessionStart_WhenInitialStartFails()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string deepRoot = Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk1")).FullName;

		FakeMonitorSession shallowSession = new(sourcesRoot, recursive: false, isRunning: true);
		FakeMonitorSession deepSession = new(deepRoot, recursive: true, isRunning: true);
		deepSession.EnqueueEvent(new InotifyEventRecord(Path.Combine(deepRoot, "SourceA"), InotifyEventMask.Create | InotifyEventMask.IsDirectory, "CREATE,ISDIR"));
		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: false, StartResult.FromStartedSession(shallowSession));
		sessionFactory.EnqueueStartResult(deepRoot, recursive: true, StartResult.FailedToolNotFound("missing inotifywait"));
		sessionFactory.EnqueueStartResult(deepRoot, recursive: true, StartResult.FromStartedSession(deepSession));
		QueueClock clock = new(
		[
			DateTimeOffset.Parse("2026-02-17T00:00:00Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:00Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:01Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:07Z")
		]);

		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Progressive,
			sessionFactory.TryStart,
			clock.GetNowUtc);

		_ = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));
		InotifyPollResult secondPoll = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(20));

		Assert.Equal(2, sessionFactory.GetStartCallCount(deepRoot, recursive: true));
		Assert.Contains(secondPoll.Events, record => record.Path.Contains("SourceA", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies progressive deep-session retry gates are re-evaluated using post-wait timestamps.
	/// </summary>
	[Fact]
	public void Poll_Edge_ShouldUsePostWaitTimestamp_WhenDeepSessionRetryGateExpiresMidPoll()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string deepRoot = Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk1")).FullName;

		FakeMonitorSession shallowSession = new(sourcesRoot, recursive: false, isRunning: true);
		FakeMonitorSession deepStoppedSession = new(deepRoot, recursive: true, isRunning: false);
		FakeMonitorSession deepRestartedSession = new(deepRoot, recursive: true, isRunning: true);
		deepRestartedSession.EnqueueEvent(new InotifyEventRecord(Path.Combine(deepRoot, "SourceB"), InotifyEventMask.Create | InotifyEventMask.IsDirectory, "CREATE,ISDIR"));

		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: false, StartResult.FromStartedSession(shallowSession));
		sessionFactory.EnqueueStartResult(deepRoot, recursive: true, StartResult.FromStartedSession(deepStoppedSession));
		sessionFactory.EnqueueStartResult(deepRoot, recursive: true, StartResult.FromStartedSession(deepRestartedSession));

		QueueClock clock = new(
		[
			DateTimeOffset.Parse("2026-02-17T00:00:00Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:00Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:01Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:07Z")
		]);

		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Progressive,
			sessionFactory.TryStart,
			clock.GetNowUtc);

		_ = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));
		InotifyPollResult secondPoll = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(20));

		Assert.Equal(2, sessionFactory.GetStartCallCount(deepRoot, recursive: true));
		Assert.Contains(secondPoll.Events, record => record.Path.Contains("SourceB", StringComparison.Ordinal));
	}

	/// <summary>
	/// Verifies deep sessions that stop between polls are requeued and restarted.
	/// </summary>
	[Fact]
	public void Poll_Failure_ShouldRestartDeepSession_WhenExistingSessionStopsBetweenPolls()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcesRoot = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources")).FullName;
		string deepRoot = Directory.CreateDirectory(Path.Combine(sourcesRoot, "disk1")).FullName;

		FakeMonitorSession shallowSession = new(sourcesRoot, recursive: false, isRunning: true);
		FakeMonitorSession deepSession = new(deepRoot, recursive: true, isRunning: true);
		deepSession.EnqueueEvent(new InotifyEventRecord(Path.Combine(deepRoot, "SourceC"), InotifyEventMask.Create | InotifyEventMask.IsDirectory, "CREATE,ISDIR"));
		FakeMonitorSession restartedDeepSession = new(deepRoot, recursive: true, isRunning: true);
		restartedDeepSession.EnqueueEvent(new InotifyEventRecord(Path.Combine(deepRoot, "SourceD"), InotifyEventMask.Create | InotifyEventMask.IsDirectory, "CREATE,ISDIR"));

		FakeSessionFactory sessionFactory = new();
		sessionFactory.EnqueueStartResult(sourcesRoot, recursive: false, StartResult.FromStartedSession(shallowSession));
		sessionFactory.EnqueueStartResult(deepRoot, recursive: true, StartResult.FromStartedSession(deepSession));
		sessionFactory.EnqueueStartResult(deepRoot, recursive: true, StartResult.FromStartedSession(restartedDeepSession));

		QueueClock clock = new(
		[
			DateTimeOffset.Parse("2026-02-17T00:00:00Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:00Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:01Z"),
			DateTimeOffset.Parse("2026-02-17T00:00:07Z")
		]);

		using PersistentInotifywaitEventReader reader = new(
			InotifyWatchStartupMode.Progressive,
			sessionFactory.TryStart,
			clock.GetNowUtc);

		_ = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(1));
		deepSession.IsRunning = false;
		InotifyPollResult secondPoll = reader.Poll([sourcesRoot], TimeSpan.FromMilliseconds(20));

		Assert.Equal(2, sessionFactory.GetStartCallCount(deepRoot, recursive: true));
		Assert.Contains(secondPoll.Events, record => record.Path.Contains("SourceD", StringComparison.Ordinal));
	}

	/// <summary>
	/// Minimal fake monitor session.
	/// </summary>
	private sealed class FakeMonitorSession : IPersistentInotifyMonitorSession
	{
		private readonly Queue<InotifyEventRecord> _events = new();
		private readonly Queue<string> _warnings = new();

		public FakeMonitorSession(string watchPath, bool recursive, bool isRunning)
		{
			WatchPath = watchPath;
			IsRecursive = recursive;
			IsRunning = isRunning;
		}

		public string WatchPath
		{
			get;
		}

		public bool IsRecursive
		{
			get;
		}

		public bool IsRunning
		{
			get;
			set;
		}

		public void EnqueueEvent(InotifyEventRecord record)
		{
			_events.Enqueue(record);
		}

		public void EnqueueWarning(string warning)
		{
			ArgumentNullException.ThrowIfNull(warning);
			_warnings.Enqueue(warning);
		}

		public void Dispose()
		{
			IsRunning = false;
		}

		public bool TryDequeueEvent(out InotifyEventRecord record)
		{
			if (_events.Count == 0)
			{
				record = null!;
				return false;
			}

			record = _events.Dequeue();
			return true;
		}

		public bool TryDequeueWarning(out string warning)
		{
			if (_warnings.Count == 0)
			{
				warning = string.Empty;
				return false;
			}

			warning = _warnings.Dequeue();
			return true;
		}
	}

	/// <summary>
	/// Start-result value object used by <see cref="FakeSessionFactory"/>.
	/// </summary>
	private sealed class StartResult
	{
		private StartResult(bool started, bool toolNotFound, string warning, IPersistentInotifyMonitorSession? session)
		{
			Started = started;
			ToolNotFound = toolNotFound;
			Warning = warning;
			Session = session;
		}

		public bool Started
		{
			get;
		}

		public bool ToolNotFound
		{
			get;
		}

		public string Warning
		{
			get;
		}

		public IPersistentInotifyMonitorSession? Session
		{
			get;
		}

		public static StartResult FromStartedSession(IPersistentInotifyMonitorSession session)
		{
			return new StartResult(true, false, string.Empty, session);
		}

		public static StartResult FailedToolNotFound(string warning)
		{
			return new StartResult(false, true, warning, null);
		}
	}

	/// <summary>
	/// Fake monitor-session start factory with per-key queued outcomes.
	/// </summary>
	private sealed class FakeSessionFactory
	{
		private readonly Dictionary<string, Queue<StartResult>> _resultsByKey = new(StringComparer.Ordinal);
		private readonly Dictionary<string, int> _startCallsByKey = new(StringComparer.Ordinal);

		public void EnqueueStartResult(string watchPath, bool recursive, StartResult result)
		{
			string key = BuildKey(watchPath, recursive);
			if (!_resultsByKey.TryGetValue(key, out Queue<StartResult>? results))
			{
				results = new Queue<StartResult>();
				_resultsByKey[key] = results;
			}

			results.Enqueue(result);
		}

		public int GetStartCallCount(string watchPath, bool recursive)
		{
			string key = BuildKey(watchPath, recursive);
			return _startCallsByKey.TryGetValue(key, out int count) ? count : 0;
		}

		public (bool Started, bool ToolNotFound, string Warning, IPersistentInotifyMonitorSession? Session) TryStart(string watchPath, bool recursive)
		{
			string key = BuildKey(watchPath, recursive);
			_startCallsByKey[key] = GetStartCallCount(watchPath, recursive) + 1;
			if (_resultsByKey.TryGetValue(key, out Queue<StartResult>? results) && results.Count > 0)
			{
				StartResult next = results.Dequeue();
				return (next.Started, next.ToolNotFound, next.Warning, next.Session);
			}

			return (false, false, "No start result was configured for this session key.", null);
		}

		private static string BuildKey(string watchPath, bool recursive)
		{
			return $"{(recursive ? "r" : "s")}:{watchPath}";
		}
	}

	/// <summary>
	/// Queue-backed UTC clock used for deterministic restart-gate timestamps.
	/// </summary>
	private sealed class QueueClock
	{
		private readonly Queue<DateTimeOffset> _timestamps;

		public QueueClock(IEnumerable<DateTimeOffset> timestamps)
		{
			_timestamps = new Queue<DateTimeOffset>(timestamps);
		}

		public DateTimeOffset GetNowUtc()
		{
			if (_timestamps.Count == 0)
			{
				return DateTimeOffset.Parse("2026-02-17T00:00:08Z");
			}

			return _timestamps.Dequeue();
		}
	}
}
