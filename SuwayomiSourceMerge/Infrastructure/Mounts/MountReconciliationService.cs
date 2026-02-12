namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Computes deterministic mount, remount, and stale-unmount actions from desired and actual mount state.
/// </summary>
internal sealed class MountReconciliationService : IMountReconciliationService
{
	/// <summary>
	/// Token used to identify mergerfs filesystem types.
	/// </summary>
	private const string MERGERFS_TOKEN = "mergerfs";

	/// <summary>
	/// Path separator used for normalization and descendant checks.
	/// </summary>
	private const char PATH_SEPARATOR = '/';

	/// <inheritdoc />
	public MountReconciliationPlan Reconcile(MountReconciliationInput input)
	{
		ArgumentNullException.ThrowIfNull(input);

		DesiredMountDefinition[] orderedDesiredMounts = input.DesiredMounts
			.OrderBy(mount => mount.MountPoint, StringComparer.Ordinal)
			.ToArray();

		HashSet<string> desiredMountPointSet = BuildNormalizedDesiredMountPointSet(orderedDesiredMounts);
		HashSet<string> forcedRemountMountPointSet = BuildNormalizedMountPointSet(input.ForceRemountMountPoints);
		Dictionary<string, MountSnapshotEntry> actualByMountPoint = BuildActualMountLookup(input.ActualSnapshot.Entries);

		List<MountReconciliationAction> actions = [];
		foreach (DesiredMountDefinition desiredMount in orderedDesiredMounts)
		{
			string normalizedDesiredMountPoint = NormalizePath(desiredMount.MountPoint);
			MountReconciliationAction? action = ReconcileDesiredMount(
				desiredMount,
				normalizedDesiredMountPoint,
				input.EnableHealthChecks,
				forcedRemountMountPointSet,
				actualByMountPoint);
			if (action is not null)
			{
				actions.Add(action);
			}
		}

		MountReconciliationAction[] staleUnmountActions = BuildStaleUnmountActions(
			input.ActualSnapshot.Entries,
			input.ManagedMountRoots,
			desiredMountPointSet);
		actions.AddRange(staleUnmountActions);

		return new MountReconciliationPlan(actions);
	}

	/// <summary>
	/// Builds a set of desired mountpoints and rejects duplicates.
	/// </summary>
	/// <param name="orderedDesiredMounts">Desired mount definitions sorted by mountpoint.</param>
	/// <returns>Unique desired mountpoint set.</returns>
	/// <exception cref="ArgumentException">Thrown when duplicate desired mountpoints are found.</exception>
	private static HashSet<string> BuildNormalizedDesiredMountPointSet(IReadOnlyList<DesiredMountDefinition> orderedDesiredMounts)
	{
		HashSet<string> desiredMountPointSet = new(StringComparer.Ordinal);
		foreach (DesiredMountDefinition desiredMount in orderedDesiredMounts)
		{
			string normalizedMountPoint = NormalizePath(desiredMount.MountPoint);
			if (!desiredMountPointSet.Add(normalizedMountPoint))
			{
				throw new ArgumentException(
					$"Duplicate desired mountpoint '{desiredMount.MountPoint}' is not allowed after normalization.",
					nameof(orderedDesiredMounts));
			}
		}

		return desiredMountPointSet;
	}

	/// <summary>
	/// Builds a normalized mountpoint set from caller-provided values.
	/// </summary>
	/// <param name="mountPoints">Mountpoints to normalize into a set.</param>
	/// <returns>Normalized mountpoint set.</returns>
	private static HashSet<string> BuildNormalizedMountPointSet(IEnumerable<string> mountPoints)
	{
		HashSet<string> normalizedMountPoints = new(StringComparer.Ordinal);
		foreach (string mountPoint in mountPoints)
		{
			normalizedMountPoints.Add(NormalizePath(mountPoint));
		}

		return normalizedMountPoints;
	}

	/// <summary>
	/// Builds lookup of actual mount entries keyed by mountpoint, preserving first-seen entry on collisions.
	/// </summary>
	/// <param name="actualEntries">Actual mount snapshot entries.</param>
	/// <returns>Lookup from mountpoint to first-seen mount entry.</returns>
	private static Dictionary<string, MountSnapshotEntry> BuildActualMountLookup(IReadOnlyList<MountSnapshotEntry> actualEntries)
	{
		Dictionary<string, MountSnapshotEntry> lookup = new(StringComparer.Ordinal);
		foreach (MountSnapshotEntry entry in actualEntries)
		{
			string normalizedMountPoint = NormalizePath(entry.MountPoint);
			if (!lookup.ContainsKey(normalizedMountPoint))
			{
				lookup.Add(normalizedMountPoint, entry);
			}
		}

		return lookup;
	}

