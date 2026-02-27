using System.Collections.Frozen;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves canonical manga titles from configuration-defined canonical and alias mappings.
/// </summary>
/// <remarks>
/// The constructor eagerly validates and indexes mappings so lookup operations remain deterministic and
/// efficient for runtime call sites.
/// </remarks>
internal sealed class MangaEquivalenceService : IMangaEquivalenceService
{
	/// <summary>
	/// Lookup of normalized title keys to canonical display titles.
	/// </summary>
	private readonly IReadOnlyDictionary<string, string> _canonicalByNormalizedTitle;

	/// <summary>
	/// Lookup of normalized title keys to deterministic equivalent-title group entries.
	/// </summary>
	private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _equivalentTitlesByNormalizedTitle;

	/// <summary>
	/// Shared cached normalizer used for title-key lookups.
	/// </summary>
	private readonly ITitleComparisonNormalizer _titleComparisonNormalizer;

	/// <summary>
	/// Initializes a new instance of the <see cref="MangaEquivalenceService"/> class.
	/// </summary>
	/// <param name="document">Parsed and validated manga-equivalents document.</param>
	/// <param name="sceneTagMatcher">
	/// Optional matcher used by title normalization when scene-tag-aware key comparison is required.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
	/// <exception cref="InvalidOperationException">
	/// Thrown when document content is incomplete, normalizes to empty keys, or contains conflicting mappings.
	/// </exception>
	public MangaEquivalenceService(MangaEquivalentsDocument document, ISceneTagMatcher? sceneTagMatcher = null)
	{
		ArgumentNullException.ThrowIfNull(document);

		_titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);
		(_canonicalByNormalizedTitle, _equivalentTitlesByNormalizedTitle) = BuildLookups(document, _titleComparisonNormalizer);
	}

	/// <inheritdoc />
	public bool TryResolveCanonicalTitle(string inputTitle, out string canonicalTitle)
	{
		ArgumentNullException.ThrowIfNull(inputTitle);

		string normalizedKey = _titleComparisonNormalizer.NormalizeTitleKey(inputTitle);
		if (string.IsNullOrEmpty(normalizedKey))
		{
			canonicalTitle = string.Empty;
			return false;
		}

		if (_canonicalByNormalizedTitle.TryGetValue(normalizedKey, out string? foundCanonicalTitle))
		{
			canonicalTitle = foundCanonicalTitle;
			return true;
		}

		canonicalTitle = string.Empty;
		return false;
	}

	/// <inheritdoc />
	public string ResolveCanonicalOrInput(string inputTitle)
	{
		ArgumentNullException.ThrowIfNull(inputTitle);

		return TryResolveCanonicalTitle(inputTitle, out string canonicalTitle)
			? canonicalTitle
			: inputTitle;
	}

	/// <summary>
	/// Attempts to resolve one full equivalent-title group for the provided input title.
	/// </summary>
	/// <param name="inputTitle">Input title used to resolve one equivalence group.</param>
	/// <param name="equivalentTitles">
	/// Equivalent-title entries in deterministic group order (canonical first, then aliases) when found;
	/// otherwise an empty list.
	/// </param>
	/// <returns><see langword="true"/> when one matching equivalence group is found; otherwise <see langword="false"/>.</returns>
	public bool TryGetEquivalentTitles(string inputTitle, out IReadOnlyList<string> equivalentTitles)
	{
		ArgumentNullException.ThrowIfNull(inputTitle);

		string normalizedKey = _titleComparisonNormalizer.NormalizeTitleKey(inputTitle);
		if (string.IsNullOrWhiteSpace(normalizedKey))
		{
			equivalentTitles = [];
			return false;
		}

		if (_equivalentTitlesByNormalizedTitle.TryGetValue(normalizedKey, out IReadOnlyList<string>? foundEquivalentTitles))
		{
			equivalentTitles = foundEquivalentTitles;
			return true;
		}

		equivalentTitles = [];
		return false;
	}

	/// <summary>
	/// Builds the normalized canonical mapping lookup from document groups.
	/// </summary>
	/// <param name="document">Document to index.</param>
	/// <param name="titleComparisonNormalizer">Cached normalizer used to derive title comparison keys.</param>
	/// <returns>Immutable lookup from normalized title key to canonical title.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when document content is malformed, normalizes to empty keys, or maps one key to different canonicals.
	/// </exception>
	private static (IReadOnlyDictionary<string, string> CanonicalLookup, IReadOnlyDictionary<string, IReadOnlyList<string>> EquivalentLookup) BuildLookups(
		MangaEquivalentsDocument document,
		ITitleComparisonNormalizer titleComparisonNormalizer)
	{
		if (document.Groups is null)
		{
			throw new InvalidOperationException("Manga equivalents document requires a non-null groups list.");
		}

		Dictionary<string, string> canonicalLookup = new(StringComparer.Ordinal);
		Dictionary<string, IReadOnlyList<string>> equivalentLookup = new(StringComparer.Ordinal);

		for (int groupIndex = 0; groupIndex < document.Groups.Count; groupIndex++)
		{
			MangaEquivalentGroup group = document.Groups[groupIndex];

			if (string.IsNullOrWhiteSpace(group.Canonical))
			{
				throw new InvalidOperationException(
					$"Manga equivalents group at index {groupIndex} is missing a canonical title.");
			}

			if (group.Aliases is null)
			{
				throw new InvalidOperationException(
					$"Manga equivalents group at index {groupIndex} is missing an aliases list.");
			}

			string canonical = group.Canonical.Trim();
			List<string> equivalentTitles = [canonical];
			HashSet<string> groupNormalizedKeys = new(StringComparer.Ordinal);
			string normalizedCanonicalKey = titleComparisonNormalizer.NormalizeTitleKey(canonical);
			RegisterMapping(
				canonicalLookup,
				normalizedCanonicalKey,
				canonical,
				$"groups[{groupIndex}].canonical");
			groupNormalizedKeys.Add(normalizedCanonicalKey);

			for (int aliasIndex = 0; aliasIndex < group.Aliases.Count; aliasIndex++)
			{
				string? alias = group.Aliases[aliasIndex];
				if (string.IsNullOrWhiteSpace(alias))
				{
					throw new InvalidOperationException(
						$"Manga equivalents alias at groups[{groupIndex}].aliases[{aliasIndex}] is empty.");
				}

				RegisterMapping(
					canonicalLookup,
					titleComparisonNormalizer.NormalizeTitleKey(alias),
					canonical,
					$"groups[{groupIndex}].aliases[{aliasIndex}]");

				equivalentTitles.Add(alias.Trim());
				groupNormalizedKeys.Add(titleComparisonNormalizer.NormalizeTitleKey(alias));
			}

			string[] materializedEquivalentTitles = equivalentTitles.ToArray();
			foreach (string normalizedKey in groupNormalizedKeys)
			{
				RegisterEquivalentTitles(
					equivalentLookup,
					normalizedKey,
					materializedEquivalentTitles);
			}
		}

		return (
			canonicalLookup.ToFrozenDictionary(StringComparer.Ordinal),
			equivalentLookup.ToFrozenDictionary(StringComparer.Ordinal));
	}

	/// <summary>
	/// Registers one normalized mapping and validates conflicts.
	/// </summary>
	/// <param name="lookup">Lookup being populated.</param>
	/// <param name="normalizedKey">Normalized key for canonical/alias text.</param>
	/// <param name="canonical">Canonical value associated with the key.</param>
	/// <param name="path">Human-readable path for diagnostics.</param>
	/// <exception cref="InvalidOperationException">
	/// Thrown when the key is empty after normalization or conflicts with a different canonical title.
	/// </exception>
	private static void RegisterMapping(
		IDictionary<string, string> lookup,
		string normalizedKey,
		string canonical,
		string path)
	{
		if (string.IsNullOrEmpty(normalizedKey))
		{
			throw new InvalidOperationException(
				$"Manga equivalents value at {path} becomes empty after normalization.");
		}

		if (!lookup.TryGetValue(normalizedKey, out string? existingCanonical))
		{
			lookup[normalizedKey] = canonical;
			return;
		}

		if (!string.Equals(existingCanonical, canonical, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Normalized manga key '{normalizedKey}' maps to conflicting canonical titles '{existingCanonical}' and '{canonical}'.");
		}
	}

	/// <summary>
	/// Registers one equivalent-title group mapping for a normalized key and validates deterministic consistency.
	/// </summary>
	/// <param name="lookup">Equivalent-title lookup being populated.</param>
	/// <param name="normalizedKey">Normalized key for one canonical/alias title entry.</param>
	/// <param name="equivalentTitles">Equivalent-title group entries.</param>
	/// <exception cref="InvalidOperationException">Thrown when one key maps to non-deterministic equivalent-title groups.</exception>
	private static void RegisterEquivalentTitles(
		IDictionary<string, IReadOnlyList<string>> lookup,
		string normalizedKey,
		IReadOnlyList<string> equivalentTitles)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(normalizedKey);
		ArgumentNullException.ThrowIfNull(equivalentTitles);

		if (!lookup.TryGetValue(normalizedKey, out IReadOnlyList<string>? existingEquivalentTitles))
		{
			lookup[normalizedKey] = equivalentTitles;
			return;
		}

		if (!existingEquivalentTitles.SequenceEqual(equivalentTitles, StringComparer.Ordinal))
		{
			throw new InvalidOperationException(
				$"Normalized manga key '{normalizedKey}' maps to conflicting equivalent-title group entries.");
		}
	}
}
