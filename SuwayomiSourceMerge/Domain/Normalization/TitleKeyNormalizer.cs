namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Produces normalized comparison keys for title and token matching.
/// </summary>
/// <remarks>
/// This type is layer-neutral and intended for use by runtime resolution and validation flows
/// so normalization behavior stays consistent without cross-layer dependencies.
/// </remarks>
internal static class TitleKeyNormalizer
{
	/// <summary>
	/// Normalizes a title into a compact alphanumeric key for title matching.
	/// </summary>
	/// <param name="input">Raw title value.</param>
	/// <returns>
	/// A normalized key with ASCII folding, punctuation removal, leading-article stripping, and
	/// per-word trailing <c>s</c> trimming applied.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string NormalizeTitleKey(string input)
	{
		return NormalizeTitleKey(input, sceneTagMatcher: null);
	}

	/// <summary>
	/// Normalizes a title into a compact alphanumeric key with optional scene-tag suffix stripping.
	/// </summary>
	/// <param name="input">Raw title value.</param>
	/// <param name="sceneTagMatcher">
	/// Optional matcher used to remove trailing scene-tag suffixes prior to punctuation/token normalization.
	/// </param>
	/// <returns>
	/// A normalized key with ASCII folding, trailing scene-tag suffix stripping when configured, punctuation
	/// removal, leading-article stripping, and per-word trailing <c>s</c> trimming applied.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string NormalizeTitleKey(string input, ISceneTagMatcher? sceneTagMatcher)
	{
		ArgumentNullException.ThrowIfNull(input);

		if (string.IsNullOrWhiteSpace(input))
		{
			return string.Empty;
		}

		string folded = ComparisonTextNormalizer.FoldToAscii(input).ToLowerInvariant();
		folded = StripSceneTagSuffixes(folded, sceneTagMatcher);
		folded = ComparisonTextNormalizer.ReplacePunctuationWithSpace(folded);

		string[] words = folded
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		if (words.Length == 0)
		{
			return string.Empty;
		}

		int wordStartIndex = IsLeadingArticle(words[0]) ? 1 : 0;

		for (int i = wordStartIndex; i < words.Length; i++)
		{
			if (words[i].Length > 1 && words[i].EndsWith('s'))
			{
				words[i] = words[i][..^1];
			}
		}

		ReadOnlySpan<string?> wordsSpan = words;
		return string.Concat(wordsSpan[wordStartIndex..]);
	}

	/// <summary>
	/// Normalizes a token-like value while preserving word boundaries.
	/// </summary>
	/// <param name="input">Raw token value.</param>
	/// <returns>A lower-case, ASCII-folded, punctuation-normalized token string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string NormalizeTokenKey(string input)
	{
		return ComparisonTextNormalizer.NormalizeTokenKey(input);
	}

	/// <summary>
	/// Determines whether a word is one of the canonical leading articles removed from title keys.
	/// </summary>
	/// <param name="word">Word token to evaluate.</param>
	/// <returns><see langword="true"/> when <paramref name="word"/> is <c>a</c>, <c>an</c>, or <c>the</c>.</returns>
	private static bool IsLeadingArticle(string word)
	{
		return word is "a" or "an" or "the";
	}

	/// <summary>
	/// Removes trailing scene-tag suffixes while a configured matcher recognizes trailing fragments.
	/// </summary>
	/// <param name="value">Lower-cased folded title value.</param>
	/// <param name="sceneTagMatcher">Optional matcher used to identify configured scene-tag suffixes.</param>
	/// <returns>Title text with any matching trailing scene-tag suffixes removed.</returns>
	private static string StripSceneTagSuffixes(string value, ISceneTagMatcher? sceneTagMatcher)
	{
		if (sceneTagMatcher is null || string.IsNullOrWhiteSpace(value))
		{
			return value;
		}

		string current = value.Trim();

		while (true)
		{
			if (TryStripBracketedSuffix(current, sceneTagMatcher, out string strippedBracketed))
			{
				current = strippedBracketed;
				continue;
			}

			if (TryStripDelimitedSuffix(current, '-', sceneTagMatcher, out string strippedHyphenated))
			{
				current = strippedHyphenated;
				continue;
			}

			if (TryStripDelimitedSuffix(current, ':', sceneTagMatcher, out string strippedColonDelimited))
			{
				current = strippedColonDelimited;
				continue;
			}

			return current;
		}
	}

	/// <summary>
	/// Attempts to strip one trailing bracketed suffix when it matches a configured scene tag.
	/// </summary>
	/// <param name="value">Title value to inspect.</param>
	/// <param name="sceneTagMatcher">Matcher used for suffix comparison.</param>
	/// <param name="strippedValue">Title text after removing one matched suffix.</param>
	/// <returns><see langword="true"/> when one trailing bracketed suffix was removed.</returns>
	private static bool TryStripBracketedSuffix(
		string value,
		ISceneTagMatcher sceneTagMatcher,
		out string strippedValue)
	{
		strippedValue = value;

		if (value.Length < 3)
		{
			return false;
		}

		char closing = value[^1];
		char opening = closing switch
		{
			')' => '(',
			']' => '[',
			_ => '\0'
		};

		if (opening == '\0')
		{
			return false;
		}

		int openingIndex = value.LastIndexOf(opening);
		if (openingIndex < 0)
		{
			return false;
		}

		int tagLength = value.Length - openingIndex - 2;
		if (tagLength <= 0)
		{
			return false;
		}

		string candidate = value.Substring(openingIndex + 1, tagLength).Trim();
		if (!sceneTagMatcher.IsMatch(candidate))
		{
			return false;
		}

		strippedValue = value[..openingIndex].TrimEnd();
		return true;
	}

	/// <summary>
	/// Attempts to strip one trailing delimiter-based suffix when it matches a configured scene tag.
	/// </summary>
	/// <param name="value">Title value to inspect.</param>
	/// <param name="delimiter">Delimiter that precedes the suffix phrase.</param>
	/// <param name="sceneTagMatcher">Matcher used for suffix comparison.</param>
	/// <param name="strippedValue">Title text after removing one matched suffix.</param>
	/// <returns><see langword="true"/> when one trailing delimiter-based suffix was removed.</returns>
	private static bool TryStripDelimitedSuffix(
		string value,
		char delimiter,
		ISceneTagMatcher sceneTagMatcher,
		out string strippedValue)
	{
		strippedValue = value;
		int searchStart = value.Length - 1;

		while (searchStart > 0)
		{
			int delimiterIndex = value.LastIndexOf(delimiter, searchStart);
			if (delimiterIndex <= 0)
			{
				return false;
			}

			string candidate = value[(delimiterIndex + 1)..].Trim();
			if (sceneTagMatcher.IsMatch(candidate))
			{
				strippedValue = value[..delimiterIndex].TrimEnd();
				return true;
			}

			searchStart = delimiterIndex - 1;
		}

		return false;
	}
}
