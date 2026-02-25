namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Canonical-title selection helpers for <see cref="MangaEquivalentsUpdateService"/>.
/// </summary>
internal sealed partial class MangaEquivalentsUpdateService
{
	/// <summary>
	/// Preferred fallback language code used when preferred-language matches are unavailable.
	/// </summary>
	private const string EnglishLanguageCode = "en";

	/// <summary>
	/// Selects canonical title according to preferred-language, prefix-language, English fallback, then main-title fallback.
	/// </summary>
	/// <param name="preferredLanguage">Preferred language token.</param>
	/// <param name="mainTitle">Main title fallback.</param>
	/// <param name="canonicalSelectionAlternates">Alternate titles considered for canonical selection in source order.</param>
	/// <returns>Selected canonical title.</returns>
	private static string SelectCanonicalTitle(
		string preferredLanguage,
		string mainTitle,
		IReadOnlyList<IncomingTitleCandidate> canonicalSelectionAlternates)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);
		ArgumentException.ThrowIfNullOrWhiteSpace(mainTitle);
		ArgumentNullException.ThrowIfNull(canonicalSelectionAlternates);

		IncomingTitleCandidate? exactPreferred = FindFirstAlternateByLanguage(canonicalSelectionAlternates, preferredLanguage, requireExact: true);
		if (exactPreferred is not null)
		{
			return exactPreferred.Title;
		}

		string preferredPrefix = preferredLanguage.Trim().Length >= 2
			? preferredLanguage.Trim()[..2]
			: preferredLanguage.Trim();
		IncomingTitleCandidate? prefixPreferred = FindFirstAlternateByLanguagePrefix(canonicalSelectionAlternates, preferredPrefix);
		if (prefixPreferred is not null)
		{
			return prefixPreferred.Title;
		}

		IncomingTitleCandidate? englishFallback = FindFirstAlternateByLanguage(canonicalSelectionAlternates, EnglishLanguageCode, requireExact: true);
		if (englishFallback is not null)
		{
			return englishFallback.Title;
		}

		return mainTitle.Trim();
	}

	/// <summary>
	/// Finds the first alternate title with a matching language token.
	/// </summary>
	/// <param name="canonicalSelectionAlternates">Alternates used for canonical selection.</param>
	/// <param name="language">Target language token.</param>
	/// <param name="requireExact">Whether match requires exact token equality.</param>
	/// <returns>Matching alternate title or <see langword="null"/>.</returns>
	private static IncomingTitleCandidate? FindFirstAlternateByLanguage(
		IReadOnlyList<IncomingTitleCandidate> canonicalSelectionAlternates,
		string language,
		bool requireExact)
	{
		ArgumentNullException.ThrowIfNull(canonicalSelectionAlternates);
		ArgumentException.ThrowIfNullOrWhiteSpace(language);

		for (int index = 0; index < canonicalSelectionAlternates.Count; index++)
		{
			IncomingTitleCandidate candidate = canonicalSelectionAlternates[index];
			if (!candidate.IsAlternate || string.IsNullOrWhiteSpace(candidate.Language))
			{
				continue;
			}

			bool isMatch = requireExact
				? string.Equals(candidate.Language, language, StringComparison.OrdinalIgnoreCase)
				: candidate.Language.StartsWith(language, StringComparison.OrdinalIgnoreCase);
			if (isMatch)
			{
				return candidate;
			}
		}

		return null;
	}

	/// <summary>
	/// Finds the first alternate title whose language token matches the preferred two-character prefix.
	/// </summary>
	/// <param name="canonicalSelectionAlternates">Alternates used for canonical selection.</param>
	/// <param name="preferredPrefix">Preferred-language prefix.</param>
	/// <returns>Matching alternate title or <see langword="null"/>.</returns>
	private static IncomingTitleCandidate? FindFirstAlternateByLanguagePrefix(
		IReadOnlyList<IncomingTitleCandidate> canonicalSelectionAlternates,
		string preferredPrefix)
	{
		ArgumentNullException.ThrowIfNull(canonicalSelectionAlternates);
		if (string.IsNullOrWhiteSpace(preferredPrefix))
		{
			return null;
		}

		for (int index = 0; index < canonicalSelectionAlternates.Count; index++)
		{
			IncomingTitleCandidate candidate = canonicalSelectionAlternates[index];
			if (!candidate.IsAlternate || string.IsNullOrWhiteSpace(candidate.Language))
			{
				continue;
			}

			if (candidate.Language.StartsWith(preferredPrefix, StringComparison.OrdinalIgnoreCase))
			{
				return candidate;
			}
		}

		return null;
	}
}
