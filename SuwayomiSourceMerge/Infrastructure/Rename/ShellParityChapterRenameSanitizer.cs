using System.Text.RegularExpressions;

namespace SuwayomiSourceMerge.Infrastructure.Rename;

/// <summary>
/// Applies chapter-directory sanitization heuristics aligned with shell v1 behavior.
/// </summary>
internal sealed class ShellParityChapterRenameSanitizer : IChapterRenameSanitizer
{
	/// <summary>
	/// Blacklisted prefix patterns that must never be rewritten.
	/// </summary>
	private static readonly Regex[] BlacklistedPrefixPatterns =
	[
		CreatePattern("^(ch|ch\\.|chapter)$"),
		CreatePattern("^(ep|ep\\.|episode)$"),
		CreatePattern("^(issue)$"),
		CreatePattern("^(sp|sp\\.|special)$"),
		CreatePattern("^(extra|side|omake|oneshot|pilot|interlude|prologue|afterword)$"),
		CreatePattern("^(vol|vol\\.|volume)$"),
		CreatePattern("^(part|pt)\\.?[0-9]+$"),
		CreatePattern("^s[0-9]+$"),
		CreatePattern("^season[0-9]+$"),
		CreatePattern("^(en|eng|jp|jpn|kr|kor|cn|chi|fr|es|de|pt|it|ru)$"),
		CreatePattern("^\\(s[0-9]+\\)$"),
		CreatePattern("^\\(season[0-9]+\\)$")
	];

	/// <summary>
	/// Whitelisted prefix patterns eligible for numeric stripping.
	/// </summary>
	private static readonly Regex[] WhitelistedPrefixPatterns =
	[
		CreatePattern("^team[A-Za-z0-9]*$"),
		CreatePattern(".*scan.*"),
		CreatePattern(".*subs?$"),
		CreatePattern("^(tl|tls|trans|translate|translator)[A-Za-z0-9]*$"),
		CreatePattern("^(anon|anonymous)[A-Za-z0-9]*$"),
		CreatePattern(".*group.*"),
		CreatePattern(".*studio.*")
	];

	/// <summary>
	/// Boundary-token chapter markers aligned with shell baseline matching.
	/// </summary>
	private static readonly string[] BoundaryChapterTokens =
	[
		"ch\\.",
		"chapter",
		"ep\\.",
		"episode",
		"issue",
		"special",
		"extra",
		"side",
		"volume",
		"vol\\."
	];

	/// <summary>
	/// Embedded chapter-token markers used by docs-first compact-name matching.
	/// </summary>
	private static readonly string[] EmbeddedChapterTokens =
	[
		"ch\\.",
		"chapter",
		"ep\\.",
		"episode",
		"issue",
		"special",
		"extra",
		"side",
		"volume",
		"vol\\."
	];

	/// <summary>
	/// Boundary-based chapter token detector.
	/// </summary>
	private static readonly Regex ChapterTokenRegex = CreatePattern(
		$"(^|[^a-z])({BuildTokenAlternation(BoundaryChapterTokens)})([^a-z]|$)");

	/// <summary>
	/// Fallback detector for embedded chapter tokens in compact names (for example, <c>MangaChapter6</c>).
	/// </summary>
	/// <remarks>
	/// This intentionally extends strict shell boundary matching so documented rename examples such as
	/// <c>Team-S3_MangaChapter6</c> continue to normalize to <c>Team-S_MangaChapter6</c>.
	/// Embedded matching also includes abbreviated chapter and episode tokens (<c>ch.</c>, <c>ep.</c>)
	/// so compact names keep consistent docs-first normalization behavior.
	/// </remarks>
	private static readonly Regex EmbeddedChapterTokenRegex = CreatePattern(
		$"({BuildTokenAlternation(EmbeddedChapterTokens)})([0-9]|$)");

	/// <summary>
	/// Case-2 prefix matcher for names like <c>Group123 Chapter 45</c>.
	/// </summary>
	private static readonly Regex PrefixSpaceChapterPattern = CreatePattern(
		"^([A-Za-z][A-Za-z0-9]*[0-9][A-Za-z0-9]*)\\s+(.+)$");

	/// <summary>
	/// Case-2 rest matcher for chapter-like text.
	/// </summary>
	private static readonly Regex CaseTwoChapterStartPattern = CreatePattern(
		"^(ch\\.|chapter|ep\\.|episode|issue|special|extra|side|season|volume|vol\\.)");

	/// <inheritdoc />
	public string Sanitize(string name)
	{
		ArgumentNullException.ThrowIfNull(name);

		if (string.IsNullOrWhiteSpace(name))
		{
			return name;
		}

		if (TrySanitizeUnderscorePattern(name, out string sanitizedUnderscorePattern))
		{
			return sanitizedUnderscorePattern;
		}

		if (TrySanitizePrefixSpacePattern(name, out string sanitizedPrefixSpacePattern))
		{
			return sanitizedPrefixSpacePattern;
		}

		return name;
	}

	/// <summary>
	/// Attempts case-1 sanitization for names formatted like <c>Group_Chapter...</c>.
	/// </summary>
	/// <param name="name">Name under evaluation.</param>
	/// <param name="sanitizedName">Sanitized output when rewritten.</param>
	/// <returns><see langword="true"/> when case-1 rewrite rules were applied.</returns>
	private static bool TrySanitizeUnderscorePattern(string name, out string sanitizedName)
	{
		sanitizedName = name;

		int underscoreIndex = name.IndexOf('_');
		if (underscoreIndex <= 0 || underscoreIndex == name.Length - 1)
		{
			return false;
		}

		string prefix = name[..underscoreIndex];
		string rest = name[(underscoreIndex + 1)..];

		(string prefixToken, string prefixTail) = SplitPrefixToken(prefix);
		if (IsBlacklistedPrefix(prefixToken))
		{
			return false;
		}

		bool allowed = IsWhitelistedPrefix(prefixToken) || LooksLikeGroupPrefix(prefixToken);
		if (!allowed || !IsChapterish(rest))
		{
			return false;
		}

		string cleanedToken = StripDigits(prefixToken);
		if (cleanedToken.Length == 0)
		{
			return false;
		}

		sanitizedName = $"{cleanedToken}{prefixTail}_{rest}";
		return true;
	}

