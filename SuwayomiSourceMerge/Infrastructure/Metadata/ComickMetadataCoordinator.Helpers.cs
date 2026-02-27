using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Logging;
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
	/// <param name="displayTitle">Display title used for metadata orchestration.</param>
	/// <param name="normalizedTitleKey">Normalized title key used for cooldown tracking.</param>
	private void TryUpdateMangaEquivalents(
		ComickComicResponse matchedComic,
		string preferredLanguage,
		string displayTitle,
		string normalizedTitleKey)
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
			MangaEquivalenceCatalogUpdateResult updateResult = _mangaEquivalenceCatalog.Update(
				new MangaEquivalentsUpdateRequest(
					_mangaEquivalentsYamlPath,
					mainTitle,
					alternateTitles,
					preferredLanguage));
			_logger.Log(
				IsEquivalentsUpdateDebugLevelOutcome(updateResult.Outcome) ? LogLevel.Debug : LogLevel.Warning,
				EquivalentsUpdateEvent,
				"Manga-equivalents update completed for matched Comick metadata.",
				BuildContext(
					("title", displayTitle),
					("normalized_title_key", normalizedTitleKey),
					("main_title", mainTitle),
					("preferred_language", preferredLanguage),
					("alternate_title_count", alternateTitles.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
					("catalog_outcome", updateResult.Outcome.ToString()),
					("updater_outcome", updateResult.UpdateResult.Outcome.ToString()),
					("affected_group_index", updateResult.UpdateResult.AffectedGroupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture)),
					("added_alias_count", updateResult.UpdateResult.AddedAliasCount.ToString(System.Globalization.CultureInfo.InvariantCulture)),
					("diagnostic", updateResult.Diagnostic ?? updateResult.UpdateResult.Diagnostic)));
		}
		catch (Exception exception) when (!IsFatalException(exception))
		{
			// Best-effort update: merge-pass metadata coordination should continue even if alias sync fails.
			_logger.Warning(
				EquivalentsUpdateEvent,
				"Manga-equivalents update failed with an exception.",
				BuildContext(
					("title", displayTitle),
					("normalized_title_key", normalizedTitleKey),
					("main_title", mainTitle),
					("preferred_language", preferredLanguage),
					("alternate_title_count", alternateTitles.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
					("catalog_outcome", MangaEquivalenceCatalogUpdateOutcome.UpdateFailed.ToString()),
					("updater_outcome", MangaEquivalentsUpdateOutcome.UnhandledException.ToString()),
					("diagnostic", ResolutionExceptionDiagnosticFormatter.Format(exception))));
		}
	}

	/// <summary>
	/// Determines whether one manga-equivalents catalog outcome should emit debug-level telemetry.
	/// </summary>
	/// <param name="outcome">Catalog update outcome.</param>
	/// <returns><see langword="true"/> for success/no-change outcomes; otherwise <see langword="false"/>.</returns>
	private static bool IsEquivalentsUpdateDebugLevelOutcome(MangaEquivalenceCatalogUpdateOutcome outcome)
	{
		return outcome == MangaEquivalenceCatalogUpdateOutcome.Applied ||
			outcome == MangaEquivalenceCatalogUpdateOutcome.NoChanges;
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

		List<string> expectedTitles = [];
		HashSet<string> seenNormalizedKeys = new(StringComparer.Ordinal);
		TryAddExpectedTitle(expectedTitles, seenNormalizedKeys, displayTitle);
		if (_mangaEquivalenceCatalog is null)
		{
			return expectedTitles;
		}

		string? resolvedCanonicalTitle = null;
		if (_mangaEquivalenceCatalog.TryResolveCanonicalTitle(displayTitle, out string canonicalTitle) &&
			TryAddExpectedTitle(expectedTitles, seenNormalizedKeys, canonicalTitle))
		{
			resolvedCanonicalTitle = canonicalTitle;
		}

		if (_mangaEquivalenceCatalog.TryGetEquivalentTitles(displayTitle, out IReadOnlyList<string> equivalentTitles) ||
			(resolvedCanonicalTitle is not null &&
				_mangaEquivalenceCatalog.TryGetEquivalentTitles(resolvedCanonicalTitle, out equivalentTitles)))
		{
			for (int index = 0; index < equivalentTitles.Count; index++)
			{
				TryAddExpectedTitle(expectedTitles, seenNormalizedKeys, equivalentTitles[index]);
			}
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

	/// <summary>
	/// Logs one cover skip event.
	/// </summary>
	/// <param name="displayTitle">Display title.</param>
	/// <param name="normalizedTitleKey">Normalized title key.</param>
	/// <param name="reason">Skip reason.</param>
	private void LogCoverSkipped(string displayTitle, string normalizedTitleKey, string reason)
	{
		_logger.Debug(
			CoverSkippedEvent,
			"Skipped cover artifact write.",
			BuildContext(
				("title", displayTitle),
				("normalized_title_key", normalizedTitleKey),
				("reason", reason)));
	}

	/// <summary>
	/// Logs one details skip event.
	/// </summary>
	/// <param name="displayTitle">Display title.</param>
	/// <param name="normalizedTitleKey">Normalized title key.</param>
	/// <param name="reason">Skip reason.</param>
	private void LogDetailsSkipped(string displayTitle, string normalizedTitleKey, string reason)
	{
		_logger.Debug(
			DetailsSkippedEvent,
			"Skipped details artifact write.",
			BuildContext(
				("title", displayTitle),
				("normalized_title_key", normalizedTitleKey),
				("reason", reason)));
	}

	/// <summary>
	/// Logs one cover outcome.
	/// </summary>
	/// <param name="displayTitle">Display title.</param>
	/// <param name="normalizedTitleKey">Normalized title key.</param>
	/// <param name="result">Cover ensure result.</param>
	private void LogCoverOutcome(string displayTitle, string normalizedTitleKey, OverrideCoverResult result)
	{
		ArgumentNullException.ThrowIfNull(result);

		switch (result.Outcome)
		{
			case OverrideCoverOutcome.AlreadyExists:
				_logger.Debug(
					CoverSkippedEvent,
					"Cover artifact already exists.",
					BuildContext(
						("title", displayTitle),
						("normalized_title_key", normalizedTitleKey),
						("outcome", result.Outcome.ToString()),
						("cover_path", result.CoverJpgPath),
						("existing_cover_path", result.ExistingCoverPath)));
				return;
			case OverrideCoverOutcome.WrittenDownloadedJpeg:
			case OverrideCoverOutcome.WrittenConvertedJpeg:
				_logger.Debug(
					CoverWrittenEvent,
					"Cover artifact written.",
					BuildContext(
						("title", displayTitle),
						("normalized_title_key", normalizedTitleKey),
						("outcome", result.Outcome.ToString()),
						("cover_path", result.CoverJpgPath),
						("cover_uri", result.CoverUri?.AbsoluteUri)));
				return;
			default:
				_logger.Warning(
					CoverFailedEvent,
					"Cover artifact write failed.",
					BuildContext(
						("title", displayTitle),
						("normalized_title_key", normalizedTitleKey),
						("outcome", result.Outcome.ToString()),
						("cover_path", result.CoverJpgPath),
						("cover_uri", result.CoverUri?.AbsoluteUri),
						("diagnostic", result.Diagnostic)));
				return;
		}
	}

	/// <summary>
	/// Logs one details outcome.
	/// </summary>
	/// <param name="displayTitle">Display title.</param>
	/// <param name="normalizedTitleKey">Normalized title key.</param>
	/// <param name="result">Details ensure result.</param>
	private void LogDetailsOutcome(string displayTitle, string normalizedTitleKey, OverrideDetailsResult result)
	{
		ArgumentNullException.ThrowIfNull(result);

		switch (result.Outcome)
		{
			case OverrideDetailsOutcome.AlreadyExists:
				_logger.Debug(
					DetailsSkippedEvent,
					"Details artifact already exists.",
					BuildContext(
						("title", displayTitle),
						("normalized_title_key", normalizedTitleKey),
						("outcome", result.Outcome.ToString()),
						("details_path", result.DetailsJsonPath)));
				return;
			case OverrideDetailsOutcome.SeededFromSource:
			case OverrideDetailsOutcome.GeneratedFromComick:
			case OverrideDetailsOutcome.GeneratedFromComicInfo:
				_logger.Debug(
					DetailsWrittenEvent,
					"Details artifact written.",
					BuildContext(
						("title", displayTitle),
						("normalized_title_key", normalizedTitleKey),
						("outcome", result.Outcome.ToString()),
						("details_path", result.DetailsJsonPath),
						("source_details_path", result.SourceDetailsJsonPath),
						("comic_info_xml_path", result.ComicInfoXmlPath)));
				return;
			default:
				_logger.Warning(
					DetailsFailedEvent,
					"Details artifact write failed.",
					BuildContext(
						("title", displayTitle),
						("normalized_title_key", normalizedTitleKey),
						("outcome", result.Outcome.ToString()),
						("details_path", result.DetailsJsonPath),
						("source_details_path", result.SourceDetailsJsonPath),
						("comic_info_xml_path", result.ComicInfoXmlPath)));
				return;
		}
	}

	/// <summary>
	/// Builds one structured logging context dictionary from non-empty values.
	/// </summary>
	/// <param name="pairs">Key/value pairs.</param>
	/// <returns>Structured context dictionary.</returns>
	private static IReadOnlyDictionary<string, string> BuildContext(params (string Key, string? Value)[] pairs)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		for (int index = 0; index < pairs.Length; index++)
		{
			(string key, string? value) = pairs[index];
			if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			context[key] = value;
		}

		return context;
	}
}
