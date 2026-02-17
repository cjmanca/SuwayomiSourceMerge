namespace SuwayomiSourceMerge.Infrastructure.Watching;

internal sealed partial class PersistentInotifywaitEventReader
{
	/// <summary>
	/// Normalizes and de-duplicates watch roots.
	/// </summary>
	/// <param name="watchRoots">Raw watch roots.</param>
	/// <param name="warnings">Warning sink for invalid watch-root inputs.</param>
	/// <returns>Normalized watch-root paths.</returns>
	private static string[] NormalizeWatchRoots(IReadOnlyList<string> watchRoots, ICollection<string> warnings)
	{
		HashSet<string> roots = new(_pathComparer);
		for (int index = 0; index < watchRoots.Count; index++)
		{
			string? root = watchRoots[index];
			if (string.IsNullOrWhiteSpace(root))
			{
				continue;
			}

			try
			{
				roots.Add(NormalizePath(root));
			}
			catch (Exception exception)
			{
				warnings.Add($"Ignoring invalid watch root '{root}': {exception.GetType().Name}.");
			}
		}

		return roots.OrderBy(static path => path, _pathComparer).ToArray();
	}

	/// <summary>
	/// Normalizes one path for equality and prefix comparisons.
	/// </summary>
	/// <param name="path">Input path.</param>
	/// <returns>Normalized absolute path.</returns>
	private static string NormalizePath(string path)
	{
		string normalized = Path.GetFullPath(Path.TrimEndingDirectorySeparator(path));
		return normalized.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
	}

	/// <summary>
	/// Returns whether one path is under one root and outputs root-relative path text.
	/// </summary>
	/// <param name="rootPath">Root path.</param>
	/// <param name="candidatePath">Candidate path.</param>
	/// <param name="relativePath">Relative path when containment is true.</param>
	/// <returns><see langword="true"/> when candidate is equal to or under root.</returns>
	private static bool TryGetRelativePath(string rootPath, string candidatePath, out string relativePath)
	{
		relativePath = string.Empty;
		string normalizedRoot = NormalizePath(rootPath);
		string normalizedCandidate = NormalizePath(candidatePath);
		if (string.Equals(normalizedRoot, normalizedCandidate, _pathComparison))
		{
			return true;
		}

		string prefix = normalizedRoot + Path.DirectorySeparatorChar;
		if (!normalizedCandidate.StartsWith(prefix, _pathComparison))
		{
			return false;
		}

		relativePath = normalizedCandidate[prefix.Length..];
		return true;
	}

	/// <summary>
	/// Splits one relative path into normalized segments.
	/// </summary>
	/// <param name="relativePath">Relative path text.</param>
	/// <returns>Segment array.</returns>
	private static string[] SplitPathSegments(string relativePath)
	{
		if (string.IsNullOrWhiteSpace(relativePath))
		{
			return [];
		}

		return relativePath.Split(
			[Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
			StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}
}