	/// <summary>
	/// Attempts case-2 sanitization for names formatted like <c>Group123 Chapter 45</c>.
	/// </summary>
	/// <param name="name">Name under evaluation.</param>
	/// <param name="sanitizedName">Sanitized output when rewritten.</param>
	/// <returns><see langword="true"/> when case-2 rewrite rules were applied.</returns>
	private static bool TrySanitizePrefixSpacePattern(string name, out string sanitizedName)
	{
		sanitizedName = name;
		Match match = PrefixSpaceChapterPattern.Match(name);
		if (!match.Success)
		{
			return false;
		}

		string prefix = match.Groups[1].Value;
		string rest = match.Groups[2].Value;
		if (IsBlacklistedPrefix(prefix))
		{
			return false;
		}

		bool allowed = IsWhitelistedPrefix(prefix) || LooksLikeGroupPrefix(prefix);
		if (!allowed || !CaseTwoChapterStartPattern.IsMatch(rest))
		{
			return false;
		}

		string cleanedPrefix = StripDigits(prefix);
		if (cleanedPrefix.Length == 0)
		{
			return false;
		}

		sanitizedName = $"{cleanedPrefix} {rest}";
		return true;
	}

	/// <summary>
	/// Splits a prefix into its first token and trailing suffix.
	/// </summary>
	/// <param name="prefix">Prefix value to split.</param>
	/// <returns>Tuple containing the first token and trailing suffix (including whitespace).</returns>
	private static (string PrefixToken, string PrefixTail) SplitPrefixToken(string prefix)
	{
		for (int index = 0; index < prefix.Length; index++)
		{
			if (char.IsWhiteSpace(prefix[index]))
			{
				return (prefix[..index], prefix[index..]);
			}
		}

		return (prefix, string.Empty);
	}

	/// <summary>
	/// Returns whether one prefix matches blacklist rules.
	/// </summary>
	/// <param name="prefix">Prefix token to evaluate.</param>
	/// <returns><see langword="true"/> when the prefix is blacklisted.</returns>
	private static bool IsBlacklistedPrefix(string prefix)
	{
		return MatchesAnyPattern(prefix, BlacklistedPrefixPatterns);
	}

	/// <summary>
	/// Returns whether one prefix matches whitelist rules.
	/// </summary>
	/// <param name="prefix">Prefix token to evaluate.</param>
	/// <returns><see langword="true"/> when the prefix is whitelisted.</returns>
	private static bool IsWhitelistedPrefix(string prefix)
	{
		return MatchesAnyPattern(prefix, WhitelistedPrefixPatterns);
	}

	/// <summary>
	/// Returns whether one string looks like a group prefix containing both letters and digits.
	/// </summary>
	/// <param name="prefix">Prefix token to evaluate.</param>
	/// <returns><see langword="true"/> when the token looks like a release-group prefix.</returns>
	private static bool LooksLikeGroupPrefix(string prefix)
	{
		if (IsBlacklistedPrefix(prefix))
		{
			return false;
		}

		bool hasLetter = false;
		bool hasDigit = false;
		foreach (char character in prefix)
		{
			if (char.IsLetter(character))
			{
				hasLetter = true;
			}
			else if (char.IsDigit(character))
			{
				hasDigit = true;
			}

			if (hasLetter && hasDigit)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Returns whether one string is chapter-like.
	/// </summary>
	/// <param name="value">String value to evaluate.</param>
	/// <returns><see langword="true"/> when chapter-like markers are present.</returns>
	private static bool IsChapterish(string value)
	{
		return ChapterTokenRegex.IsMatch(value) || EmbeddedChapterTokenRegex.IsMatch(value);
	}

	/// <summary>
	/// Removes numeric characters from one input string.
	/// </summary>
	/// <param name="value">Input text.</param>
	/// <returns>Input text without numeric characters.</returns>
	private static string StripDigits(string value)
	{
		return string.Concat(value.Where(character => !char.IsDigit(character)));
	}

	/// <summary>
	/// Returns whether one value matches any pattern in one set.
	/// </summary>
	/// <param name="value">Input value to test.</param>
	/// <param name="patterns">Patterns to evaluate.</param>
	/// <returns><see langword="true"/> when any pattern matches the value.</returns>
	private static bool MatchesAnyPattern(string value, IReadOnlyList<Regex> patterns)
	{
		for (int index = 0; index < patterns.Count; index++)
		{
			if (patterns[index].IsMatch(value))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Builds one regex alternation string from token patterns.
	/// </summary>
	/// <param name="tokens">Token patterns.</param>
	/// <returns>Joined alternation expression.</returns>
	private static string BuildTokenAlternation(IReadOnlyList<string> tokens)
	{
		return string.Join("|", tokens);
	}

	/// <summary>
	/// Creates one case-insensitive compiled regular expression.
	/// </summary>
	/// <param name="pattern">Pattern text.</param>
	/// <returns>Compiled regular expression.</returns>
	private static Regex CreatePattern(string pattern)
	{
		return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
	}
}
