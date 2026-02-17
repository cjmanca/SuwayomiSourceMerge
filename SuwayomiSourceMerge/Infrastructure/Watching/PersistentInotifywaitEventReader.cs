using System.Diagnostics;
using System.Globalization;

namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Uses long-running <c>inotifywait -m</c> monitor sessions and batches queued events per poll.
/// </summary>
internal sealed partial class PersistentInotifywaitEventReader : IInotifyEventReader, IDisposable
{
	/// <summary>
	/// Poll cadence used while waiting for queued monitor events.
	/// </summary>
	private static readonly TimeSpan _pollWaitInterval = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Delay before retrying failed monitor session starts.
	/// </summary>
	private static readonly TimeSpan _sessionRestartDelay = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Maximum number of progressive deep-watch sessions started per poll.
	/// </summary>
	private const int MaxDeepSessionsStartedPerPoll = 3;

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
	/// Path key comparer for platform-specific filesystem semantics.
	/// </summary>
	private static readonly StringComparer _pathComparer = OperatingSystem.IsWindows()
		? StringComparer.OrdinalIgnoreCase
		: StringComparer.Ordinal;

	/// <summary>
	/// Path comparison mode for prefix checks.
	/// </summary>
	private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows()
		? StringComparison.OrdinalIgnoreCase
		: StringComparison.Ordinal;

	/// <summary>
	/// Synchronization lock for mutable session state.
	/// </summary>
	private readonly object _syncRoot = new();

	/// <summary>
	/// Startup mode controlling full versus progressive session initialization.
	/// </summary>
	private readonly InotifyWatchStartupMode _startupMode;

	/// <summary>
	/// Active monitor sessions keyed by normalized session identity.
	/// </summary>
	private readonly Dictionary<string, IPersistentInotifyMonitorSession> _sessions = new(_pathComparer);

	/// <summary>
	/// Retry gates for monitor sessions that failed to start.
	/// </summary>
	private readonly Dictionary<string, DateTimeOffset> _restartNotBeforeUtc = new(_pathComparer);

	/// <summary>
	/// Progressive-mode deep-watch roots that are desired for monitoring.
	/// </summary>
	private readonly HashSet<string> _knownProgressiveDeepRoots = new(_pathComparer);

	/// <summary>
	/// Progressive-mode deep-watch roots currently present in the pending start queue.
	/// </summary>
	private readonly HashSet<string> _queuedProgressiveDeepRoots = new(_pathComparer);

	/// <summary>
	/// Progressive-mode queue for deep-watch roots awaiting session startup.
	/// </summary>
	private readonly Queue<string> _pendingProgressiveDeepRoots = new();

	/// <summary>
	/// Progressive-mode roots that already had direct-child discovery seeded.
	/// </summary>
	private readonly HashSet<string> _seededProgressiveRoots = new(_pathComparer);

	/// <summary>
	/// Factory used to start monitor sessions.
	/// </summary>
	private readonly Func<string, bool, (bool Started, bool ToolNotFound, string Warning, IPersistentInotifyMonitorSession? Session)> _tryStartSession;

	/// <summary>
	/// Clock provider used for restart-gate timestamps.
	/// </summary>
	private readonly Func<DateTimeOffset> _utcNowProvider;

	/// <summary>
	/// Sleep delegate used by poll wait loop.
	/// </summary>
	private readonly Action<TimeSpan> _sleep;

