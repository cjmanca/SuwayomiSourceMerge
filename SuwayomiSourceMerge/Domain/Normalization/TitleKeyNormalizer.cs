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
	/// Strips trailing scene-tag suffixes from one title while preserving display formatting.
	/// </summary>
	/// <param name="input">Raw title value.</param>
	/// <param name="sceneTagMatcher">
	/// Optional matcher used to remove trailing scene-tag suffixes.
	/// </param>
	/// <returns>
	/// Input title text with any matching trailing scene-tag suffixes removed and outer whitespace trimmed.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string StripTrailingSceneTagSuffixes(string input, ISceneTagMatcher? sceneTagMatcher)
	{
		ArgumentNullException.ThrowIfNull(input);

		if (string.IsNullOrWhiteSpace(input))
		{
			return string.Empty;
		}

		return StripSceneTagSuffixes(input.Trim(), sceneTagMatcher);
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

		if (!TryGetTrailingBracketSuffixRange(value, out int openingIndex, out int closingIndex))
		{
			return false;
		}

		int tagLength = closingIndex - openingIndex - 1;
		if (tagLength <= 0)
		{
			return false;
		}

		string candidate = value.Substring(openingIndex + 1, tagLength).Trim();
		if (!TryMatchSceneTagCandidate(candidate, sceneTagMatcher))
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
			if (TryMatchSceneTagCandidate(candidate, sceneTagMatcher))
			{
				strippedValue = value[..delimiterIndex].TrimEnd();
				return true;
			}

			searchStart = delimiterIndex - 1;
		}

		return false;
	}

	/// <summary>
	/// Attempts scene-tag candidate matching with fallback trimming of trailing punctuation noise.
	/// </summary>
	/// <param name="candidate">Candidate suffix text.</param>
	/// <param name="sceneTagMatcher">Matcher used for scene-tag checks.</param>
	/// <returns><see langword="true"/> when candidate matches one configured scene tag.</returns>
	private static bool TryMatchSceneTagCandidate(string candidate, ISceneTagMatcher sceneTagMatcher)
	{
		if (sceneTagMatcher.IsMatch(candidate))
		{
			return true;
		}

		if (!ContainsLetterOrDigit(candidate))
		{
			return false;
		}

		string trimmedCandidate = TrimTrailingNonAlphanumeric(candidate);
		if (trimmedCandidate.Length == 0 || string.Equals(trimmedCandidate, candidate, StringComparison.Ordinal))
		{
			return false;
		}

		return sceneTagMatcher.IsMatch(trimmedCandidate);
	}

	/// <summary>
	/// Attempts to locate the opening/closing bracket range for one trailing bracketed suffix.
	/// </summary>
	/// <param name="value">Title text being inspected.</param>
	/// <param name="openingIndex">Opening bracket index when found.</param>
	/// <param name="closingIndex">Closing bracket index when found.</param>
	/// <returns><see langword="true"/> when one trailing bracketed suffix range is found.</returns>
	private static bool TryGetTrailingBracketSuffixRange(string value, out int openingIndex, out int closingIndex)
	{
		openingIndex = -1;
		closingIndex = -1;

		for (int index = value.Length - 1; index >= 0; index--)
		{
			char current = value[index];
			if (current is ')' or ']')
			{
				closingIndex = index;
				break;
			}

			if (char.IsLetterOrDigit(current))
			{
				return false;
			}
		}

		if (closingIndex < 0)
		{
			return false;
		}

		if (closingIndex == 0)
		{
			return false;
		}

		char opening = value[closingIndex] == ')' ? '(' : '[';
		openingIndex = value.LastIndexOf(opening, closingIndex - 1);
		return openingIndex >= 0;
	}

	/// <summary>
	/// Trims trailing non-alphanumeric characters from one candidate suffix.
	/// </summary>
	/// <param name="candidate">Candidate suffix text.</param>
	/// <returns>Candidate text trimmed to the last alphanumeric character.</returns>
	private static string TrimTrailingNonAlphanumeric(string candidate)
	{
		int endExclusive = candidate.Length;
		while (endExclusive > 0 && !char.IsLetterOrDigit(candidate[endExclusive - 1]))
		{
			endExclusive--;
		}

		return candidate[..endExclusive].TrimEnd();
	}

	/// <summary>
	/// Determines whether candidate text contains at least one alphanumeric character.
	/// </summary>
	/// <param name="candidate">Candidate text.</param>
	/// <returns><see langword="true"/> when one letter or digit exists; otherwise <see langword="false"/>.</returns>
	private static bool ContainsLetterOrDigit(string candidate)
	{
		for (int index = 0; index < candidate.Length; index++)
		{
			if (char.IsLetterOrDigit(candidate[index]))
			{
				return true;
			}
		}

		return false;
	}
}
