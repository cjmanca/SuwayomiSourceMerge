using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves canonical manga titles from configuration-defined canonical and alias mappings.
/// </summary>
/// <remarks>
/// The constructor eagerly validates and indexes mappings so lookup operations remain deterministic and
/// allocation-free for runtime call sites.
/// </remarks>
internal sealed class MangaEquivalenceService : IMangaEquivalenceService
{
	/// <summary>
	/// Lookup of normalized title keys to canonical display titles.
	/// </summary>
	private readonly IReadOnlyDictionary<string, string> _canonicalByNormalizedTitle;

	/// <summary>
	/// Optional matcher used by normalization for trailing scene-tag suffix stripping.
	/// </summary>
	private readonly ISceneTagMatcher? _sceneTagMatcher;

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

		_sceneTagMatcher = sceneTagMatcher;
		_canonicalByNormalizedTitle = BuildLookup(document, sceneTagMatcher);
	}

	/// <inheritdoc />
	public bool TryResolveCanonicalTitle(string inputTitle, out string canonicalTitle)
	{
		ArgumentNullException.ThrowIfNull(inputTitle);

		string normalizedKey = TitleKeyNormalizer.NormalizeTitleKey(inputTitle, _sceneTagMatcher);
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
	/// Builds the normalized canonical mapping lookup from document groups.
	/// </summary>
	/// <param name="document">Document to index.</param>
	/// <param name="sceneTagMatcher">Optional matcher used during title-key normalization.</param>
	/// <returns>Immutable lookup from normalized title key to canonical title.</returns>
	/// <exception cref="InvalidOperationException">
	/// Thrown when document content is malformed, normalizes to empty keys, or maps one key to different canonicals.
	/// </exception>
	private static IReadOnlyDictionary<string, string> BuildLookup(
		MangaEquivalentsDocument document,
		ISceneTagMatcher? sceneTagMatcher)
	{
		if (document.Groups is null)
		{
			throw new InvalidOperationException("Manga equivalents document requires a non-null groups list.");
		}

		Dictionary<string, string> lookup = new(StringComparer.Ordinal);

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
			RegisterMapping(
				lookup,
				TitleKeyNormalizer.NormalizeTitleKey(canonical, sceneTagMatcher),
				canonical,
				$"groups[{groupIndex}].canonical");

			for (int aliasIndex = 0; aliasIndex < group.Aliases.Count; aliasIndex++)
			{
				string? alias = group.Aliases[aliasIndex];
				if (string.IsNullOrWhiteSpace(alias))
				{
					throw new InvalidOperationException(
						$"Manga equivalents alias at groups[{groupIndex}].aliases[{aliasIndex}] is empty.");
				}

				RegisterMapping(
					lookup,
					TitleKeyNormalizer.NormalizeTitleKey(alias, sceneTagMatcher),
					canonical,
					$"groups[{groupIndex}].aliases[{aliasIndex}]");
			}
		}

		return lookup;
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
}
