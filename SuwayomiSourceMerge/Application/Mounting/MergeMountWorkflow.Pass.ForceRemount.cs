using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Mounts;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Force-remount selection behavior for <see cref="MergeMountWorkflow"/> merge passes.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
	/// <summary>
	/// Resolves force-remount mountpoints from force flags and dispatch reason.
	/// </summary>
	/// <param name="force">Force flag.</param>
	/// <param name="reason">Reason text.</param>
	/// <param name="desiredMounts">Desired mount definitions.</param>
	/// <param name="overrideCanonicalResolver">Override canonical resolver.</param>
	/// <returns>Force remount mountpoint set.</returns>
	private HashSet<string> ResolveForceRemountSet(
		bool force,
		string reason,
		IReadOnlyList<DesiredMountDefinition> desiredMounts,
		OverrideCanonicalResolver overrideCanonicalResolver)
	{
		HashSet<string> desiredMountPointSet = new(PathSafetyPolicy.GetPathComparer());
		for (int index = 0; index < desiredMounts.Count; index++)
		{
			desiredMountPointSet.Add(Path.GetFullPath(desiredMounts[index].MountPoint));
		}

		HashSet<string> forceSet = new(PathSafetyPolicy.GetPathComparer());
		if (!force)
		{
			return forceSet;
		}

		const string overrideForcePrefix = "override-force:";
		if (!reason.StartsWith(overrideForcePrefix, StringComparison.Ordinal))
		{
			forceSet.UnionWith(desiredMountPointSet);
			return forceSet;
		}

		string inputTitle = reason[overrideForcePrefix.Length..].Trim();
		if (string.IsNullOrWhiteSpace(inputTitle))
		{
			_logger.Warning(
				MergePassWarningEvent,
				"Override-force request did not include a title segment; force-remount set left empty.",
				BuildContext(("reason", reason)));
			return forceSet;
		}

		string canonicalTitle = ResolveCanonicalTitle(inputTitle, overrideCanonicalResolver);
		string mountPoint = BuildMountPointPath(canonicalTitle);
		if (desiredMountPointSet.Contains(mountPoint))
		{
			forceSet.Add(mountPoint);
			return forceSet;
		}

		_logger.Warning(
			MergePassWarningEvent,
			"Override-force title did not resolve to a desired mountpoint; force-remount set left empty.",
			BuildContext(
				("requested_title", inputTitle),
				("canonical_title", canonicalTitle)));
		return forceSet;
	}
}
