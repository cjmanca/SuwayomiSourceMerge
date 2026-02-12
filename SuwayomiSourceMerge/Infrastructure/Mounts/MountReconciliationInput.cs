namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Supplies desired and actual state inputs used by mount reconciliation.
/// </summary>
internal sealed class MountReconciliationInput
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MountReconciliationInput"/> class.
	/// </summary>
	/// <param name="desiredMounts">Desired mount definitions.</param>
	/// <param name="actualSnapshot">Actual captured snapshot.</param>
	/// <param name="managedMountRoots">Roots under which stale mergerfs mounts may be unmounted.</param>
	/// <param name="enableHealthChecks">Whether unhealthy mount entries should be remounted.</param>
	/// <param name="forceRemountMountPoints">Mountpoints that should always be remounted.</param>
	/// <exception cref="ArgumentNullException">Thrown when required collections are <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when collection items contain invalid values.</exception>
	public MountReconciliationInput(
		IReadOnlyList<DesiredMountDefinition> desiredMounts,
		MountSnapshot actualSnapshot,
		IReadOnlyList<string> managedMountRoots,
		bool enableHealthChecks,
		IReadOnlySet<string> forceRemountMountPoints)
	{
		ArgumentNullException.ThrowIfNull(desiredMounts);
		ArgumentNullException.ThrowIfNull(actualSnapshot);
		ArgumentNullException.ThrowIfNull(managedMountRoots);
		ArgumentNullException.ThrowIfNull(forceRemountMountPoints);

		for (int index = 0; index < desiredMounts.Count; index++)
		{
			if (desiredMounts[index] is null)
			{
				throw new ArgumentException(
					$"Desired mounts must not contain null items. Null item at index {index}.",
					nameof(desiredMounts));
			}
		}

		string[] rootArray = managedMountRoots.ToArray();
		for (int index = 0; index < rootArray.Length; index++)
		{
			if (string.IsNullOrWhiteSpace(rootArray[index]))
			{
				throw new ArgumentException(
					$"Managed mount roots must not contain null, empty, or whitespace values. Invalid root at index {index}.",
					nameof(managedMountRoots));
			}
		}

		HashSet<string> forcedRemountSet = new(StringComparer.Ordinal);
		foreach (string mountPoint in forceRemountMountPoints)
		{
			if (string.IsNullOrWhiteSpace(mountPoint))
			{
				throw new ArgumentException(
					"Force remount mountpoints must not contain null, empty, or whitespace values.",
					nameof(forceRemountMountPoints));
			}

			forcedRemountSet.Add(mountPoint);
		}

		DesiredMounts = desiredMounts.ToArray();
		ActualSnapshot = actualSnapshot;
		ManagedMountRoots = rootArray;
		EnableHealthChecks = enableHealthChecks;
		ForceRemountMountPoints = forcedRemountSet;
	}

	/// <summary>
	/// Gets the desired mount definitions.
	/// </summary>
	public IReadOnlyList<DesiredMountDefinition> DesiredMounts
	{
		get;
	}

	/// <summary>
	/// Gets the actual captured mount snapshot.
	/// </summary>
	public MountSnapshot ActualSnapshot
	{
		get;
	}

	/// <summary>
	/// Gets managed roots under which stale mergerfs mounts may be unmounted.
	/// </summary>
	public IReadOnlyList<string> ManagedMountRoots
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether unhealthy mount entries should be remounted.
	/// </summary>
	public bool EnableHealthChecks
	{
		get;
	}

	/// <summary>
	/// Gets mountpoints that should be remounted even when identities match.
	/// </summary>
	public IReadOnlySet<string> ForceRemountMountPoints
	{
		get;
	}
}
