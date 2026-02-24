using System.Net;
using System.Text.RegularExpressions;

using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

namespace SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Description and title-block formatting helpers for <see cref="OverrideDetailsService"/>.
/// </summary>
internal sealed partial class OverrideDetailsService
{
	/// <summary>
	/// Header used for the appended language-coded title bullet block.
	/// </summary>
	private const string TitleBlockHeader = "Titles:";

	/// <summary>
	/// Fallback language token used when Comick does not provide a language code.
	/// </summary>
	private const string UnknownLanguageCode = "unknown";

	/// <summary>
	/// Regex used to normalize HTML line-break tags to newlines.
	/// </summary>
	private static readonly Regex _lineBreakTagRegex = new(
		@"<\s*br\s*/?\s*>",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	/// <summary>
	/// Regex used to replace closing paragraph tags with blank-line boundaries.
	/// </summary>
	private static readonly Regex _paragraphCloseTagRegex = new(
		@"<\s*/\s*p\s*>",
		RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	/// <summary>
	/// Regex used to remove remaining HTML tags from parsed description fallback text.
	/// </summary>
	private static readonly Regex _htmlTagRegex = new(
		@"<[^>]+>",
		RegexOptions.CultureInvariant);

	/// <summary>
	/// Resolves Comick-first description source text using Comick description and parsed fallback only.
	/// </summary>
	/// <param name="comickComic">Matched Comick payload.</param>
	/// <returns>Description source text.</returns>
	private static string ResolveComickDescriptionSourceFromComick(ComickComicResponse comickComic)
	{
		ArgumentNullException.ThrowIfNull(comickComic);

		string? description = comickComic.Comic?.Description;
		if (!string.IsNullOrWhiteSpace(description))
		{
			return description.Trim();
		}

		string parsedDescription = NormalizeParsedDescription(comickComic.Comic?.ParsedDescription);
		if (!string.IsNullOrWhiteSpace(parsedDescription))
		{
			return parsedDescription;
		}

		return string.Empty;
	}

	/// <summary>
	/// Normalizes parsed HTML description text into plain line-oriented text.
	/// </summary>
	/// <param name="parsedDescription">Raw parsed HTML description.</param>
	/// <returns>Normalized plain-text description.</returns>
	private static string NormalizeParsedDescription(string? parsedDescription)
	{
		if (string.IsNullOrWhiteSpace(parsedDescription))
		{
			return string.Empty;
		}

		string normalized = parsedDescription
			.Replace("\r\n", "\n", StringComparison.Ordinal)
			.Replace("\r", "\n", StringComparison.Ordinal);
		normalized = _paragraphCloseTagRegex.Replace(normalized, "\n\n");
		normalized = _lineBreakTagRegex.Replace(normalized, "\n");
		normalized = _htmlTagRegex.Replace(normalized, string.Empty);
		normalized = WebUtility.HtmlDecode(normalized);

		return normalized.Trim();
	}

	/// <summary>
	/// Appends a language-coded bullet-list block of main and alternate titles to one description body.
	/// </summary>
	/// <param name="description">Description body.</param>
	/// <param name="comickComic">Comick payload.</param>
	/// <returns>Description body with appended title block.</returns>
	private static string AppendLanguageTitleBlock(string description, ComickComicResponse comickComic)
	{
		ArgumentNullException.ThrowIfNull(description);
		ArgumentNullException.ThrowIfNull(comickComic);

		List<string> bulletLines = BuildLanguageTitleBulletLines(comickComic);
		if (bulletLines.Count == 0)
		{
			return description.Trim();
		}

		string titleBlock = string.Join(
			"\n",
			new[] { TitleBlockHeader }.Concat(bulletLines));
		string trimmedDescription = description.Trim();
		if (trimmedDescription.Length == 0)
		{
			return titleBlock;
		}

		return string.Create(
			System.Globalization.CultureInfo.InvariantCulture,
			$"{trimmedDescription}\n\n{titleBlock}");
	}

	/// <summary>
	/// Builds deterministic language-coded bullet lines from one Comick payload.
	/// </summary>
	/// <param name="comickComic">Comick payload.</param>
	/// <returns>Ordered language-coded bullet lines.</returns>
	private static List<string> BuildLanguageTitleBulletLines(ComickComicResponse comickComic)
	{
		ArgumentNullException.ThrowIfNull(comickComic);

		HashSet<string> seenLines = new(StringComparer.Ordinal);
		List<string> bulletLines = [];

		ComickComicDetails? comicDetails = comickComic.Comic;
		AddLanguageTitleBulletLine(
			comicDetails?.Iso6391,
			comicDetails?.Title,
			seenLines,
			bulletLines);

		if (comicDetails?.MdTitles is null)
		{
			return bulletLines;
		}

		for (int index = 0; index < comicDetails.MdTitles.Count; index++)
		{
			ComickTitleAlias? alias = comicDetails.MdTitles[index];
			if (alias is null)
			{
				continue;
			}

			AddLanguageTitleBulletLine(alias.Language, alias.Title, seenLines, bulletLines);
		}

		return bulletLines;
	}

	/// <summary>
	/// Adds one language-coded title bullet line when valid and not already present.
	/// </summary>
	/// <param name="languageCode">Language code.</param>
	/// <param name="title">Title text.</param>
	/// <param name="seenLines">Seen-line set.</param>
	/// <param name="bulletLines">Output bullet lines.</param>
	private static void AddLanguageTitleBulletLine(
		string? languageCode,
		string? title,
		HashSet<string> seenLines,
		List<string> bulletLines)
	{
		ArgumentNullException.ThrowIfNull(seenLines);
		ArgumentNullException.ThrowIfNull(bulletLines);

		if (string.IsNullOrWhiteSpace(title))
		{
			return;
		}

		string normalizedLanguageCode = NormalizeLanguageCode(languageCode);
		string trimmedTitle = title.Trim();
		string bulletLine = string.Create(
			System.Globalization.CultureInfo.InvariantCulture,
			$"- [{normalizedLanguageCode}] {trimmedTitle}");
		if (seenLines.Add(bulletLine))
		{
			bulletLines.Add(bulletLine);
		}
	}

	/// <summary>
	/// Normalizes a language code token for language-coded bullet output.
	/// </summary>
	/// <param name="languageCode">Language code text.</param>
	/// <returns>Normalized language code token.</returns>
	private static string NormalizeLanguageCode(string? languageCode)
	{
		if (string.IsNullOrWhiteSpace(languageCode))
		{
			return UnknownLanguageCode;
		}

		return languageCode.Trim().ToLowerInvariant();
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
}
