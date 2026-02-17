using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Domain.Normalization;
using SuwayomiSourceMerge.Infrastructure.Metadata;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Volumes;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Title grouping and canonicalization helpers for <see cref="MergeMountWorkflow"/> merge passes.
/// </summary>
internal sealed partial class MergeMountWorkflow
{
	/// <summary>
	/// Builds title groups from discovered source volumes.
	/// </summary>
	/// <param name="sourceVolumePaths">Source volume roots.</param>
	/// <param name="overrideTitles">Existing override titles.</param>
	/// <param name="overrideCanonicalResolver">Override canonical resolver.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Grouped title state.</returns>
	private (IReadOnlyList<MergeTitleGroup> Groups, bool HadEnumerationFailure) BuildTitleGroups(
		IReadOnlyList<string> sourceVolumePaths,
		IReadOnlyList<string> overrideTitles,
		OverrideCanonicalResolver overrideCanonicalResolver,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sourceVolumePaths);
		ArgumentNullException.ThrowIfNull(overrideTitles);
		ArgumentNullException.ThrowIfNull(overrideCanonicalResolver);

		bool hadEnumerationFailure = false;
		Dictionary<string, MergeTitleGroupBuilder> builders = new(StringComparer.Ordinal);
		for (int volumeIndex = 0; volumeIndex < sourceVolumePaths.Count; volumeIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string sourceVolumePath = sourceVolumePaths[volumeIndex];
			string[] sourceDirectoryPaths = EnumerateDirectoriesSafe(sourceVolumePath, ref hadEnumerationFailure);
			for (int sourceIndex = 0; sourceIndex < sourceDirectoryPaths.Length; sourceIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string sourceDirectoryPath = sourceDirectoryPaths[sourceIndex];
				string sourceName = Path.GetFileName(sourceDirectoryPath);
				if (string.IsNullOrWhiteSpace(sourceName))
				{
					continue;
				}

				string sourceKey = SourceNameKeyNormalizer.NormalizeSourceKey(sourceName);
				if (string.IsNullOrWhiteSpace(sourceKey) || _excludedSources.Contains(sourceKey))
				{
					continue;
				}

				string[] titleDirectoryPaths = EnumerateDirectoriesSafe(sourceDirectoryPath, ref hadEnumerationFailure);
				for (int titleIndex = 0; titleIndex < titleDirectoryPaths.Length; titleIndex++)
				{
					cancellationToken.ThrowIfCancellationRequested();
					string titleDirectoryPath = titleDirectoryPaths[titleIndex];
					string rawTitle = Path.GetFileName(titleDirectoryPath);
					if (string.IsNullOrWhiteSpace(rawTitle))
					{
						continue;
					}

					string canonicalTitle = ResolveCanonicalTitle(rawTitle, overrideCanonicalResolver);
					string groupKey = BuildGroupKey(canonicalTitle, rawTitle);
					if (!builders.TryGetValue(groupKey, out MergeTitleGroupBuilder? builder))
					{
						builder = new MergeTitleGroupBuilder(groupKey, canonicalTitle);
						builders.Add(groupKey, builder);
					}

					builder.AddSourceBranch(sourceName, Path.GetFullPath(titleDirectoryPath));
				}
			}
		}

		for (int index = 0; index < overrideTitles.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string overrideTitle = overrideTitles[index];
			if (string.IsNullOrWhiteSpace(overrideTitle))
			{
				continue;
			}

			string canonicalTitle = ResolveCanonicalTitle(overrideTitle, overrideCanonicalResolver);
			string groupKey = BuildGroupKey(canonicalTitle, overrideTitle);
			if (builders.ContainsKey(groupKey))
			{
				continue;
			}

			builders.Add(groupKey, new MergeTitleGroupBuilder(groupKey, canonicalTitle));
		}

		IReadOnlyList<MergeTitleGroup> groups = builders.Values
			.OrderBy(static builder => builder.CanonicalTitle, StringComparer.Ordinal)
			.ThenBy(static builder => builder.GroupKey, StringComparer.Ordinal)
			.Select(static builder => builder.Build())
			.ToArray();

