namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Ensures details.json metadata exists using override-exists checks, source seeding, API-first generation, and ComicInfo.xml generation.
/// </summary>
internal sealed partial class OverrideDetailsService : IOverrideDetailsService
{
	/// <summary>
	/// details.json file name.
	/// </summary>
	private const string DetailsJsonFileName = "details.json";

	/// <summary>
	/// ComicInfo.xml file name.
	/// </summary>
	private const string ComicInfoFileName = "ComicInfo.xml";

	/// <summary>
	/// Minimum depth used for ComicInfo.xml discovery.
	/// </summary>
	private const int MinCandidateDepth = 2;

	/// <summary>
	/// Typical chapter-layout depth used for fast candidate discovery.
	/// </summary>
	private const int FastCandidateDepth = 2;

	/// <summary>
	/// Maximum fallback depth used for tolerant candidate discovery.
	/// </summary>
	private const int SlowCandidateMaxDepth = 6;

	/// <summary>
	/// Maximum number of slow-path candidates collected per source directory.
	/// </summary>
	private const int MaxSlowCandidatesPerSource = 30;

	/// <summary>
	/// ComicInfo parser dependency.
	/// </summary>
	private readonly IComicInfoMetadataParser _comicInfoMetadataParser;

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideDetailsService"/> class.
	/// </summary>
	public OverrideDetailsService()
		: this(new ComicInfoMetadataParser())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideDetailsService"/> class.
	/// </summary>
	/// <param name="comicInfoMetadataParser">ComicInfo parser dependency.</param>
	internal OverrideDetailsService(IComicInfoMetadataParser comicInfoMetadataParser)
	{
		_comicInfoMetadataParser = comicInfoMetadataParser ?? throw new ArgumentNullException(nameof(comicInfoMetadataParser));
	}

	/// <inheritdoc />
	public OverrideDetailsResult EnsureDetailsJson(OverrideDetailsRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		string preferredDetailsPath = Path.Combine(
			request.PreferredOverrideDirectoryPath,
			DetailsJsonFileName);

		if (TryFindExistingOverrideDetailsPath(request, out string? existingDetailsPath))
		{
			return CreateAlreadyExistsResult(existingDetailsPath!);
		}

		Directory.CreateDirectory(request.PreferredOverrideDirectoryPath);

		if (TrySeedFromSource(
			request,
			preferredDetailsPath,
			out string? sourceDetailsJsonPath,
			out bool seededDestinationExists))
		{
			return new OverrideDetailsResult(
				OverrideDetailsOutcome.SeededFromSource,
				preferredDetailsPath,
				detailsJsonExists: true,
				sourceDetailsJsonPath,
				comicInfoXmlPath: null);
		}

		if (seededDestinationExists)
		{
			return CreateAlreadyExistsResult(preferredDetailsPath);
		}

		if (request.MatchedComickComic is not null)
		{
			if (TryGenerateFromComick(
				request,
				preferredDetailsPath,
				out string? fallbackComicInfoPath,
				out bool comickDestinationExists))
			{
				return new OverrideDetailsResult(
					OverrideDetailsOutcome.GeneratedFromComick,
					preferredDetailsPath,
					detailsJsonExists: true,
					sourceDetailsJsonPath: null,
					comicInfoXmlPath: fallbackComicInfoPath);
			}

			if (comickDestinationExists)
			{
				return CreateAlreadyExistsResult(preferredDetailsPath);
			}
		}

		Dictionary<string, string> fastPathCandidatesBySource = DiscoverFastPathCandidates(request.OrderedSourceDirectoryPaths);
		HashSet<string> attemptedCandidatePaths = new(StringComparer.Ordinal);

		foreach (string sourceDirectoryPath in request.OrderedSourceDirectoryPaths)
		{
			if (!fastPathCandidatesBySource.TryGetValue(sourceDirectoryPath, out string? candidatePath))
			{
				continue;
			}

			if (!attemptedCandidatePaths.Add(candidatePath))
			{
				continue;
			}

			if (!TryGenerateFromComicInfo(
				candidatePath,
				request,
				preferredDetailsPath,
				out bool generatedDestinationExists))
			{
				if (generatedDestinationExists)
				{
					return CreateAlreadyExistsResult(preferredDetailsPath);
				}

				continue;
			}

			return new OverrideDetailsResult(
				OverrideDetailsOutcome.GeneratedFromComicInfo,
				preferredDetailsPath,
				detailsJsonExists: true,
				sourceDetailsJsonPath: null,
				comicInfoXmlPath: candidatePath);
		}

		List<string> slowPathCandidates = DiscoverSlowPathCandidates(
			request.OrderedSourceDirectoryPaths,
			fastPathCandidatesBySource);
		foreach (string candidatePath in slowPathCandidates)
		{
			if (!attemptedCandidatePaths.Add(candidatePath))
			{
				continue;
			}

			if (!TryGenerateFromComicInfo(
				candidatePath,
				request,
				preferredDetailsPath,
				out bool generatedDestinationExists))
			{
				if (generatedDestinationExists)
				{
					return CreateAlreadyExistsResult(preferredDetailsPath);
				}

				continue;
			}

			return new OverrideDetailsResult(
				OverrideDetailsOutcome.GeneratedFromComicInfo,
				preferredDetailsPath,
				detailsJsonExists: true,
				sourceDetailsJsonPath: null,
				comicInfoXmlPath: candidatePath);
		}

		bool hasComicInfoCandidates = fastPathCandidatesBySource.Count > 0 || slowPathCandidates.Count > 0;
		return new OverrideDetailsResult(
			hasComicInfoCandidates
				? OverrideDetailsOutcome.SkippedParseFailure
				: OverrideDetailsOutcome.SkippedNoComicInfo,
			preferredDetailsPath,
			detailsJsonExists: false,
			sourceDetailsJsonPath: null,
			comicInfoXmlPath: null);
	}

