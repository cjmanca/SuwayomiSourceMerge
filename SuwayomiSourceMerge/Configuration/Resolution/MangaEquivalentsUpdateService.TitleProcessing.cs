using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Title-candidate matching and document-mutation helpers for <see cref="MangaEquivalentsUpdateService"/>.
/// </summary>
internal sealed partial class MangaEquivalentsUpdateService
{
	/// <summary>
	/// Builds deduped incoming title candidates from main title and alternate titles.
	/// </summary>
	/// <param name="request">Update request.</param>
	/// <param name="normalizer">Title normalizer.</param>
	/// <returns>Deduped candidate list preserving first-seen order.</returns>
	private static List<IncomingTitleCandidate> BuildDedupedIncomingTitles(
		MangaEquivalentsUpdateRequest request,
		ITitleComparisonNormalizer normalizer)
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(normalizer);

		List<IncomingTitleCandidate> candidates = [];
		HashSet<string> seenNormalizedKeys = new(StringComparer.Ordinal);

		TryAddIncomingCandidate(
			request.MainTitle,
			language: null,
			isAlternate: false,
			normalizer,
			seenNormalizedKeys,
			candidates);

		for (int index = 0; index < request.AlternateTitles.Count; index++)
		{
			MangaEquivalentAlternateTitle alternateTitle = request.AlternateTitles[index];
			TryAddIncomingCandidate(
				alternateTitle.Title,
				alternateTitle.Language,
				isAlternate: true,
				normalizer,
				seenNormalizedKeys,
				candidates);
		}

