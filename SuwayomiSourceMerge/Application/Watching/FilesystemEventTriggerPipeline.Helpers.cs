using System.Globalization;

using SuwayomiSourceMerge.Infrastructure.Watching;

namespace SuwayomiSourceMerge.Application.Watching;

internal sealed partial class FilesystemEventTriggerPipeline
{
	/// <summary>
	/// Builds a normalized source/manga composite key.
	/// </summary>
	/// <param name="sourceName">Source name.</param>
	/// <param name="mangaName">Manga name.</param>
	/// <returns>Composite key string.</returns>
	private static string BuildSourceMangaKey(string sourceName, string mangaName)
	{
		return string.Create(CultureInfo.InvariantCulture, $"{sourceName}/{mangaName}");
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
	/// Normalizes one path for containment and equality checks.
	/// </summary>
	/// <param name="path">Input path.</param>
	/// <returns>Normalized full path.</returns>
	private static string NormalizePath(string path)
	{
		string normalized = Path.GetFullPath(Path.TrimEndingDirectorySeparator(path));
		return normalized.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
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

	/// <summary>
	/// Returns whether one bit mask contains any of the specified flags.
	/// </summary>
	/// <param name="mask">Input mask.</param>
	/// <param name="values">Flags to probe.</param>
	/// <returns><see langword="true"/> when any flag is present.</returns>
	private static bool HasAny(InotifyEventMask mask, InotifyEventMask values)
	{
		return (mask & values) != 0;
	}

	/// <summary>
	/// Resolves tick-dispatch outcome from startup-first and post-poll dispatch calls.
	/// </summary>
	/// <param name="current">Current dispatch outcome.</param>
	/// <param name="latest">Latest dispatch outcome.</param>
	/// <returns>Resolved dispatch outcome for tick diagnostics.</returns>
	private static MergeScanDispatchOutcome ResolveDispatchOutcome(
		MergeScanDispatchOutcome current,
		MergeScanDispatchOutcome latest)
	{
		return latest == MergeScanDispatchOutcome.NoPendingRequest
			? current
			: latest;
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> when the pipeline has been disposed.
	/// </summary>
	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
	}

	/// <summary>
	/// Builds one immutable logging context dictionary.
	/// </summary>
	/// <param name="pairs">Context key/value pairs.</param>
	/// <returns>Context dictionary.</returns>
	private static IReadOnlyDictionary<string, string> BuildContext(params (string Key, string Value)[] pairs)
	{
		Dictionary<string, string> context = new(StringComparer.Ordinal);
		for (int index = 0; index < pairs.Length; index++)
		{
			(string key, string value) = pairs[index];
			if (!string.IsNullOrWhiteSpace(key))
			{
				context[key] = value;
			}
		}

		return context;
	}
}