	/// <summary>
	/// Tracks whether this reader has been disposed.
	/// </summary>
	private bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PersistentInotifywaitEventReader"/> class.
	/// </summary>
	/// <param name="startupMode">Watcher startup mode controlling full versus progressive monitor initialization.</param>
	public PersistentInotifywaitEventReader(InotifyWatchStartupMode startupMode = InotifyWatchStartupMode.Progressive)
		: this(
			startupMode,
			TryStartSession,
			static () => DateTimeOffset.UtcNow,
			static duration => Thread.Sleep(duration))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PersistentInotifywaitEventReader"/> class.
	/// </summary>
	/// <param name="startupMode">Watcher startup mode controlling full versus progressive monitor initialization.</param>
	/// <param name="tryStartSession">Session-start factory delegate.</param>
	/// <param name="utcNowProvider">Clock provider used for restart gates.</param>
	internal PersistentInotifywaitEventReader(
		InotifyWatchStartupMode startupMode,
		Func<string, bool, (bool Started, bool ToolNotFound, string Warning, IPersistentInotifyMonitorSession? Session)> tryStartSession,
		Func<DateTimeOffset> utcNowProvider)
		: this(
			startupMode,
			tryStartSession,
			utcNowProvider,
			static duration => Thread.Sleep(duration))
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PersistentInotifywaitEventReader"/> class.
	/// </summary>
	/// <param name="startupMode">Watcher startup mode controlling full versus progressive monitor initialization.</param>
	/// <param name="tryStartSession">Session-start factory delegate.</param>
	/// <param name="utcNowProvider">Clock provider used for restart gates.</param>
	/// <param name="sleep">Sleep delegate used by poll wait loop.</param>
	internal PersistentInotifywaitEventReader(
		InotifyWatchStartupMode startupMode,
		Func<string, bool, (bool Started, bool ToolNotFound, string Warning, IPersistentInotifyMonitorSession? Session)> tryStartSession,
		Func<DateTimeOffset> utcNowProvider,
		Action<TimeSpan> sleep)
	{
		_startupMode = startupMode;
		_tryStartSession = tryStartSession ?? throw new ArgumentNullException(nameof(tryStartSession));
		_utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
		_sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
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
			lock (_syncRoot)
			{
				ThrowIfDisposed();
				ReconcileDesiredMonitorState([]);
			}

			return new InotifyPollResult(InotifyPollOutcome.Success, [], warnings);
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

		bool hasExistingRoots = existingRoots.Count > 0;
		if (!hasExistingRoots)
		{
			warnings.Add("No existing watch roots were available for inotify polling.");
		}

		bool toolNotFound = false;
		bool commandFailed = false;
		DateTimeOffset nowUtc = _utcNowProvider();
		lock (_syncRoot)
		{
			ThrowIfDisposed();
			ReconcileDesiredMonitorState(existingRoots);
			if (hasExistingRoots)
			{
				EnsureSessions(existingRoots, nowUtc, warnings, ref toolNotFound, ref commandFailed);
				ReconcileProgressiveDeepSessionHealth();
				StartPendingProgressiveDeepSessions(nowUtc, warnings, ref toolNotFound, ref commandFailed);
			}
		}

		if (!hasExistingRoots)
		{
			return new InotifyPollResult(InotifyPollOutcome.Success, [], warnings);
		}

		List<InotifyEventRecord> events = [];
		DrainSessionQueues(events, warnings, queueDeepRoots: true);

		if (events.Count == 0)
		{
			Stopwatch wait = Stopwatch.StartNew();
			while (wait.Elapsed < timeout)
			{
				cancellationToken.ThrowIfCancellationRequested();
				TimeSpan remaining = timeout - wait.Elapsed;
				if (remaining <= TimeSpan.Zero)
				{
					break;
				}

				TimeSpan sleepDuration = remaining < _pollWaitInterval
					? remaining
					: _pollWaitInterval;
				_sleep(sleepDuration);
				DrainSessionQueues(events, warnings, queueDeepRoots: true);
				if (events.Count > 0)
				{
					break;
				}
			}
		}

		DateTimeOffset postWaitUtc = _utcNowProvider();
		lock (_syncRoot)
		{
			ThrowIfDisposed();
			ReconcileProgressiveDeepSessionHealth();
			StartPendingProgressiveDeepSessions(postWaitUtc, warnings, ref toolNotFound, ref commandFailed);
		}

		DrainSessionQueues(events, warnings, queueDeepRoots: false);
		InotifyPollOutcome outcome = ClassifyOutcome(events.Count, toolNotFound, commandFailed);
		return new InotifyPollResult(outcome, events, warnings);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		lock (_syncRoot)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			foreach (IPersistentInotifyMonitorSession session in _sessions.Values)
			{
				session.Dispose();
			}

			_sessions.Clear();
			_restartNotBeforeUtc.Clear();
			_knownProgressiveDeepRoots.Clear();
			_queuedProgressiveDeepRoots.Clear();
			_pendingProgressiveDeepRoots.Clear();
			_seededProgressiveRoots.Clear();
		}
	}

	/// <summary>
	/// Ensures required monitor sessions are active for the current roots.
	/// </summary>
	/// <param name="existingRoots">Existing watch roots.</param>
	/// <param name="nowUtc">Current timestamp used by restart gates.</param>
	/// <param name="warnings">Warning sink.</param>
	/// <param name="toolNotFound">Receives tool-not-found classification when startup fails for missing executable.</param>
	/// <param name="commandFailed">Receives generic command-failure classification for other startup failures.</param>
	private void EnsureSessions(
		IReadOnlyList<string> existingRoots,
		DateTimeOffset nowUtc,
		ICollection<string> warnings,
		ref bool toolNotFound,
		ref bool commandFailed)
	{
		if (_startupMode == InotifyWatchStartupMode.Full)
		{
			for (int index = 0; index < existingRoots.Count; index++)
			{
				EnsureSession(existingRoots[index], recursive: true, nowUtc, warnings, ref toolNotFound, ref commandFailed);
			}
			return;
		}

		for (int index = 0; index < existingRoots.Count; index++)
		{
			string root = existingRoots[index];
			EnsureSession(root, recursive: false, nowUtc, warnings, ref toolNotFound, ref commandFailed);
			SeedProgressiveRoot(root);
		}
	}

	/// <summary>
	/// Seeds progressive-mode deep-watch candidates from one root's direct child directories.
	/// </summary>
	/// <param name="rootPath">Root path to seed.</param>
	private void SeedProgressiveRoot(string rootPath)
	{
		if (_startupMode != InotifyWatchStartupMode.Progressive || _seededProgressiveRoots.Contains(rootPath))
		{
			return;
		}

		_seededProgressiveRoots.Add(rootPath);
		try
		{
			string[] childDirectories = Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly);
			for (int index = 0; index < childDirectories.Length; index++)
			{
				EnqueueProgressiveDeepRoot(childDirectories[index]);
			}
		}
		catch
		{
			// Best-effort progressive seeding. Enumeration failures are emitted by normal watcher fallback behavior.
		}
	}

	/// <summary>
	/// Drains queued events and warnings from active sessions.
	/// </summary>
	/// <param name="events">Event sink.</param>
	/// <param name="warnings">Warning sink.</param>
	/// <param name="queueDeepRoots">Whether progressive-mode deep roots should be discovered from shallow events.</param>
	private void DrainSessionQueues(ICollection<InotifyEventRecord> events, ICollection<string> warnings, bool queueDeepRoots)
	{
		IPersistentInotifyMonitorSession[] snapshot;
		lock (_syncRoot)
		{
			snapshot = _sessions.Values.ToArray();
		}

		for (int index = 0; index < snapshot.Length; index++)
		{
			IPersistentInotifyMonitorSession session = snapshot[index];
			while (session.TryDequeueWarning(out string warning))
			{
				warnings.Add(warning);
			}

			while (session.TryDequeueEvent(out InotifyEventRecord record))
			{
				if (queueDeepRoots && _startupMode == InotifyWatchStartupMode.Progressive && !session.IsRecursive)
				{
					TryEnqueueProgressiveDeepRootFromShallowEvent(session.WatchPath, record);
				}

				events.Add(record);
			}
		}
	}


	/// <summary>
	/// Classifies polling outcome from collected events and startup failures.
	/// </summary>
	/// <param name="eventCount">Collected event count.</param>
	/// <param name="toolNotFound">Whether startup failures indicated missing tool.</param>
	/// <param name="commandFailed">Whether startup failures indicated command failure.</param>
	/// <returns>Polling outcome classification.</returns>
	private static InotifyPollOutcome ClassifyOutcome(int eventCount, bool toolNotFound, bool commandFailed)
	{
		if (toolNotFound)
		{
			return InotifyPollOutcome.ToolNotFound;
		}

		if (commandFailed)
		{
			return InotifyPollOutcome.CommandFailed;
		}

		return eventCount > 0 ? InotifyPollOutcome.Success : InotifyPollOutcome.TimedOut;
	}

	/// <summary>
	/// Starts one monitor session and maps startup failures to typed tuple values.
	/// </summary>
	/// <param name="watchPath">Watch path.</param>
	/// <param name="recursive">Recursive mode flag.</param>
	/// <returns>Tuple describing startup success or failure classification.</returns>
	private static (bool Started, bool ToolNotFound, string Warning, IPersistentInotifyMonitorSession? Session) TryStartSession(
		string watchPath,
		bool recursive)
	{
		if (!InotifyMonitorSession.TryStart(
			watchPath,
			recursive,
			out InotifyMonitorSession? session,
			out SessionStartFailureKind failureKind,
			out string warning))
		{
			return (false, failureKind == SessionStartFailureKind.ToolNotFound, warning, null);
		}

		return (true, false, string.Empty, session);
	}

	/// <summary>
	/// Builds a normalized session key.
	/// </summary>
	/// <param name="watchPath">Watch path.</param>
	/// <param name="recursive">Recursive-mode flag.</param>
	/// <returns>Normalized session key.</returns>
	private static string BuildSessionKey(string watchPath, bool recursive)
	{
		string prefix = recursive ? "r:" : "s:";
		return string.Create(CultureInfo.InvariantCulture, $"{prefix}{NormalizePath(watchPath)}");
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> when reader has been disposed.
	/// </summary>
	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}

}