		return (groups, hadEnumerationFailure);
	}

	/// <summary>
	/// Ensures details.json for one branch plan.
	/// </summary>
	/// <param name="branchPlan">Branch planning output.</param>
	private void EnsureDetailsJson(MergerfsBranchPlan branchPlan)
	{
		ArgumentNullException.ThrowIfNull(branchPlan);

		string[] allOverrideDirectoryPaths = branchPlan.BranchLinks
			.Where(static link => link.AccessMode == MergerfsBranchAccessMode.ReadWrite)
			.Select(static link => link.TargetPath)
			.Distinct(PathSafetyPolicy.GetPathComparer())
			.ToArray();
		string[] orderedSourceDirectories = branchPlan.BranchLinks
			.Where(static link => link.AccessMode == MergerfsBranchAccessMode.ReadOnly)
			.Select(static link => link.TargetPath)
			.ToArray();

		OverrideDetailsRequest request = new(
			branchPlan.PreferredOverridePath,
			allOverrideDirectoryPaths,
			orderedSourceDirectories,
			BuildDisplayTitleFromMountPoint(branchPlan),
			_options.DetailsDescriptionMode);
		_ = _overrideDetailsService.EnsureDetailsJson(request);
	}

	/// <summary>
	/// Resolves canonical title text from one mount plan.
	/// </summary>
	/// <param name="branchPlan">Branch plan.</param>
	/// <returns>Display title text.</returns>
	private static string BuildDisplayTitleFromMountPoint(MergerfsBranchPlan branchPlan)
	{
		string leafName = Path.GetFileName(branchPlan.PreferredOverridePath);
		return string.IsNullOrWhiteSpace(leafName)
			? branchPlan.GroupId
			: leafName;
	}

	/// <summary>
	/// Resolves canonical title from equivalence and override resolvers.
	/// </summary>
	/// <param name="inputTitle">Input title.</param>
	/// <param name="overrideCanonicalResolver">Override canonical resolver.</param>
	/// <returns>Canonical title.</returns>
	private string ResolveCanonicalTitle(string inputTitle, OverrideCanonicalResolver overrideCanonicalResolver)
	{
		if (_mangaEquivalenceService.TryResolveCanonicalTitle(inputTitle, out string canonicalTitle))
		{
			return canonicalTitle;
		}

		if (overrideCanonicalResolver.TryResolveOverrideCanonical(inputTitle, out string overrideCanonicalTitle))
		{
			return overrideCanonicalTitle;
		}

		return inputTitle.Trim();
	}

	/// <summary>
	/// Builds stable group key text for canonical grouping.
	/// </summary>
	/// <param name="canonicalTitle">Canonical title text.</param>
	/// <param name="rawTitle">Raw source title text.</param>
	/// <returns>Stable group key.</returns>
	private string BuildGroupKey(string canonicalTitle, string rawTitle)
	{
		string normalizedCanonical = _titleComparisonNormalizer.NormalizeTitleKey(canonicalTitle);
		if (!string.IsNullOrWhiteSpace(normalizedCanonical))
		{
			return normalizedCanonical;
		}

		string normalizedRaw = _titleComparisonNormalizer.NormalizeTitleKey(rawTitle);
		if (!string.IsNullOrWhiteSpace(normalizedRaw))
		{
			return normalizedRaw;
		}

		return $"group-{ComputeHash($"{canonicalTitle}|{rawTitle}")}";
	}

	/// <summary>
	/// Builds one merged-root mountpoint path for a canonical title.
	/// </summary>
	/// <param name="canonicalTitle">Canonical title text.</param>
	/// <returns>Absolute mountpoint path.</returns>
	private string BuildMountPointPath(string canonicalTitle)
	{
		string escapedTitle = PathSafetyPolicy.EscapeReservedSegment(canonicalTitle.Trim());
		return Path.GetFullPath(Path.Combine(_options.MergedRootPath, escapedTitle));
	}

	/// <summary>
	/// Discovers existing override title directory names.
	/// </summary>
	/// <param name="overrideVolumePaths">Override volume paths.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Directory names in deterministic first-seen order.</returns>
	private (IReadOnlyList<string> Titles, bool HadEnumerationFailure) DiscoverExistingOverrideTitles(
		IReadOnlyList<string> overrideVolumePaths,
		CancellationToken cancellationToken)
	{
		bool hadEnumerationFailure = false;
		HashSet<string> titles = new(StringComparer.Ordinal);
		List<string> orderedTitles = [];
		for (int volumeIndex = 0; volumeIndex < overrideVolumePaths.Count; volumeIndex++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			string overrideVolumePath = overrideVolumePaths[volumeIndex];
			if (!Directory.Exists(overrideVolumePath))
			{
				continue;
			}

			string[] directories;
			try
			{
				directories = Directory
					.EnumerateDirectories(overrideVolumePath, "*", SearchOption.TopDirectoryOnly)
					.OrderBy(static path => path, StringComparer.Ordinal)
					.ToArray();
			}
			catch (Exception exception)
			{
				hadEnumerationFailure = true;
				_logger.Warning(
					MergePassWarningEvent,
					"Failed to enumerate override title directories.",
					BuildContext(
						("path", overrideVolumePath),
						("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
						("message", exception.Message)));
				continue;
			}

			for (int directoryIndex = 0; directoryIndex < directories.Length; directoryIndex++)
			{
				cancellationToken.ThrowIfCancellationRequested();
				string titleDirectoryPath = directories[directoryIndex];
				string title = Path.GetFileName(titleDirectoryPath);
				if (string.IsNullOrWhiteSpace(title))
				{
					continue;
				}

				if (!IsValidOverrideResolverTitle(title, titleDirectoryPath))
				{
					continue;
				}

				if (titles.Add(title))
				{
					orderedTitles.Add(title);
				}
			}
		}

		return (orderedTitles, hadEnumerationFailure);
	}

	/// <summary>
	/// Returns deterministic top-level directory enumeration results for one root path.
	/// </summary>
	/// <param name="rootPath">Root path.</param>
	/// <returns>Ordered directory paths.</returns>
	private string[] EnumerateDirectoriesSafe(string rootPath, ref bool hadEnumerationFailure)
	{
		try
		{
			if (!Directory.Exists(rootPath))
			{
				return [];
			}

			return Directory
				.EnumerateDirectories(rootPath, "*", SearchOption.TopDirectoryOnly)
				.OrderBy(static path => path, StringComparer.Ordinal)
				.ToArray();
		}
		catch (Exception exception)
		{
			hadEnumerationFailure = true;
			_logger.Warning(
				MergePassWarningEvent,
				"Failed to enumerate directories.",
				BuildContext(
					("path", rootPath),
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
			return [];
		}
	}

	/// <summary>
	/// Logs container volume discovery warnings.
	/// </summary>
	/// <param name="discoveryResult">Discovery result.</param>
	private void LogVolumeDiscoveryWarnings(ContainerVolumeDiscoveryResult discoveryResult)
	{
		for (int index = 0; index < discoveryResult.Warnings.Count; index++)
		{
			ContainerVolumeDiscoveryWarning warning = discoveryResult.Warnings[index];
			_logger.Warning(
				MergePassWarningEvent,
				warning.Message,
				BuildContext(
					("warning_code", warning.Code),
					("root", warning.RootPath)));
		}
	}

	/// <summary>
	/// Determines whether source-volume discovery reported warnings for this pass.
	/// </summary>
	/// <param name="discoveryResult">Volume discovery result.</param>
	/// <returns><see langword="true"/> when source discovery emitted warnings; otherwise <see langword="false"/>.</returns>
	private bool HasSourceDiscoveryWarnings(ContainerVolumeDiscoveryResult discoveryResult)
	{
		ArgumentNullException.ThrowIfNull(discoveryResult);

		StringComparer pathComparer = PathSafetyPolicy.GetPathComparer();
		string sourceRootPath = Path.GetFullPath(_options.SourcesRootPath);
		for (int index = 0; index < discoveryResult.Warnings.Count; index++)
		{
			ContainerVolumeDiscoveryWarning warning = discoveryResult.Warnings[index];
			string warningRootPath = Path.GetFullPath(warning.RootPath);
			if (pathComparer.Equals(warningRootPath, sourceRootPath))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Returns whether one discovered override title should be included in resolver lookup construction.
	/// </summary>
	/// <param name="title">Discovered override title directory name.</param>
	/// <param name="titleDirectoryPath">Absolute override title directory path.</param>
	/// <returns><see langword="true"/> when title is valid for resolver lookup construction.</returns>
	private bool IsValidOverrideResolverTitle(string title, string titleDirectoryPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentException.ThrowIfNullOrWhiteSpace(titleDirectoryPath);

		try
		{
			string normalizedKey = _titleComparisonNormalizer.NormalizeTitleKey(title);
			if (!string.IsNullOrWhiteSpace(normalizedKey))
			{
				return true;
			}

			_logger.Warning(
				MergePassWarningEvent,
				"Skipped override title that normalizes to an empty comparison key.",
				BuildContext(
					("path", titleDirectoryPath),
					("title", title)));
			return false;
		}
		catch (Exception exception)
		{
			_logger.Warning(
				MergePassWarningEvent,
				"Skipped override title because normalization failed.",
				BuildContext(
					("path", titleDirectoryPath),
					("title", title),
					("exception_type", exception.GetType().FullName ?? exception.GetType().Name),
					("message", exception.Message)));
			return false;
		}
	}
}
