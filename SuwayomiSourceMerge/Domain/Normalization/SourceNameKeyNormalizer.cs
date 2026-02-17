namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Produces normalized lookup keys for source-name exclusion matching.
/// </summary>
/// <remarks>
/// This helper intentionally applies only trim + case-insensitive normalization so source exclusion
/// behavior stays strict and predictable without punctuation-folding over-match risks.
/// </remarks>
internal static class SourceNameKeyNormalizer
{
	/// <summary>
	/// Normalizes one source name into canonical lookup-key form.
	/// </summary>
	/// <param name="sourceName">Source name text.</param>
	/// <returns>Trimmed lower-case source key, or empty string when the input is whitespace.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="sourceName"/> is <see langword="null"/>.</exception>
	public static string NormalizeSourceKey(string sourceName)
	{
		ArgumentNullException.ThrowIfNull(sourceName);

		if (string.IsNullOrWhiteSpace(sourceName))
		{
			return string.Empty;
		}

		return sourceName.Trim().ToLowerInvariant();
	}
}
