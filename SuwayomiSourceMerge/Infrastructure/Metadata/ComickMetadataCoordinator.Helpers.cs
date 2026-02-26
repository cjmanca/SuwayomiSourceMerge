using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Helper and persistence logic for <see cref="ComickMetadataCoordinator"/>.
/// </summary>
internal sealed partial class ComickMetadataCoordinator
{
	/// <summary>
	/// Attempts to apply one Comick-driven manga-equivalents update for the matched comic payload.
	/// </summary>
	/// <param name="matchedComic">Matched Comick payload.</param>
	/// <param name="preferredLanguage">Preferred language for canonical selection.</param>
	private void TryUpdateMangaEquivalents(ComickComicResponse matchedComic, string preferredLanguage)
	{
		ArgumentNullException.ThrowIfNull(matchedComic);
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);

		if (_mangaEquivalenceCatalog is null || matchedComic.Comic is null)
		{
			return;
		}

		string mainTitle = matchedComic.Comic.Title;
		if (string.IsNullOrWhiteSpace(mainTitle))
		{
			return;
		}

		List<MangaEquivalentAlternateTitle> alternateTitles = [];
		if (matchedComic.Comic.MdTitles is not null)
		{
			for (int index = 0; index < matchedComic.Comic.MdTitles.Count; index++)
			{
				ComickTitleAlias? alias = matchedComic.Comic.MdTitles[index];
				if (alias is null || string.IsNullOrWhiteSpace(alias.Title))
				{
					continue;
				}

				alternateTitles.Add(new MangaEquivalentAlternateTitle(alias.Title, alias.Language));
			}
		}