		return candidates;
	}

	/// <summary>
	/// Builds canonical-selection candidates from alternate titles without normalized-key deduplication.
	/// </summary>
	/// <param name="request">Update request.</param>
	/// <returns>Alternate-title candidates preserving source order.</returns>
	private static List<IncomingTitleCandidate> BuildCanonicalSelectionAlternates(MangaEquivalentsUpdateRequest request)
	{
		ArgumentNullException.ThrowIfNull(request);

		List<IncomingTitleCandidate> alternates = [];
		for (int index = 0; index < request.AlternateTitles.Count; index++)
		{
			MangaEquivalentAlternateTitle alternateTitle = request.AlternateTitles[index];
			alternates.Add(
				new IncomingTitleCandidate(
					alternateTitle.Title,
					NormalizedKey: string.Empty,
					NormalizeLanguage(alternateTitle.Language),
					IsAlternate: true));
		}

		return alternates;
	}

	/// <summary>
	/// Adds one incoming title candidate when normalization yields a non-empty unseen key.
	/// </summary>
	/// <param name="title">Raw title text.</param>
	/// <param name="language">Optional language code.</param>
	/// <param name="isAlternate">Whether this candidate came from alternate-title input.</param>
	/// <param name="normalizer">Title normalizer.</param>
	/// <param name="seenNormalizedKeys">Seen normalized-key set.</param>
	/// <param name="candidates">Candidate list.</param>
	private static void TryAddIncomingCandidate(
		string title,
		string? language,
		bool isAlternate,
		ITitleComparisonNormalizer normalizer,
		ISet<string> seenNormalizedKeys,
		ICollection<IncomingTitleCandidate> candidates)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentNullException.ThrowIfNull(normalizer);
		ArgumentNullException.ThrowIfNull(seenNormalizedKeys);
		ArgumentNullException.ThrowIfNull(candidates);

		string trimmedTitle = title.Trim();
		string normalizedKey = normalizer.NormalizeTitleKey(trimmedTitle);
		if (string.IsNullOrWhiteSpace(normalizedKey) || !seenNormalizedKeys.Add(normalizedKey))
		{
			return;
		}

		candidates.Add(new IncomingTitleCandidate(trimmedTitle, normalizedKey, NormalizeLanguage(language), isAlternate));
	}

	/// <summary>
	/// Normalizes one optional language token.
	/// </summary>
	/// <param name="language">Language token.</param>
	/// <returns>Trimmed language token or <see langword="null"/>.</returns>
	private static string? NormalizeLanguage(string? language)
	{
		return string.IsNullOrWhiteSpace(language)
			? null
			: language.Trim();
	}

	/// <summary>
	/// Finds groups whose normalized canonical/alias keys intersect with incoming normalized keys.
	/// </summary>
	/// <param name="document">Manga-equivalents document.</param>
	/// <param name="incomingTitles">Incoming deduped titles.</param>
	/// <param name="normalizer">Title normalizer.</param>
	/// <returns>Matched group index set.</returns>
	private static HashSet<int> FindMatchedGroupIndices(
		MangaEquivalentsDocument document,
		IReadOnlyList<IncomingTitleCandidate> incomingTitles,
		ITitleComparisonNormalizer normalizer)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(incomingTitles);
		ArgumentNullException.ThrowIfNull(normalizer);

		HashSet<string> incomingKeys = incomingTitles
			.Select(static candidate => candidate.NormalizedKey)
			.ToHashSet(StringComparer.Ordinal);
		HashSet<int> matchedGroupIndices = [];
		if (document.Groups is null)
		{
			return matchedGroupIndices;
		}

		List<MangaEquivalentGroup> groups = document.Groups;
		for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
		{
			MangaEquivalentGroup group = groups[groupIndex];
			if (group is null)
			{
				continue;
			}

			if (TryGroupMatchesIncoming(group, incomingKeys, normalizer))
			{
				matchedGroupIndices.Add(groupIndex);
			}
		}

		return matchedGroupIndices;
	}

	/// <summary>
	/// Determines whether one group matches incoming normalized title keys.
	/// </summary>
	/// <param name="group">Group to inspect.</param>
	/// <param name="incomingKeys">Incoming normalized-key set.</param>
	/// <param name="normalizer">Title normalizer.</param>
	/// <returns><see langword="true"/> when one canonical/alias key intersects incoming keys; otherwise <see langword="false"/>.</returns>
	private static bool TryGroupMatchesIncoming(
		MangaEquivalentGroup group,
		IReadOnlySet<string> incomingKeys,
		ITitleComparisonNormalizer normalizer)
	{
		ArgumentNullException.ThrowIfNull(group);
		ArgumentNullException.ThrowIfNull(incomingKeys);
		ArgumentNullException.ThrowIfNull(normalizer);

		if (!string.IsNullOrWhiteSpace(group.Canonical))
		{
			string canonicalKey = normalizer.NormalizeTitleKey(group.Canonical);
			if (!string.IsNullOrWhiteSpace(canonicalKey) && incomingKeys.Contains(canonicalKey))
			{
				return true;
			}
		}

		if (group.Aliases is null)
		{
			return false;
		}

		for (int index = 0; index < group.Aliases.Count; index++)
		{
			string? alias = group.Aliases[index];
			if (string.IsNullOrWhiteSpace(alias))
			{
				continue;
			}

			string aliasKey = normalizer.NormalizeTitleKey(alias);
			if (!string.IsNullOrWhiteSpace(aliasKey) && incomingKeys.Contains(aliasKey))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Creates one new manga-equivalents group and appends it to the document.
	/// </summary>
	/// <param name="document">Document to mutate.</param>
	/// <param name="incomingTitles">Incoming deduped titles.</param>
	/// <param name="normalizer">Title normalizer.</param>
	/// <param name="canonicalTitle">Preselected canonical title value.</param>
	/// <returns>Created group index and alias-add count.</returns>
	private static (int GroupIndex, int AddedAliasCount) CreateNewGroup(
		MangaEquivalentsDocument document,
		IReadOnlyList<IncomingTitleCandidate> incomingTitles,
		ITitleComparisonNormalizer normalizer,
		string canonicalTitle)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(incomingTitles);
		ArgumentNullException.ThrowIfNull(normalizer);
		ArgumentException.ThrowIfNullOrWhiteSpace(canonicalTitle);

		if (document.Groups is null)
		{
			throw new InvalidOperationException("Manga equivalents document requires a non-null groups list.");
		}

		List<MangaEquivalentGroup> groups = document.Groups;
		string canonicalNormalizedKey = normalizer.NormalizeTitleKey(canonicalTitle);

		List<string> aliases = [];
		for (int index = 0; index < incomingTitles.Count; index++)
		{
			IncomingTitleCandidate incomingTitle = incomingTitles[index];
			if (string.Equals(incomingTitle.NormalizedKey, canonicalNormalizedKey, StringComparison.Ordinal))
			{
				continue;
			}

			aliases.Add(incomingTitle.Title);
		}

		groups.Add(
			new MangaEquivalentGroup
			{
				Canonical = canonicalTitle,
				Aliases = aliases
			});

		int createdIndex = groups.Count - 1;
		return (createdIndex, aliases.Count);
	}

	/// <summary>
	/// Appends missing aliases to one existing group.
	/// </summary>
	/// <param name="document">Document to mutate.</param>
	/// <param name="groupIndex">Target group index.</param>
	/// <param name="incomingTitles">Incoming deduped titles.</param>
	/// <param name="normalizer">Title normalizer.</param>
	/// <returns>Number of aliases appended.</returns>
	private static int AppendMissingAliases(
		MangaEquivalentsDocument document,
		int groupIndex,
		IReadOnlyList<IncomingTitleCandidate> incomingTitles,
		ITitleComparisonNormalizer normalizer)
	{
		ArgumentNullException.ThrowIfNull(document);
		ArgumentNullException.ThrowIfNull(incomingTitles);
		ArgumentNullException.ThrowIfNull(normalizer);

		if (document.Groups is null)
		{
			throw new InvalidOperationException("Manga equivalents document requires a non-null groups list.");
		}

		List<MangaEquivalentGroup> groups = document.Groups;
		MangaEquivalentGroup group = groups[groupIndex];
		if (group.Aliases is null)
		{
			throw new InvalidOperationException($"Manga equivalents group at index {groupIndex} has a null aliases list.");
		}

		List<string> aliases = group.Aliases;

		HashSet<string> existingKeys = new(StringComparer.Ordinal);
		if (!string.IsNullOrWhiteSpace(group.Canonical))
		{
			string canonicalKey = normalizer.NormalizeTitleKey(group.Canonical);
			if (!string.IsNullOrWhiteSpace(canonicalKey))
			{
				existingKeys.Add(canonicalKey);
			}
		}

		for (int aliasIndex = 0; aliasIndex < aliases.Count; aliasIndex++)
		{
			string? alias = aliases[aliasIndex];
			if (string.IsNullOrWhiteSpace(alias))
			{
				continue;
			}

			string aliasKey = normalizer.NormalizeTitleKey(alias);
			if (!string.IsNullOrWhiteSpace(aliasKey))
			{
				existingKeys.Add(aliasKey);
			}
		}

		int addedAliasCount = 0;
		for (int index = 0; index < incomingTitles.Count; index++)
		{
			IncomingTitleCandidate candidate = incomingTitles[index];
			if (!existingKeys.Add(candidate.NormalizedKey))
			{
				continue;
			}

			aliases.Add(candidate.Title);
			addedAliasCount++;
		}

		return addedAliasCount;
	}

	/// <summary>
	/// Clones one manga-equivalents document to isolate in-memory mutations from parsed source objects.
	/// </summary>
	/// <param name="document">Source document.</param>
	/// <returns>Deep-cloned document.</returns>
	private static MangaEquivalentsDocument CloneDocument(MangaEquivalentsDocument document)
	{
		ArgumentNullException.ThrowIfNull(document);

		List<MangaEquivalentGroup> groups = [];
		if (document.Groups is not null)
		{
			groups = new List<MangaEquivalentGroup>(document.Groups.Count);
			for (int index = 0; index < document.Groups.Count; index++)
			{
				MangaEquivalentGroup sourceGroup = document.Groups[index];
				groups.Add(
					new MangaEquivalentGroup
					{
						Canonical = sourceGroup.Canonical,
						Aliases = sourceGroup.Aliases is null
							? null
							: sourceGroup.Aliases.ToList()
					});
			}
		}

		return new MangaEquivalentsDocument
		{
			Groups = groups
		};
	}

	/// <summary>
	/// Represents one deduped incoming title candidate.
	/// </summary>
	/// <param name="Title">Display title text.</param>
	/// <param name="NormalizedKey">Normalized comparison key.</param>
	/// <param name="Language">Optional language token.</param>
	/// <param name="IsAlternate">Whether candidate originated from alternate-title input.</param>
	private sealed record IncomingTitleCandidate(
		string Title,
		string NormalizedKey,
		string? Language,
		bool IsAlternate);
}