	/// <summary>
	/// Produces reconciliation action for one desired mount when needed.
	/// </summary>
	/// <param name="desiredMount">Desired mount definition.</param>
	/// <param name="normalizedDesiredMountPoint">Normalized desired mountpoint key.</param>
	/// <param name="enableHealthChecks">Whether unhealthy mount entries should be remounted.</param>
	/// <param name="forceRemountMountPoints">Mountpoints that should always be remounted.</param>
	/// <param name="actualByMountPoint">Lookup of actual entries by mountpoint.</param>
	/// <returns>Required reconciliation action, or <see langword="null"/> when no action is needed.</returns>
	private static MountReconciliationAction? ReconcileDesiredMount(
		DesiredMountDefinition desiredMount,
		string normalizedDesiredMountPoint,
		bool enableHealthChecks,
		IReadOnlySet<string> forceRemountMountPoints,
		IReadOnlyDictionary<string, MountSnapshotEntry> actualByMountPoint)
	{
		if (forceRemountMountPoints.Contains(normalizedDesiredMountPoint))
		{
			return BuildDesiredAction(desiredMount, MountReconciliationActionKind.Remount, MountReconciliationReason.ForcedRemount);
		}

		if (!actualByMountPoint.TryGetValue(normalizedDesiredMountPoint, out MountSnapshotEntry? actualEntry))
		{
			return BuildDesiredAction(desiredMount, MountReconciliationActionKind.Mount, MountReconciliationReason.MissingMount);
		}

		if (!IsMergerfsFileSystem(actualEntry.FileSystemType))
		{
			return BuildDesiredAction(
				desiredMount,
				MountReconciliationActionKind.Remount,
				MountReconciliationReason.NonMergerfsAtTarget);
		}

		string actualIdentity = ResolveActualIdentity(actualEntry);
		if (!string.Equals(actualIdentity, desiredMount.DesiredIdentity, StringComparison.Ordinal))
		{
			return BuildDesiredAction(
				desiredMount,
				MountReconciliationActionKind.Remount,
				MountReconciliationReason.DesiredIdentityMismatch);
		}

		if (enableHealthChecks && actualEntry.IsHealthy.HasValue && !actualEntry.IsHealthy.Value)
		{
			return BuildDesiredAction(
				desiredMount,
				MountReconciliationActionKind.Remount,
				MountReconciliationReason.UnhealthyMount);
		}

		return null;
	}

	/// <summary>
	/// Builds a mount or remount action using desired mount fields.
	/// </summary>
	/// <param name="desiredMount">Desired mount definition.</param>
	/// <param name="kind">Action kind.</param>
	/// <param name="reason">Action reason.</param>
	/// <returns>Action instance.</returns>
	private static MountReconciliationAction BuildDesiredAction(
		DesiredMountDefinition desiredMount,
		MountReconciliationActionKind kind,
		MountReconciliationReason reason)
	{
		return new MountReconciliationAction(
			kind,
			desiredMount.MountPoint,
			desiredMount.DesiredIdentity,
			desiredMount.MountPayload,
			reason);
	}

	/// <summary>
	/// Builds stale mergerfs unmount actions under managed roots.
	/// </summary>
	/// <param name="actualEntries">Actual mount snapshot entries.</param>
	/// <param name="managedMountRoots">Managed roots used for stale cleanup checks.</param>
	/// <param name="desiredMountPointSet">Desired mountpoint set.</param>
	/// <returns>Ordered stale unmount actions.</returns>
	private static MountReconciliationAction[] BuildStaleUnmountActions(
		IReadOnlyList<MountSnapshotEntry> actualEntries,
		IReadOnlyList<string> managedMountRoots,
		IReadOnlySet<string> desiredMountPointSet)
	{
		string[] normalizedManagedRoots = managedMountRoots
			.Select(NormalizePath)
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		MountReconciliationAction[] staleActions = actualEntries
			.Where(entry => IsMergerfsFileSystem(entry.FileSystemType))
			.Where(entry => IsUnderManagedRoot(entry.MountPoint, normalizedManagedRoots))
			.Where(entry => !desiredMountPointSet.Contains(NormalizePath(entry.MountPoint)))
			.Select(
				entry => new MountReconciliationAction(
					MountReconciliationActionKind.Unmount,
					entry.MountPoint,
					desiredIdentity: null,
					mountPayload: null,
					MountReconciliationReason.StaleMount))
			.OrderByDescending(action => GetPathDepth(action.MountPoint))
			.ThenBy(action => action.MountPoint, StringComparer.Ordinal)
			.ToArray();

		return staleActions;
	}