		try
		{
			_ = _mangaEquivalenceCatalog.Update(
				new MangaEquivalentsUpdateRequest(
					_mangaEquivalentsYamlPath,
					mainTitle,
					alternateTitles,
					preferredLanguage));
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			// Best-effort update: merge-pass metadata coordination should continue even if alias sync fails.
		}
	}

	/// <summary>
	/// Determines whether one normalized title key is currently inside the persisted cooldown window.
	/// </summary>
	/// <param name="normalizedTitleKey">Normalized title key.</param>
	/// <param name="nowUtc">Current UTC timestamp.</param>
	/// <returns><see langword="true"/> when cooldown is active; otherwise <see langword="false"/>.</returns>
	private bool IsCooldownActive(string normalizedTitleKey, DateTimeOffset nowUtc)
	{
		if (string.IsNullOrWhiteSpace(normalizedTitleKey))
		{
			return false;
		}

		try
		{
			MetadataStateSnapshot snapshot = _metadataStateStore.Read();
			return snapshot.TitleCooldownsUtc.TryGetValue(normalizedTitleKey, out DateTimeOffset cooldownUntilUtc) &&
				cooldownUntilUtc > nowUtc;
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			return false;
		}
	}

	/// <summary>
	/// Attempts to persist one per-title cooldown timestamp.
	/// </summary>
	/// <param name="normalizedTitleKey">Normalized title key.</param>
	/// <param name="cooldownUntilUtc">Cooldown expiry timestamp.</param>
	private void TryPersistCooldown(string normalizedTitleKey, DateTimeOffset cooldownUntilUtc)
	{
		if (string.IsNullOrWhiteSpace(normalizedTitleKey))
		{
			return;
		}

		DateTimeOffset normalizedCooldownUntilUtc = cooldownUntilUtc.ToUniversalTime();
		try
		{
			_metadataStateStore.Transform(
				current =>
				{
					Dictionary<string, DateTimeOffset> updatedCooldowns = new(current.TitleCooldownsUtc, StringComparer.Ordinal)
					{
						[normalizedTitleKey] = normalizedCooldownUntilUtc
					};
					return new MetadataStateSnapshot(
						updatedCooldowns,
						current.StickyFlaresolverrUntilUtc);
				});
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			// Cooldown persistence is best-effort and must not block merge-pass metadata flow.
		}
	}

	/// <summary>
	/// Determines whether the search outcome should be treated as Comick service interruption.
	/// </summary>
	/// <param name="outcome">Search outcome.</param>
	/// <returns><see langword="true"/> when outcome indicates interruption; otherwise <see langword="false"/>.</returns>
	private static bool IsServiceInterruptionOutcome(ComickDirectApiOutcome outcome)
	{
		return outcome == ComickDirectApiOutcome.TransportFailure ||
			outcome == ComickDirectApiOutcome.Cancelled ||
			outcome == ComickDirectApiOutcome.CloudflareBlocked ||
			outcome == ComickDirectApiOutcome.HttpFailure ||
			outcome == ComickDirectApiOutcome.MalformedPayload;
	}

	/// <summary>
	/// Builds expected title values used by candidate matching.
	/// </summary>
	/// <param name="displayTitle">Display title.</param>
	/// <returns>Expected title values in deterministic order.</returns>
	private IReadOnlyList<string> BuildExpectedTitles(string displayTitle)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);

		List<string> expectedTitles = [displayTitle];
		if (_mangaEquivalenceCatalog is null)
		{
			return expectedTitles;
		}

		if (_mangaEquivalenceCatalog.TryResolveCanonicalTitle(displayTitle, out string canonicalTitle) &&
			!string.Equals(canonicalTitle, displayTitle, StringComparison.Ordinal))
		{
			expectedTitles.Add(canonicalTitle);
		}

		return expectedTitles;
	}

	/// <summary>
	/// Resolves the first usable Comick cover key from the matched payload.
	/// </summary>
	/// <param name="matchedComic">Matched Comick payload.</param>
	/// <returns>Cover key when available; otherwise <see langword="null"/>.</returns>
	private static string? TryResolveCoverKey(ComickComicResponse matchedComic)
	{
		ArgumentNullException.ThrowIfNull(matchedComic);

		ComickComicDetails? comic = matchedComic.Comic;
		if (comic?.MdCovers is null)
		{
			return null;
		}

		for (int index = 0; index < comic.MdCovers.Count; index++)
		{
			ComickCover? cover = comic.MdCovers[index];
			if (cover is null || string.IsNullOrWhiteSpace(cover.B2Key))
			{
				continue;
			}

			return cover.B2Key.Trim();
		}

		return null;
	}

	/// <summary>
	/// Determines whether one metadata artifact already exists in preferred/all override directories.
	/// </summary>
	/// <param name="request">Coordinator request.</param>
	/// <param name="fileName">Artifact file name.</param>
	/// <returns><see langword="true"/> when artifact exists; otherwise <see langword="false"/>.</returns>
	private static bool ArtifactExists(ComickMetadataCoordinatorRequest request, string fileName)
	{
		return TryFindExistingArtifactPath(request, fileName, out _);
	}

	/// <summary>
	/// Attempts to find one metadata artifact path in preferred/all override directories.
	/// </summary>
	/// <param name="request">Coordinator request.</param>
	/// <param name="fileName">Artifact file name.</param>
	/// <param name="artifactPath">Artifact path when found.</param>
	/// <returns><see langword="true"/> when found; otherwise <see langword="false"/>.</returns>
	private static bool TryFindExistingArtifactPath(
		ComickMetadataCoordinatorRequest request,
		string fileName,
		out string? artifactPath)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

		HashSet<string> checkedDirectories = new(StringComparer.Ordinal);
		if (checkedDirectories.Add(request.PreferredOverrideDirectoryPath))
		{
			string preferredPath = Path.Combine(request.PreferredOverrideDirectoryPath, fileName);
			if (File.Exists(preferredPath))
			{
				artifactPath = preferredPath;
				return true;
			}
		}

		for (int index = 0; index < request.AllOverrideDirectoryPaths.Count; index++)
		{
			string overrideDirectoryPath = request.AllOverrideDirectoryPaths[index];
			if (!checkedDirectories.Add(overrideDirectoryPath))
			{
				continue;
			}

			string path = Path.Combine(overrideDirectoryPath, fileName);
			if (File.Exists(path))
			{
				artifactPath = path;
				return true;
			}
		}

		artifactPath = null;
		return false;
	}

	/// <summary>
	/// Determines whether an exception should be treated as fatal and rethrown.
	/// </summary>
	/// <param name="exception">Exception to inspect.</param>
	/// <returns><see langword="true"/> when exception is fatal; otherwise <see langword="false"/>.</returns>
	private static bool IsFatalException(Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		return exception is OutOfMemoryException
			|| exception is StackOverflowException
			|| exception is AccessViolationException;
	}
}
