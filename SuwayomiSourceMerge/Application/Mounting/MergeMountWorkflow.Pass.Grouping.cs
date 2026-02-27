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
	/// <param name="overrideTitleCatalog">Existing override title catalog entries.</param>
	/// <param name="overrideCanonicalResolver">Override canonical resolver.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Grouped title state.</returns>
	private (IReadOnlyList<MergeTitleGroup> Groups, bool HadEnumerationFailure) BuildTitleGroups(
		IReadOnlyList<string> sourceVolumePaths,
		IReadOnlyList<OverrideTitleCatalogEntry> overrideTitleCatalog,
		OverrideCanonicalResolver overrideCanonicalResolver,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(sourceVolumePaths);
		ArgumentNullException.ThrowIfNull(overrideTitleCatalog);
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

		for (int index = 0; index < overrideTitleCatalog.Count; index++)
		{
			cancellationToken.ThrowIfCancellationRequested();
			OverrideTitleCatalogEntry overrideEntry = overrideTitleCatalog[index];
			string overrideTitle = overrideEntry.Title;

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
	/// Ensures title metadata artifacts for one branch plan.
	/// </summary>
	/// <param name="branchPlan">Branch planning output.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns><see langword="true"/> when Comick API service interruption occurred; otherwise <see langword="false"/>.</returns>
	private bool EnsureTitleMetadata(
		MergerfsBranchPlan branchPlan,
		CancellationToken cancellationToken)
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

		ComickMetadataCoordinatorRequest request = new(
			branchPlan.PreferredOverridePath,
			allOverrideDirectoryPaths,
			orderedSourceDirectories,
			BuildDisplayTitleFromMountPoint(branchPlan),
			_options.MetadataOrchestration);
		ComickMetadataCoordinatorResult result = _comickMetadataCoordinator.EnsureMetadata(
			request,
			cancellationToken);
		return result.HadServiceInterruption;
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

		string strippedTitle = TitleKeyNormalizer.StripTrailingSceneTagSuffixes(inputTitle, _sceneTagMatcher);
		if (!string.IsNullOrWhiteSpace(strippedTitle))
		{
			return strippedTitle;
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
	/// Discovers existing override title catalog entries.
	/// </summary>
	/// <param name="overrideVolumePaths">Override volume paths.</param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>Catalog entries in deterministic path order.</returns>
	private (IReadOnlyList<OverrideTitleCatalogEntry> Catalog, bool HadEnumerationFailure) DiscoverExistingOverrideTitleCatalog(
		IReadOnlyList<string> overrideVolumePaths,
		CancellationToken cancellationToken)
	{
		bool hadEnumerationFailure = false;
		List<OverrideTitleCatalogEntry> catalog = [];
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
			catch (Exception exception) when (!IsFatalException(exception))
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

				if (!TryCreateOverrideTitleCatalogEntry(title, titleDirectoryPath, out OverrideTitleCatalogEntry entry))
				{
					continue;
				}

				catalog.Add(entry);
			}
		}

		IReadOnlyList<OverrideTitleCatalogEntry> orderedCatalog = catalog
			.OrderBy(static entry => entry.DirectoryPath, StringComparer.Ordinal)
			.ThenBy(static entry => entry.Title, StringComparer.Ordinal)
			.ToArray();
		return (orderedCatalog, hadEnumerationFailure);
	}

	/// <summary>
	/// Attempts to create one override title catalog entry from discovered directory metadata.
	/// </summary>
	/// <param name="title">Discovered override title directory name.</param>
	/// <param name="titleDirectoryPath">Absolute override title directory path.</param>
	/// <param name="entry">Created catalog entry when successful.</param>
	/// <returns><see langword="true"/> when entry creation succeeds; otherwise <see langword="false"/>.</returns>
	private bool TryCreateOverrideTitleCatalogEntry(
		string title,
		string titleDirectoryPath,
		out OverrideTitleCatalogEntry entry)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentException.ThrowIfNullOrWhiteSpace(titleDirectoryPath);

		entry = null!;
		try
		{
			string normalizedKey = _titleComparisonNormalizer.NormalizeTitleKey(title);
			if (string.IsNullOrWhiteSpace(normalizedKey))
			{
				_logger.Warning(
					MergePassWarningEvent,
					"Skipped override title that normalizes to an empty comparison key.",
					BuildContext(
						("path", titleDirectoryPath),
						("title", title)));
				return false;
			}

			string strippedTitle = TitleKeyNormalizer.StripTrailingSceneTagSuffixes(title, _sceneTagMatcher);
			bool isSuffixTagged = !string.Equals(strippedTitle, title.Trim(), StringComparison.Ordinal);
			entry = new OverrideTitleCatalogEntry(
				title,
				titleDirectoryPath,
				normalizedKey,
				strippedTitle,
				isSuffixTagged);
			return true;
		}
		catch (Exception exception) when (!IsFatalException(exception))
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
		catch (Exception exception) when (!IsFatalException(exception))
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
	/// Logs override-canonical advisories for tagged-only title preservation.
	/// </summary>
	/// <param name="advisories">Resolver advisories.</param>
	private void LogOverrideCanonicalAdvisories(IReadOnlyList<OverrideCanonicalAdvisory> advisories)
	{
		ArgumentNullException.ThrowIfNull(advisories);

		for (int index = 0; index < advisories.Count; index++)
		{
			OverrideCanonicalAdvisory advisory = advisories[index];
			string selectedDirectoryPath = advisory.SelectedDirectoryPath;
			string? selectedParentPath = Path.GetDirectoryName(selectedDirectoryPath);
			if (string.IsNullOrWhiteSpace(selectedParentPath))
			{
				continue;
			}

			string suggestedDirectoryPath = Path.Combine(
				selectedParentPath,
				PathSafetyPolicy.EscapeReservedSegment(advisory.SuggestedStrippedTitle));
			// Display-only operator hint. The application does not execute this shell command.
			string renameCommand = string.Create(
				System.Globalization.CultureInfo.InvariantCulture,
				$"mv {QuoteForPosixShell(selectedDirectoryPath)} {QuoteForPosixShell(suggestedDirectoryPath)}");
			_logger.Warning(
				MergePassWarningEvent,
				"Preserved tagged-only override title to avoid creating a duplicate stripped directory. Rename manually to converge naming.",
				BuildContext(
					("normalized_key", advisory.NormalizedKey),
					("selected_title", advisory.SelectedTitle),
					("suggested_title", advisory.SuggestedStrippedTitle),
					("selected_path", selectedDirectoryPath),
					("suggested_path", suggestedDirectoryPath),
					("manual_rename_command", renameCommand)));
		}
	}

	/// <summary>
	/// Quotes one path for POSIX shell usage with single-quote safety.
	/// </summary>
	/// <remarks>
	/// This helper is used only for manual command text shown in warning logs.
	/// The runtime does not execute shell text produced by this method.
	/// </remarks>
	/// <param name="path">Path text to quote.</param>
	/// <returns>Quoted path safe for shell command rendering.</returns>
	private static string QuoteForPosixShell(string path)
	{
		ArgumentNullException.ThrowIfNull(path);
		return string.Create(
			System.Globalization.CultureInfo.InvariantCulture,
			$"'{path.Replace("'", "'\"'\"'", StringComparison.Ordinal)}'");
	}
}
