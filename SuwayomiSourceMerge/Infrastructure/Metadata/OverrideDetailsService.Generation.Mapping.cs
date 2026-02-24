using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Mapping helpers for <see cref="OverrideDetailsService"/>.
/// </summary>
internal sealed partial class OverrideDetailsService
{
	/// <summary>
	/// Builds one details model from ComicInfo metadata only.
	/// </summary>
	/// <param name="displayTitle">Display title.</param>
	/// <param name="descriptionMode">Description mode.</param>
	/// <param name="metadata">ComicInfo metadata.</param>
	/// <returns>Mapped details model.</returns>
	private static DetailsJsonDocumentModel BuildComicInfoDetailsJsonModel(
		string displayTitle,
		string descriptionMode,
		ComicInfoMetadata metadata)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);
		ArgumentException.ThrowIfNullOrWhiteSpace(descriptionMode);
		ArgumentNullException.ThrowIfNull(metadata);

		return new DetailsJsonDocumentModel(
			displayTitle.Trim(),
			metadata.Writer.Trim(),
			metadata.Penciller.Trim(),
			NormalizeDescription(metadata.Summary, descriptionMode),
			SplitGenre(metadata.Genre),
			MapStatusCode(metadata.Status));
	}

	/// <summary>
	/// Builds one API-first details model using Comick values with ComicInfo per-field fallback.
	/// </summary>
	/// <param name="displayTitle">Display title.</param>
	/// <param name="descriptionMode">Description mode.</param>
	/// <param name="comickComic">Matched Comick payload.</param>
	/// <param name="sourceDirectoryPaths">Ordered source paths used for lazy ComicInfo fallback discovery.</param>
	/// <param name="fallbackComicInfoPath">ComicInfo path when fallback values were used.</param>
	/// <returns>Mapped details model.</returns>
	private DetailsJsonDocumentModel BuildComickPreferredDetailsJsonModel(
		string displayTitle,
		string descriptionMode,
		ComickComicResponse comickComic,
		IReadOnlyList<string> sourceDirectoryPaths,
		out string? fallbackComicInfoPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);
		ArgumentException.ThrowIfNullOrWhiteSpace(descriptionMode);
		ArgumentNullException.ThrowIfNull(comickComic);
		ArgumentNullException.ThrowIfNull(sourceDirectoryPaths);

		LazyComicInfoFallbackResolver fallbackResolver = new(this, sourceDirectoryPaths);
		string? resolvedFallbackComicInfoPath = null;
		bool usedFallback = false;

		string author = JoinCreatorNames(comickComic.Authors);
		if (author.Length == 0)
		{
			ComicInfoFallbackResolution fallback = fallbackResolver.Resolve();
			if (!string.IsNullOrWhiteSpace(fallback.Metadata?.Writer))
			{
				author = fallback.Metadata.Writer.Trim();
				usedFallback = true;
				resolvedFallbackComicInfoPath = fallback.ComicInfoXmlPath;
			}
		}

		string artist = JoinCreatorNames(comickComic.Artists);
		if (artist.Length == 0)
		{
			ComicInfoFallbackResolution fallback = fallbackResolver.Resolve();
			if (!string.IsNullOrWhiteSpace(fallback.Metadata?.Penciller))
			{
				artist = fallback.Metadata.Penciller.Trim();
				usedFallback = true;
				resolvedFallbackComicInfoPath = fallback.ComicInfoXmlPath;
			}
		}

		string descriptionSource = ResolveComickDescriptionSourceFromComick(comickComic);
		if (descriptionSource.Length == 0)
		{
			ComicInfoFallbackResolution fallback = fallbackResolver.Resolve();
			if (!string.IsNullOrWhiteSpace(fallback.Metadata?.Summary))
			{
				descriptionSource = fallback.Metadata.Summary.Trim();
				usedFallback = true;
				resolvedFallbackComicInfoPath = fallback.ComicInfoXmlPath;
			}
		}

		descriptionSource = AppendLanguageTitleBlock(descriptionSource, comickComic);
		string description = NormalizeDescription(descriptionSource, descriptionMode);

		IReadOnlyList<string> genres = BuildComickGenres(comickComic.Comic);
		if (genres.Count == 0)
		{
			ComicInfoFallbackResolution fallback = fallbackResolver.Resolve();
			if (fallback.Metadata is not null)
			{
				IReadOnlyList<string> fallbackGenres = SplitGenre(fallback.Metadata.Genre);
				if (fallbackGenres.Count > 0)
				{
					genres = fallbackGenres;
					usedFallback = true;
					resolvedFallbackComicInfoPath = fallback.ComicInfoXmlPath;
				}
			}
		}

		string status;
		if (comickComic.Comic?.Status is int comickStatus)
		{
			status = MapComickStatusCode(comickStatus);
		}
		else
		{
			ComicInfoFallbackResolution fallback = fallbackResolver.Resolve();
			if (fallback.Metadata is not null)
			{
				status = MapStatusCode(fallback.Metadata.Status);
				if (!string.Equals(status, "0", StringComparison.Ordinal))
				{
					usedFallback = true;
					resolvedFallbackComicInfoPath = fallback.ComicInfoXmlPath;
				}
			}
			else
			{
				status = "0";
			}
		}

		fallbackComicInfoPath = usedFallback
			? resolvedFallbackComicInfoPath
			: null;

		return new DetailsJsonDocumentModel(
			displayTitle.Trim(),
			author,
			artist,
			description,
			genres,
			status);
	}

	/// <summary>
	/// Builds ordered genres from Comick genre mappings and MangaUpdates category votes.
	/// </summary>
	/// <param name="comicDetails">Comick comic details.</param>
	/// <returns>Ordered distinct genre list.</returns>
	private static IReadOnlyList<string> BuildComickGenres(ComickComicDetails? comicDetails)
	{
		List<string> genres = [];
		HashSet<string> seenGenres = new(StringComparer.Ordinal);

		if (comicDetails?.GenreMappings is not null)
		{
			for (int index = 0; index < comicDetails.GenreMappings.Count; index++)
			{
				ComickComicGenreMapping? genreMapping = comicDetails.GenreMappings[index];
				if (genreMapping is null)
				{
					continue;
				}

				string? genreName = genreMapping.Genre?.Name;
				if (string.IsNullOrWhiteSpace(genreName))
				{
					continue;
				}

				string normalizedGenreName = genreName.Trim();
				if (seenGenres.Add(normalizedGenreName))
				{
					genres.Add(normalizedGenreName);
				}
			}
		}

		if (comicDetails?.MuComics?.MuComicCategories is null)
		{
			return genres;
		}

		for (int index = 0; index < comicDetails.MuComics.MuComicCategories.Count; index++)
		{
			ComickMuComicCategoryVote? voteEntry = comicDetails.MuComics.MuComicCategories[index];
			if (voteEntry is null)
			{
				continue;
			}

			if (!voteEntry.PositiveVote.HasValue || !voteEntry.NegativeVote.HasValue)
			{
				continue;
			}

			if (voteEntry.PositiveVote.Value <= voteEntry.NegativeVote.Value)
			{
				continue;
			}

			string? genreTitle = voteEntry.Category?.Title;
			if (string.IsNullOrWhiteSpace(genreTitle))
			{
				continue;
			}

			string normalizedGenreTitle = genreTitle.Trim();
			if (seenGenres.Add(normalizedGenreTitle))
			{
				genres.Add(normalizedGenreTitle);
			}
		}

		return genres;
	}

	/// <summary>
	/// Joins creator names into a deterministic comma-separated value.
	/// </summary>
	/// <param name="creators">Creator list.</param>
	/// <returns>Joined creator string, or empty when no valid names exist.</returns>
	private static string JoinCreatorNames(IReadOnlyList<ComickCreator>? creators)
	{
		if (creators is null || creators.Count == 0)
		{
			return string.Empty;
		}

		HashSet<string> seen = new(StringComparer.Ordinal);
		List<string> names = [];
		for (int index = 0; index < creators.Count; index++)
		{
			ComickCreator? creator = creators[index];
			if (creator is null || string.IsNullOrWhiteSpace(creator.Name))
			{
				continue;
			}

			string trimmedName = creator.Name.Trim();
			if (seen.Add(trimmedName))
			{
				names.Add(trimmedName);
			}
		}

		return names.Count == 0
			? string.Empty
			: string.Join(", ", names);
	}

	/// <summary>
	/// Splits and trims ComicInfo genre text into array items using comma and semicolon delimiters.
	/// </summary>
	/// <param name="genre">Genre source text.</param>
	/// <returns>Ordered non-empty genre values.</returns>
	private static IReadOnlyList<string> SplitGenre(string genre)
	{
		ArgumentNullException.ThrowIfNull(genre);

		string normalizedGenre = genre.Replace(';', ',');
		string[] rawParts = normalizedGenre.Split(',', StringSplitOptions.None);

		List<string> genres = [];
		for (int index = 0; index < rawParts.Length; index++)
		{
			string trimmed = rawParts[index].Trim();
			if (trimmed.Length == 0)
			{
				continue;
			}

			genres.Add(trimmed);
		}

		return genres;
	}

	/// <summary>
	/// Maps status text to shell-parity numeric status code strings.
	/// </summary>
	/// <param name="status">Status text.</param>
	/// <returns>Shell-parity status code string.</returns>
	private static string MapStatusCode(string status)
	{
		ArgumentNullException.ThrowIfNull(status);

		string normalized = status.Trim().ToLowerInvariant();
		if (normalized.Length == 0)
		{
			return "0";
		}

		if (normalized == "ongoing"
			|| normalized.Contains("ongoing", StringComparison.Ordinal)
			|| normalized.Contains("publishing", StringComparison.Ordinal)
			|| normalized.Contains("serialization", StringComparison.Ordinal))
		{
			return "1";
		}

		if (normalized == "completed"
			|| normalized.Contains("completed", StringComparison.Ordinal)
			|| normalized == "complete"
			|| normalized.Contains("finished", StringComparison.Ordinal)
			|| normalized.Contains("ended", StringComparison.Ordinal))
		{
			return "2";
		}

		return normalized == "licensed" || normalized.Contains("licensed", StringComparison.Ordinal)
			? "3"
			: "0";
	}

	/// <summary>
	/// Maps Comick integer status values to details.json status codes.
	/// </summary>
	/// <param name="status">Comick integer status code.</param>
	/// <returns>details.json status code string.</returns>
	private static string MapComickStatusCode(int status)
	{
		return status switch
		{
			1 => "1",
			2 => "2",
			3 => "3",
			_ => "0"
		};
	}

}
