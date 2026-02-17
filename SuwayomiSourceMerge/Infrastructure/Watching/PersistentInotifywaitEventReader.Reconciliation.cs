namespace SuwayomiSourceMerge.Infrastructure.Watching;

internal sealed partial class PersistentInotifywaitEventReader
{
	/// <summary>
	/// Reconciles active session and progressive state to the currently desired watch roots.
	/// </summary>
	/// <param name="existingRoots">Normalized watch roots that currently exist.</param>
	private void ReconcileDesiredMonitorState(IReadOnlyList<string> existingRoots)
	{
		HashSet<string> existingRootSet = new(existingRoots, _pathComparer);
		if (_startupMode == InotifyWatchStartupMode.Progressive)
		{
			ReconcileProgressiveState(existingRootSet);
		}
		else
		{
			_knownProgressiveDeepRoots.Clear();
			_queuedProgressiveDeepRoots.Clear();
			_pendingProgressiveDeepRoots.Clear();
			_seededProgressiveRoots.Clear();
		}

		HashSet<string> desiredSessionKeys = BuildDesiredSessionKeys(existingRootSet);
		string[] sessionKeys = _sessions.Keys.ToArray();
		for (int index = 0; index < sessionKeys.Length; index++)
		{
			string key = sessionKeys[index];
			if (desiredSessionKeys.Contains(key))
			{
				continue;
			}

			if (_sessions.TryGetValue(key, out IPersistentInotifyMonitorSession? session))
			{
				session.Dispose();
				_sessions.Remove(key);
			}
		}

		string[] restartKeys = _restartNotBeforeUtc.Keys.ToArray();
		for (int index = 0; index < restartKeys.Length; index++)
		{
			string key = restartKeys[index];
			if (!desiredSessionKeys.Contains(key))
			{
				_restartNotBeforeUtc.Remove(key);
			}
		}
	}

	/// <summary>
	/// Reconciles progressive-only state to current roots and drops orphaned queue entries.
	/// </summary>
	/// <param name="existingRootSet">Set of currently existing normalized roots.</param>
	private void ReconcileProgressiveState(HashSet<string> existingRootSet)
	{
		string[] seededRoots = _seededProgressiveRoots.ToArray();
		for (int index = 0; index < seededRoots.Length; index++)
		{
			string seededRoot = seededRoots[index];
			if (!existingRootSet.Contains(seededRoot))
			{
				_seededProgressiveRoots.Remove(seededRoot);
			}
		}

		string[] deepRoots = _knownProgressiveDeepRoots.ToArray();
		for (int index = 0; index < deepRoots.Length; index++)
		{
			string deepRoot = deepRoots[index];
			if (!IsPathUnderAnyRoot(deepRoot, existingRootSet))
			{
				RemoveDesiredProgressiveDeepRoot(deepRoot);
			}
		}

		if (_pendingProgressiveDeepRoots.Count == 0)
		{
			return;
		}

		Queue<string> retainedPending = new();
		int pendingSnapshotCount = _pendingProgressiveDeepRoots.Count;
		for (int index = 0; index < pendingSnapshotCount; index++)
		{
			string pendingRoot = _pendingProgressiveDeepRoots.Dequeue();
			if (!_queuedProgressiveDeepRoots.Contains(pendingRoot))
			{
				continue;
			}

			if (!IsPathUnderAnyRoot(pendingRoot, existingRootSet))
			{
				_queuedProgressiveDeepRoots.Remove(pendingRoot);
				continue;
			}

			retainedPending.Enqueue(pendingRoot);
		}

		while (retainedPending.Count > 0)
		{
			_pendingProgressiveDeepRoots.Enqueue(retainedPending.Dequeue());
		}
	}

	/// <summary>
	/// Builds desired session keys for the current startup mode and root set.
	/// </summary>
	/// <param name="existingRootSet">Set of currently existing normalized roots.</param>
	/// <returns>Desired session key set.</returns>
	private HashSet<string> BuildDesiredSessionKeys(HashSet<string> existingRootSet)
	{
		HashSet<string> desiredSessionKeys = new(_pathComparer);
		foreach (string root in existingRootSet)
		{
			if (_startupMode == InotifyWatchStartupMode.Full)
			{
				desiredSessionKeys.Add(BuildSessionKey(root, recursive: true));
			}
			else
			{
				desiredSessionKeys.Add(BuildSessionKey(root, recursive: false));
			}
		}

		if (_startupMode != InotifyWatchStartupMode.Progressive)
		{
			return desiredSessionKeys;
		}

		foreach (string deepRoot in _knownProgressiveDeepRoots)
		{
			if (IsPathUnderAnyRoot(deepRoot, existingRootSet))
			{
				desiredSessionKeys.Add(BuildSessionKey(deepRoot, recursive: true));
			}
		}

		return desiredSessionKeys;
	}

	/// <summary>
	/// Returns whether one candidate path is equal to or beneath any configured root.
	/// </summary>
	/// <param name="candidatePath">Candidate path.</param>
	/// <param name="roots">Configured roots.</param>
	/// <returns><see langword="true"/> when candidate path is under any root.</returns>
	private static bool IsPathUnderAnyRoot(string candidatePath, IEnumerable<string> roots)
	{
		foreach (string root in roots)
		{
			if (TryGetRelativePath(root, candidatePath, out _))
			{
				return true;
			}
		}

		return false;
	}
}
