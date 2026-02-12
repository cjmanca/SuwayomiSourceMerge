using System.Globalization;
using System.Text;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Produces normalized comparison keys used by configuration validators.
/// </summary>
/// <remarks>
/// The normalization logic here is intentionally shared so validators apply identical folding behavior
/// when checking uniqueness and conflicts.
/// </remarks>
internal static class ValidationKeyNormalizer
{
	/// <summary>
	/// Leading article tokens removed from title keys during normalization.
	/// </summary>
	private static readonly string[] LeadingArticles =
	[
		"a",
		"an",
		"the"
	];

	/// <summary>
	/// Normalizes a title into a compact alphanumeric key for equivalence comparisons.
	/// </summary>
	/// <param name="input">Raw title value.</param>
	/// <returns>
	/// A normalized key with ASCII folding, punctuation removal, leading-article stripping, and
	/// per-word trailing <c>s</c> trimming applied.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string NormalizeTitleKey(string input)
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

		if (words.Length == 0)
		{
			return string.Empty;
		}

		if (LeadingArticles.Contains(words[0], StringComparer.Ordinal))
		{
			words = words.Skip(1).ToArray();
		}

		for (int i = 0; i < words.Length; i++)
		{
			if (words[i].Length > 1 && words[i].EndsWith('s'))
			{
				words[i] = words[i][..^1];
			}
		}

		return string.Concat(words);
	}

	/// <summary>
	/// Normalizes a token-like value while preserving word boundaries.
	/// </summary>
	/// <param name="input">Raw token value.</param>
	/// <returns>A lower-case, ASCII-folded, punctuation-normalized token string.</returns>
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
	/// Replaces non-alphanumeric characters with spaces to normalize separator variants.
	/// </summary>
	/// <param name="input">Input text to normalize.</param>
	/// <returns>Text where punctuation/separators are replaced by single-character spaces.</returns>
	private static string ReplacePunctuationWithSpace(string input)
	{
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

	/// <summary>
	/// Performs Unicode decomposition and removes combining marks to approximate ASCII folding.
	/// </summary>
	/// <param name="input">Input text to fold.</param>
	/// <returns>Text with diacritic marks removed and normalization form restored.</returns>
	private static string FoldToAscii(string input)
	{
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
}
