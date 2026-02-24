namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Candidate discovery helpers for <see cref="OverrideDetailsService"/>.
/// </summary>
internal sealed partial class OverrideDetailsService
{
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
}