	/// <summary>
	/// Resolves actual mount identity from source first, then <c>fsname</c> option fallback.
	/// </summary>
	/// <param name="entry">Actual snapshot entry.</param>
	/// <returns>Resolved identity token, or empty string when unknown.</returns>
	private static string ResolveActualIdentity(MountSnapshotEntry entry)
	{
		if (!string.IsNullOrWhiteSpace(entry.Source))
		{
			return entry.Source;
		}

		if (TryExtractFsNameOption(entry.Options, out string? fsName))
		{
			return fsName!;
		}

		return string.Empty;
	}

	/// <summary>
	/// Attempts to extract <c>fsname</c> option value from a comma-separated options string.
	/// </summary>
	/// <param name="options">Options string.</param>
	/// <param name="fsName">Extracted fsname on success.</param>
	/// <returns><see langword="true"/> when fsname exists; otherwise <see langword="false"/>.</returns>
	private static bool TryExtractFsNameOption(string options, out string? fsName)
	{
		fsName = null;
		if (string.IsNullOrWhiteSpace(options))
		{
			return false;
		}

		string[] parts = options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string part in parts)
		{
			if (!part.StartsWith("fsname=", StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}

			string value = part["fsname=".Length..];
			if (string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			fsName = value;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Determines whether a filesystem type belongs to mergerfs.
	/// </summary>
	/// <param name="fileSystemType">Filesystem type text to inspect.</param>
	/// <returns><see langword="true"/> when mergerfs; otherwise <see langword="false"/>.</returns>
	private static bool IsMergerfsFileSystem(string fileSystemType)
	{
		return fileSystemType.Contains(MERGERFS_TOKEN, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Determines whether a mountpoint is at or below any managed root.
	/// </summary>
	/// <param name="mountPoint">Mountpoint path.</param>
	/// <param name="normalizedManagedRoots">Normalized managed roots.</param>
	/// <returns><see langword="true"/> when managed; otherwise <see langword="false"/>.</returns>
	private static bool IsUnderManagedRoot(string mountPoint, IReadOnlyList<string> normalizedManagedRoots)
	{
		string normalizedMountPoint = NormalizePath(mountPoint);
		foreach (string normalizedManagedRoot in normalizedManagedRoots)
		{
			if (IsPathAtOrUnderRoot(normalizedMountPoint, normalizedManagedRoot))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Normalizes path separators and trailing separators for deterministic comparisons.
	/// </summary>
	/// <param name="path">Path value to normalize.</param>
	/// <returns>Normalized path.</returns>
	private static string NormalizePath(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		string normalizedPath = path.Replace('\\', PATH_SEPARATOR);
		while (normalizedPath.Length > 1 && normalizedPath.EndsWith(PATH_SEPARATOR))
		{
			normalizedPath = normalizedPath[..^1];
		}

		return normalizedPath;
	}

	/// <summary>
	/// Determines whether a normalized path is equal to or below a normalized root.
	/// </summary>
	/// <param name="normalizedPath">Normalized path.</param>
	/// <param name="normalizedRoot">Normalized root.</param>
	/// <returns><see langword="true"/> when path is equal to root or root descendant; otherwise <see langword="false"/>.</returns>
	private static bool IsPathAtOrUnderRoot(string normalizedPath, string normalizedRoot)
	{
		if (string.Equals(normalizedPath, normalizedRoot, StringComparison.Ordinal))
		{
			return true;
		}

		if (!normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal))
		{
			return false;
		}

		if (normalizedRoot == PATH_SEPARATOR.ToString())
		{
			return true;
		}

		return normalizedPath.Length > normalizedRoot.Length
			&& normalizedPath[normalizedRoot.Length] == PATH_SEPARATOR;
	}

	/// <summary>
	/// Computes normalized path depth for deterministic stale unmount ordering.
	/// </summary>
	/// <param name="path">Path to measure.</param>
	/// <returns>Segment depth count.</returns>
	private static int GetPathDepth(string path)
	{
		string normalizedPath = NormalizePath(path);
		if (normalizedPath == PATH_SEPARATOR.ToString())
		{
			return 0;
		}

		return normalizedPath
			.Split(PATH_SEPARATOR, StringSplitOptions.RemoveEmptyEntries)
			.Length;
	}
}
