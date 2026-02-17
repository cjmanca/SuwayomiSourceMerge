namespace SuwayomiSourceMerge.Infrastructure.Watching;

internal sealed partial class PersistentInotifywaitEventReader
{
	/// <summary>
	/// Reconciles progressive deep-session health by queueing missing or stopped desired sessions.
	/// </summary>
	private void ReconcileProgressiveDeepSessionHealth()
	{
		if (_startupMode != InotifyWatchStartupMode.Progressive)
		{
			return;
		}

		string[] desiredRoots = _knownProgressiveDeepRoots.ToArray();
		for (int index = 0; index < desiredRoots.Length; index++)
		{
			string deepRoot = desiredRoots[index];
			if (_queuedProgressiveDeepRoots.Contains(deepRoot))
			{
				continue;
			}

			if (!Directory.Exists(deepRoot))
			{
				RemoveDesiredProgressiveDeepRoot(deepRoot);
				continue;
			}

			string sessionKey = BuildSessionKey(deepRoot, recursive: true);
			if (_sessions.TryGetValue(sessionKey, out IPersistentInotifyMonitorSession? session) && session.IsRunning)
			{
				continue;
			}

			EnqueueProgressiveDeepRoot(deepRoot);
		}
	}

	/// <summary>
	/// Starts queued progressive deep-watch sessions under a bounded per-start-phase budget.
	/// </summary>
	/// <param name="nowUtc">Current timestamp used by restart gates.</param>
	/// <param name="warnings">Warning sink.</param>
	/// <param name="toolNotFound">Receives tool-not-found classification when startup fails for missing executable.</param>
	/// <param name="commandFailed">Receives generic command-failure classification for other startup failures.</param>
	private void StartPendingProgressiveDeepSessions(
		DateTimeOffset nowUtc,
		ICollection<string> warnings,
		ref bool toolNotFound,
		ref bool commandFailed)
	{
		if (_startupMode != InotifyWatchStartupMode.Progressive)
		{
			return;
		}

		int started = 0;
		int processed = 0;
		int pendingSnapshotCount = _pendingProgressiveDeepRoots.Count;
		while (processed < pendingSnapshotCount && _pendingProgressiveDeepRoots.Count > 0 && started < MaxDeepSessionsStartedPerStartPhase)
		{
			string deepRoot = _pendingProgressiveDeepRoots.Dequeue();
			_queuedProgressiveDeepRoots.Remove(deepRoot);
			processed++;

			if (!Directory.Exists(deepRoot))
			{
				RemoveDesiredProgressiveDeepRoot(deepRoot);
				continue;
			}

			EnsureSessionResult ensureResult = EnsureSession(
				deepRoot,
				recursive: true,
				nowUtc,
				warnings,
				ref toolNotFound,
				ref commandFailed);

			if (ensureResult == EnsureSessionResult.Started || ensureResult == EnsureSessionResult.Running)
			{
				started++;
				continue;
			}

			if (ensureResult == EnsureSessionResult.RetryDeferred || ensureResult == EnsureSessionResult.FailedStart)
			{
				EnqueueProgressiveDeepRoot(deepRoot);
			}
		}
	}

	/// <summary>
	/// Ensures one monitor session is running for the supplied watch path.
	/// </summary>
	/// <param name="watchPath">Watch path for the session.</param>
	/// <param name="recursive">Whether recursive monitoring should be enabled.</param>
	/// <param name="nowUtc">Current timestamp used by restart gates.</param>
	/// <param name="warnings">Warning sink.</param>
	/// <param name="toolNotFound">Receives tool-not-found classification when startup fails for missing executable.</param>
	/// <param name="commandFailed">Receives generic command-failure classification for other startup failures.</param>
	private EnsureSessionResult EnsureSession(
		string watchPath,
		bool recursive,
		DateTimeOffset nowUtc,
		ICollection<string> warnings,
		ref bool toolNotFound,
		ref bool commandFailed)
	{
		string key = BuildSessionKey(watchPath, recursive);
		if (_sessions.TryGetValue(key, out IPersistentInotifyMonitorSession? existing))
		{
			if (existing.IsRunning)
			{
				return EnsureSessionResult.Running;
			}

			existing.Dispose();
			_sessions.Remove(key);
			_restartNotBeforeUtc[key] = nowUtc + _sessionRestartDelay;
		}

		if (_restartNotBeforeUtc.TryGetValue(key, out DateTimeOffset notBeforeUtc) && nowUtc < notBeforeUtc)
		{
			return EnsureSessionResult.RetryDeferred;
		}

		(bool started, bool startFailedForMissingTool, string warning, IPersistentInotifyMonitorSession? session) = _tryStartSession(watchPath, recursive);
		if (!started)
		{
			if (!string.IsNullOrWhiteSpace(warning))
			{
				warnings.Add(warning);
			}

			_restartNotBeforeUtc[key] = nowUtc + _sessionRestartDelay;
			toolNotFound |= startFailedForMissingTool;
			commandFailed |= !startFailedForMissingTool;
			return EnsureSessionResult.FailedStart;
		}

		_sessions[key] = session!;
		_restartNotBeforeUtc.Remove(key);
		return EnsureSessionResult.Started;
	}

	/// <summary>
	/// Attempts to enqueue a deep progressive watch root from one shallow root event.
	/// </summary>
	/// <param name="shallowRootPath">Shallow root path where the event was observed.</param>
	/// <param name="record">Observed event record.</param>
	private void TryEnqueueProgressiveDeepRootFromShallowEvent(string shallowRootPath, InotifyEventRecord record)
	{
		if (!record.IsDirectory || (record.EventMask & (InotifyEventMask.Delete | InotifyEventMask.MovedFrom)) != 0)
		{
			return;
		}

		if (!TryGetRelativePath(shallowRootPath, record.Path, out string relativePath))
		{
			return;
		}

		string[] segments = SplitPathSegments(relativePath);
		if (segments.Length != 1 || string.IsNullOrWhiteSpace(segments[0]))
		{
			return;
		}

		EnqueueProgressiveDeepRoot(record.Path);
	}

	/// <summary>
	/// Adds one deep progressive watch root to the pending queue when first discovered.
	/// </summary>
	/// <param name="rootPath">Root path to queue.</param>
	private void EnqueueProgressiveDeepRoot(string rootPath)
	{
		string normalizedRoot = NormalizePath(rootPath);
		lock (_syncRoot)
		{
			_ = _knownProgressiveDeepRoots.Add(normalizedRoot);
			if (_queuedProgressiveDeepRoots.Add(normalizedRoot))
			{
				_pendingProgressiveDeepRoots.Enqueue(normalizedRoot);
			}
		}
	}

	/// <summary>
	/// Removes one desired progressive deep-watch root and any associated session state.
	/// </summary>
	/// <param name="deepRoot">Normalized deep-watch root.</param>
	private void RemoveDesiredProgressiveDeepRoot(string deepRoot)
	{
		_knownProgressiveDeepRoots.Remove(deepRoot);
		_queuedProgressiveDeepRoots.Remove(deepRoot);

		string key = BuildSessionKey(deepRoot, recursive: true);
		if (_sessions.TryGetValue(key, out IPersistentInotifyMonitorSession? session))
		{
			session.Dispose();
			_sessions.Remove(key);
		}

		_restartNotBeforeUtc.Remove(key);
	}

	/// <summary>
	/// Classifies the outcome of one ensure-session pass.
	/// </summary>
	private enum EnsureSessionResult
	{
		Running,
		Started,
		RetryDeferred,
		FailedStart
	}
}
