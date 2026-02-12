using System.Globalization;
using System.Text;

namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Provides shared text-normalization primitives used by comparison and matching flows.
/// </summary>
/// <remarks>
/// These methods are layer-neutral and can be consumed by both domain matching services and
/// configuration validators without creating cross-module coupling.
/// </remarks>
internal static class ComparisonTextNormalizer
{
	/// <summary>
	/// Normalizes token-like text into a lower-case ASCII key with punctuation mapped to spaces.
	/// </summary>
	/// <param name="input">Raw token text to normalize.</param>
	/// <returns>A normalized token key with collapsed whitespace between token words.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string NormalizeTokenKey(string input)
	{
		ArgumentNullException.ThrowIfNull(input);

		if (string.IsNullOrWhiteSpace(input))
		{
			return string.Empty;
		}

		string folded = FoldToAscii(input).ToLowerInvariant();
		folded = ReplacePunctuationWithSpace(folded);

		string[] words = folded
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.ToArray();

		return string.Join(' ', words);
	}

	/// <summary>
	/// Performs Unicode decomposition and removes combining marks to approximate ASCII folding.
	/// </summary>
	/// <param name="input">Input text to fold.</param>
	/// <returns>Text with diacritic marks removed and normalization form restored.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string FoldToAscii(string input)
	{
		ArgumentNullException.ThrowIfNull(input);

		string decomposed = input.Normalize(NormalizationForm.FormD);
		StringBuilder builder = new(decomposed.Length);

		foreach (char c in decomposed)
		{
			UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
			if (category != UnicodeCategory.NonSpacingMark)
			{
				builder.Append(c);
			}
		}

		return builder.ToString().Normalize(NormalizationForm.FormC);
	}

	/// <summary>
	/// Replaces all non-alphanumeric characters with spaces.
	/// </summary>
	/// <param name="input">Input text to normalize.</param>
	/// <returns>Text where punctuation and symbols are replaced by spaces.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string ReplacePunctuationWithSpace(string input)
	{
		ArgumentNullException.ThrowIfNull(input);

		StringBuilder builder = new(input.Length);
		foreach (char ch in input)
		{
			if (char.IsLetterOrDigit(ch))
			{
				builder.Append(ch);
			}
			else
			{
				builder.Append(' ');
			}
		}

		return builder.ToString();
	}
}
