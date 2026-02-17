using System.Text.Json;
using System.Text.RegularExpressions;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Ensures details.json metadata exists using override-exists checks, source seeding, and ComicInfo.xml generation.
/// </summary>
internal sealed class OverrideDetailsService : IOverrideDetailsService
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
	/// Placeholder status display values stored for shell-parity details.json output.
	/// </summary>
	private static readonly IReadOnlyList<string> _statusValueDescriptions =
	[
		"0 = Unknown",
		"1 = Ongoing",
		"2 = Completed",
		"3 = Licensed"
	];

	/// <summary>
	/// Regex used to normalize HTML line-break tags to newlines.
	/// </summary>
	private static readonly Regex _lineBreakTagRegex = new(
		@"<\s*br\s*/?\s*>",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

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
			return new OverrideDetailsResult(
				OverrideDetailsOutcome.AlreadyExists,
				existingDetailsPath!,
				detailsJsonExists: true,
				null,
				null);
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
				null);
		}

		if (seededDestinationExists)
		{
			return new OverrideDetailsResult(
				OverrideDetailsOutcome.AlreadyExists,
				preferredDetailsPath,
				detailsJsonExists: true,
				null,
				null);
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
					return new OverrideDetailsResult(
						OverrideDetailsOutcome.AlreadyExists,
						preferredDetailsPath,
						detailsJsonExists: true,
						null,
						null);
				}

				continue;
			}

			return new OverrideDetailsResult(
				OverrideDetailsOutcome.GeneratedFromComicInfo,
				preferredDetailsPath,
				detailsJsonExists: true,
				null,
				candidatePath);
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
					return new OverrideDetailsResult(
						OverrideDetailsOutcome.AlreadyExists,
						preferredDetailsPath,
						detailsJsonExists: true,
						null,
						null);
				}

				continue;
			}

			return new OverrideDetailsResult(
				OverrideDetailsOutcome.GeneratedFromComicInfo,
				preferredDetailsPath,
				detailsJsonExists: true,
				null,
				candidatePath);
		}

		bool hasComicInfoCandidates = fastPathCandidatesBySource.Count > 0 || slowPathCandidates.Count > 0;
		return new OverrideDetailsResult(
			hasComicInfoCandidates
				? OverrideDetailsOutcome.SkippedParseFailure
				: OverrideDetailsOutcome.SkippedNoComicInfo,
			preferredDetailsPath,
			detailsJsonExists: false,
			null,
			null);
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

	/// <summary>
	/// Discovers one fast-path ComicInfo.xml candidate per source directory at typical depth.
	/// </summary>
	/// <param name="sourceDirectoryPaths">Ordered source directories.</param>
	/// <returns>Map from source directory to first candidate path.</returns>
	private static Dictionary<string, string> DiscoverFastPathCandidates(IReadOnlyList<string> sourceDirectoryPaths)
	{
		ArgumentNullException.ThrowIfNull(sourceDirectoryPaths);

		Dictionary<string, string> candidatesBySource = new(StringComparer.Ordinal);
		foreach (string sourceDirectoryPath in sourceDirectoryPaths)
		{
			if (!Directory.Exists(sourceDirectoryPath))
			{
				continue;
			}

			string? candidatePath = FindLexicographicallySmallestCandidate(
				sourceDirectoryPath,
				MinCandidateDepth,
				FastCandidateDepth);
			if (candidatePath is null)
			{
				continue;
			}

			candidatesBySource[sourceDirectoryPath] = candidatePath;
		}

		return candidatesBySource;
	}

	/// <summary>
	/// Discovers slow-path ComicInfo.xml candidates after fast-path attempts fail.
	/// </summary>
	/// <param name="sourceDirectoryPaths">Ordered source directories.</param>
	/// <param name="fastPathCandidatesBySource">Fast-path candidate map by source.</param>
	/// <returns>Ordered slow-path candidate list.</returns>
	private static List<string> DiscoverSlowPathCandidates(
		IReadOnlyList<string> sourceDirectoryPaths,
		IReadOnlyDictionary<string, string> fastPathCandidatesBySource)
	{
		ArgumentNullException.ThrowIfNull(sourceDirectoryPaths);
		ArgumentNullException.ThrowIfNull(fastPathCandidatesBySource);

		List<string> candidates = [];

		foreach (string sourceDirectoryPath in sourceDirectoryPaths)
		{
			if (!Directory.Exists(sourceDirectoryPath))
			{
				continue;
			}

			fastPathCandidatesBySource.TryGetValue(sourceDirectoryPath, out string? fastPathCandidate);

			HashSet<string> perSourceCandidates = new(StringComparer.Ordinal);
			List<string> depthTwoCandidates = EnumerateComicInfoFilesInDepthRange(
					sourceDirectoryPath,
					MinCandidateDepth,
					FastCandidateDepth)
				.Where(
					path =>
						!string.Equals(path, fastPathCandidate, StringComparison.Ordinal)
						&& perSourceCandidates.Add(path))
				.Take(MaxSlowCandidatesPerSource)
				.ToList();

			if (depthTwoCandidates.Count > 0)
			{
				candidates.AddRange(depthTwoCandidates);
				continue;
			}

			List<string> depthSixCandidates = EnumerateComicInfoFilesInDepthRange(
					sourceDirectoryPath,
					MinCandidateDepth,
					SlowCandidateMaxDepth)
				.Where(
					path =>
						!string.Equals(path, fastPathCandidate, StringComparison.Ordinal)
						&& perSourceCandidates.Add(path))
				.Take(MaxSlowCandidatesPerSource)
				.ToList();

			candidates.AddRange(depthSixCandidates);
		}

		return candidates;
	}

	/// <summary>
	/// Finds the lexicographically smallest ComicInfo.xml candidate within a depth range.
	/// </summary>
	/// <param name="sourceDirectoryPath">Source directory path.</param>
	/// <param name="minimumDepth">Minimum relative depth to include.</param>
	/// <param name="maximumDepth">Maximum relative depth to include.</param>
	/// <returns>Smallest candidate path when found; otherwise <see langword="null"/>.</returns>
	private static string? FindLexicographicallySmallestCandidate(
		string sourceDirectoryPath,
		int minimumDepth,
		int maximumDepth)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryPath);

		string? smallest = null;
		foreach (string candidatePath in EnumerateComicInfoFilesInDepthRange(
			sourceDirectoryPath,
			minimumDepth,
			maximumDepth))
		{
			if (smallest is null || StringComparer.Ordinal.Compare(candidatePath, smallest) < 0)
			{
				smallest = candidatePath;
			}
		}

		return smallest;
	}

	/// <summary>
	/// Enumerates ComicInfo.xml files under one source directory for a bounded depth range.
	/// </summary>
	/// <param name="sourceDirectoryPath">Source directory path.</param>
	/// <param name="minimumDepth">Minimum relative depth to include.</param>
	/// <param name="maximumDepth">Maximum relative depth to include.</param>
	/// <returns>Deterministically ordered ComicInfo.xml candidate file paths.</returns>
	private static IEnumerable<string> EnumerateComicInfoFilesInDepthRange(
		string sourceDirectoryPath,
		int minimumDepth,
		int maximumDepth)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectoryPath);
		if (minimumDepth > maximumDepth)
		{
			yield break;
		}

		string normalizedRootPath = Path.GetFullPath(sourceDirectoryPath);
		Stack<(string DirectoryPath, int Depth)> pendingDirectories = [];
		pendingDirectories.Push((normalizedRootPath, 0));

		while (pendingDirectories.Count > 0)
		{
			(string currentDirectoryPath, int currentDepth) = pendingDirectories.Pop();

			int fileDepth = currentDepth + 1;
			if (fileDepth >= minimumDepth && fileDepth <= maximumDepth)
			{
				string candidatePath = Path.Combine(currentDirectoryPath, ComicInfoFileName);
				if (File.Exists(candidatePath))
				{
					yield return candidatePath;
				}
			}

			int nextDirectoryDepth = currentDepth + 1;
			if (nextDirectoryDepth > maximumDepth)
			{
				continue;
			}

			string[] childDirectories = GetOrderedChildDirectoriesSafe(currentDirectoryPath);

			for (int index = childDirectories.Length - 1; index >= 0; index--)
			{
				pendingDirectories.Push((childDirectories[index], nextDirectoryDepth));
			}
		}
	}

	/// <summary>
	/// Enumerates child directories in deterministic path order while tolerating transient filesystem races.
	/// </summary>
	/// <param name="directoryPath">Parent directory path.</param>
	/// <returns>Ordered child directories, or an empty array when enumeration fails.</returns>
	private static string[] GetOrderedChildDirectoriesSafe(string directoryPath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

		try
		{
			return Directory
				.EnumerateDirectories(directoryPath)
				.OrderBy(path => path, StringComparer.Ordinal)
				.ToArray();
		}
		catch (UnauthorizedAccessException)
		{
			return [];
		}
		catch (DirectoryNotFoundException)
		{
			return [];
		}
		catch (IOException)
		{
			return [];
		}
	}

	/// <summary>
	/// Attempts to parse one ComicInfo.xml file and write shell-parity details.json content.
	/// </summary>
	/// <param name="comicInfoXmlPath">ComicInfo.xml path to parse.</param>
	/// <param name="request">Ensure request.</param>
	/// <param name="detailsJsonPath">details.json output path.</param>
	/// <param name="destinationAlreadyExists">Whether destination details.json already exists after a handled race condition.</param>
	/// <returns><see langword="true"/> when parsing and writing succeed; otherwise <see langword="false"/>.</returns>
	private bool TryGenerateFromComicInfo(
		string comicInfoXmlPath,
		OverrideDetailsRequest request,
		string detailsJsonPath,
		out bool destinationAlreadyExists)
	{
		if (!_comicInfoMetadataParser.TryParse(comicInfoXmlPath, out ComicInfoMetadata? metadata) || metadata is null)
		{
			destinationAlreadyExists = false;
			return false;
		}

		try
		{
			WriteDetailsJson(detailsJsonPath, request.DisplayTitle, request.DetailsDescriptionMode, metadata);
			destinationAlreadyExists = false;
			return true;
		}
		catch (IOException)
		{
			destinationAlreadyExists = File.Exists(detailsJsonPath);
			return false;
		}
		catch (UnauthorizedAccessException)
		{
			destinationAlreadyExists = File.Exists(detailsJsonPath);
			return false;
		}
	}

	/// <summary>
	/// Writes shell-parity details.json content to the target path using a temporary file and atomic move.
	/// </summary>
	/// <param name="detailsJsonPath">Target details.json path.</param>
	/// <param name="displayTitle">Canonical display title.</param>
	/// <param name="descriptionMode">Description rendering mode.</param>
	/// <param name="metadata">Parsed ComicInfo metadata.</param>
	private static void WriteDetailsJson(
		string detailsJsonPath,
		string displayTitle,
		string descriptionMode,
		ComicInfoMetadata metadata)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(detailsJsonPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(displayTitle);
		ArgumentException.ThrowIfNullOrWhiteSpace(descriptionMode);
		ArgumentNullException.ThrowIfNull(metadata);

		string destinationDirectory = Path.GetDirectoryName(detailsJsonPath)
			?? throw new InvalidOperationException("Details.json destination directory could not be determined.");
		Directory.CreateDirectory(destinationDirectory);

		string temporaryPath = $"{detailsJsonPath}.{Guid.NewGuid():N}.tmp";
		try
		{
			using (FileStream stream = File.Create(temporaryPath))
			{
				using Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true });
				writer.WriteStartObject();
				writer.WriteString("title", displayTitle.Trim());
				writer.WriteString("author", metadata.Writer.Trim());
				writer.WriteString("artist", metadata.Penciller.Trim());
				writer.WriteString("description", NormalizeDescription(metadata.Summary, descriptionMode));

				writer.WriteStartArray("genre");
				foreach (string genre in SplitGenre(metadata.Genre))
				{
					writer.WriteStringValue(genre);
				}

				writer.WriteEndArray();
				writer.WriteString("status", MapStatusCode(metadata.Status));

				writer.WriteStartArray("_status values");
				foreach (string statusDisplayValue in _statusValueDescriptions)
				{
					writer.WriteStringValue(statusDisplayValue);
				}

				writer.WriteEndArray();
				writer.WriteEndObject();
				writer.Flush();
			}

			File.Move(temporaryPath, detailsJsonPath, overwrite: false);
		}
		finally
		{
			if (File.Exists(temporaryPath))
			{
				File.Delete(temporaryPath);
			}
		}
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
		foreach (string rawPart in rawParts)
		{
			string trimmed = rawPart.Trim();
			if (trimmed.Length == 0)
			{
				continue;
			}

			genres.Add(trimmed);
		}

		return genres;
	}

	/// <summary>
	/// Normalizes description text by converting HTML line breaks and applying configured rendering mode.
	/// </summary>
	/// <param name="summary">Summary value from metadata.</param>
	/// <param name="descriptionMode">Normalized description mode.</param>
	/// <returns>Normalized description string for details.json.</returns>
	private static string NormalizeDescription(string summary, string descriptionMode)
	{
		ArgumentNullException.ThrowIfNull(summary);
		ArgumentException.ThrowIfNullOrWhiteSpace(descriptionMode);

		string normalized = summary
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace("\r", "\n", StringComparison.Ordinal);
		normalized = _lineBreakTagRegex.Replace(normalized, "\n");

		return descriptionMode is "br" or "html"
			? normalized.Replace("\n", "<br />\n", StringComparison.Ordinal)
			: normalized;
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
}