	/// <summary>
	/// Creates an already-exists result for one existing details.json path.
	/// </summary>
	/// <param name="existingDetailsPath">Existing details path.</param>
	/// <returns>Already-exists result.</returns>
	private static OverrideDetailsResult CreateAlreadyExistsResult(string existingDetailsPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(existingDetailsPath);
		return new OverrideDetailsResult(
			OverrideDetailsOutcome.AlreadyExists,
			existingDetailsPath,
			detailsJsonExists: true,
			sourceDetailsJsonPath: null,
			comicInfoXmlPath: null);
	}

	/// <summary>
	/// Attempts to find an existing details.json file in override directories.
	/// </summary>
	/// <param name="request">Ensure request.</param>
	/// <param name="existingDetailsPath">Existing details.json path when found.</param>
	/// <returns><see langword="true"/> when an existing file is found; otherwise <see langword="false"/>.</returns>
	private static bool TryFindExistingOverrideDetailsPath(
		OverrideDetailsRequest request,
		out string? existingDetailsPath)
	{
		ArgumentNullException.ThrowIfNull(request);

		HashSet<string> checkedDirectories = new(StringComparer.Ordinal);
		if (checkedDirectories.Add(request.PreferredOverrideDirectoryPath))
		{
			string preferredDetailsPath = Path.Combine(request.PreferredOverrideDirectoryPath, DetailsJsonFileName);
			if (File.Exists(preferredDetailsPath))
			{
				existingDetailsPath = preferredDetailsPath;
				return true;
			}
		}

		foreach (string overrideDirectoryPath in request.AllOverrideDirectoryPaths)
		{
			if (!checkedDirectories.Add(overrideDirectoryPath))
			{
				continue;
			}

			string detailsPath = Path.Combine(overrideDirectoryPath, DetailsJsonFileName);
			if (File.Exists(detailsPath))
			{
				existingDetailsPath = detailsPath;
				return true;
			}
		}

		existingDetailsPath = null;
		return false;
	}

	/// <summary>
	/// Attempts to seed details.json from the first source directory that already has one.
	/// </summary>
	/// <param name="request">Ensure request.</param>
	/// <param name="targetDetailsPath">Preferred override details path.</param>
	/// <param name="sourceDetailsJsonPath">Seed source path when seeding succeeds.</param>
	/// <param name="destinationAlreadyExists">Whether destination details.json already exists after a handled race condition.</param>
	/// <returns><see langword="true"/> when seeding succeeds; otherwise <see langword="false"/>.</returns>
	private static bool TrySeedFromSource(
		OverrideDetailsRequest request,
		string targetDetailsPath,
		out string? sourceDetailsJsonPath,
		out bool destinationAlreadyExists)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentException.ThrowIfNullOrWhiteSpace(targetDetailsPath);

		foreach (string sourceDirectoryPath in request.OrderedSourceDirectoryPaths)
		{
			string sourceDetailsPath = Path.Combine(sourceDirectoryPath, DetailsJsonFileName);
			if (!File.Exists(sourceDetailsPath))
			{
				continue;
			}

			try
			{
				File.Copy(sourceDetailsPath, targetDetailsPath, overwrite: false);
				sourceDetailsJsonPath = sourceDetailsPath;
				destinationAlreadyExists = false;
				return true;
			}
			catch (IOException)
			{
				if (File.Exists(targetDetailsPath))
				{
					sourceDetailsJsonPath = null;
					destinationAlreadyExists = true;
					return false;
				}
			}
			catch (UnauthorizedAccessException)
			{
				if (File.Exists(targetDetailsPath))
				{
					sourceDetailsJsonPath = null;
					destinationAlreadyExists = true;
					return false;
				}
			}
		}

		sourceDetailsJsonPath = null;
		destinationAlreadyExists = false;
		return false;
	}
}
