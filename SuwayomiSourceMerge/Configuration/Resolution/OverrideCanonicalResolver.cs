using System.Collections.Frozen;

using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves canonical override titles from existing override directory names.
/// </summary>
/// <remarks>
/// Lookup collisions are resolved by deterministic title arbitration:
/// untagged titles win over tagged-only variants for the same normalized key.
/// </remarks>
internal sealed class OverrideCanonicalResolver : IOverrideCanonicalResolver
{
	/// <summary>
	/// Lookup of normalized title keys to exact override directory titles.
	/// </summary>
	private readonly IReadOnlyDictionary<string, string> _overrideTitleByNormalizedKey;

	/// <summary>
	/// Shared cached normalizer used for title-key lookups.
	/// </summary>
	private readonly ITitleComparisonNormalizer _titleComparisonNormalizer;

	/// <summary>
	/// Advisory list produced while building canonical lookup state.
	/// </summary>
	private readonly IReadOnlyList<OverrideCanonicalAdvisory> _advisories;

	/// <summary>
	/// Initializes a new instance of the <see cref="OverrideCanonicalResolver"/> class.
	/// </summary>
	/// <param name="existingOverrideEntries">
	/// Existing override title catalog entries discovered from override directories.
	/// </param>
	/// <param name="sceneTagMatcher">
	/// Optional matcher used by title normalization when scene-tag-aware key comparison is required.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="existingOverrideEntries"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when any catalog entry is invalid or mismatched for matcher-aware normalization.
	/// </exception>
	public OverrideCanonicalResolver(
		IReadOnlyList<OverrideTitleCatalogEntry> existingOverrideEntries,
		ISceneTagMatcher? sceneTagMatcher = null)
	{
		ArgumentNullException.ThrowIfNull(existingOverrideEntries);

		_titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);
		(_overrideTitleByNormalizedKey, _advisories) = BuildLookup(existingOverrideEntries, _titleComparisonNormalizer);
	}

	/// <summary>
	/// Gets resolver advisories discovered while building canonical lookup state.
	/// </summary>
	public IReadOnlyList<OverrideCanonicalAdvisory> Advisories
	{
		get
		{
			return _advisories;
		}
	}

	/// <inheritdoc />
	public bool TryResolveOverrideCanonical(string inputTitle, out string overrideCanonicalTitle)
	{
		ArgumentNullException.ThrowIfNull(inputTitle);

		string normalizedKey = _titleComparisonNormalizer.NormalizeTitleKey(inputTitle);
		if (string.IsNullOrEmpty(normalizedKey))
		{
			overrideCanonicalTitle = string.Empty;
			return false;
		}

		if (_overrideTitleByNormalizedKey.TryGetValue(normalizedKey, out string? foundOverrideCanonicalTitle))
		{
			overrideCanonicalTitle = foundOverrideCanonicalTitle;
			return true;
		}

		overrideCanonicalTitle = string.Empty;
		return false;
	}

	/// <summary>
	/// Builds a normalized lookup from existing override titles.
	/// </summary>
	/// <param name="existingOverrideEntries">Catalog entries to index.</param>
	/// <param name="titleComparisonNormalizer">Cached normalizer used to derive title comparison keys.</param>
	/// <returns>Immutable lookup and any advisories produced by deterministic title arbitration.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when any entry is null, malformed, or mismatched for matcher-aware normalization.
	/// </exception>
	private static (IReadOnlyDictionary<string, string> Lookup, IReadOnlyList<OverrideCanonicalAdvisory> Advisories) BuildLookup(
		IReadOnlyList<OverrideTitleCatalogEntry> existingOverrideEntries,
		ITitleComparisonNormalizer titleComparisonNormalizer)
	{
		ArgumentNullException.ThrowIfNull(existingOverrideEntries);
		ArgumentNullException.ThrowIfNull(titleComparisonNormalizer);

		Dictionary<string, List<OverrideTitleCatalogEntry>> entriesByNormalizedKey = new(StringComparer.Ordinal);
		for (int index = 0; index < existingOverrideEntries.Count; index++)
		{
			OverrideTitleCatalogEntry? entry = existingOverrideEntries[index];
			if (entry is null)
			{
				throw new ArgumentException(
					$"Override title catalog entries must not contain null items. Null item at index {index}.",
					nameof(existingOverrideEntries));
			}

			if (string.IsNullOrWhiteSpace(entry.NormalizedKey))
			{
				throw new ArgumentException(
					$"Override title catalog entry at index {index} has an empty normalized key.",
					nameof(existingOverrideEntries));
			}

			string normalizedFromTitle = titleComparisonNormalizer.NormalizeTitleKey(entry.Title);
			if (!string.Equals(entry.NormalizedKey, normalizedFromTitle, StringComparison.Ordinal))
			{
				throw new ArgumentException(
					$"Override title catalog entry at index {index} has a normalized key that does not match title normalization.",
					nameof(existingOverrideEntries));
			}

			if (!entriesByNormalizedKey.TryGetValue(entry.NormalizedKey, out List<OverrideTitleCatalogEntry>? bucket))
			{
				bucket = [];
				entriesByNormalizedKey.Add(entry.NormalizedKey, bucket);
			}

			bucket.Add(entry);
		}

		Dictionary<string, string> lookup = new(StringComparer.Ordinal);
		List<OverrideCanonicalAdvisory> advisories = [];

		foreach ((string normalizedKey, List<OverrideTitleCatalogEntry> candidates) in entriesByNormalizedKey)
		{
			if (candidates.Count == 0)
			{
				continue;
			}

			OverrideTitleCatalogEntry selected = SelectCanonicalCandidate(candidates);
			lookup.Add(normalizedKey, selected.Title);

			// Title and StrippedTitle are both trimmed at OverrideTitleCatalogEntry construction,
			// so Ordinal comparison here is deterministic and whitespace-neutral.
			if (!selected.IsSuffixTagged
				|| string.IsNullOrWhiteSpace(selected.StrippedTitle)
				|| string.Equals(selected.Title, selected.StrippedTitle, StringComparison.Ordinal))
			{
				continue;
			}

			advisories.Add(
				new OverrideCanonicalAdvisory(
					normalizedKey,
					selected.Title,
					selected.DirectoryPath,
					selected.StrippedTitle));
		}

		return (
			lookup.ToFrozenDictionary(StringComparer.Ordinal),
			advisories
				.OrderBy(static advisory => advisory.SelectedTitle, StringComparer.Ordinal)
				.ThenBy(static advisory => advisory.SelectedDirectoryPath, StringComparer.Ordinal)
				.ToArray());
	}

	/// <summary>
	/// Selects one deterministic canonical candidate for a normalized key bucket.
	/// </summary>
	/// <param name="candidates">Candidate entries sharing one normalized key.</param>
	/// <returns>Selected canonical entry.</returns>
	private static OverrideTitleCatalogEntry SelectCanonicalCandidate(IReadOnlyList<OverrideTitleCatalogEntry> candidates)
	{
		ArgumentNullException.ThrowIfNull(candidates);
		if (candidates.Count == 0)
		{
			throw new ArgumentException("Candidate list must contain at least one entry.", nameof(candidates));
		}

		OverrideTitleCatalogEntry[] orderedCandidates = candidates
			.OrderBy(static candidate => candidate.Title, StringComparer.Ordinal)
			.ThenBy(static candidate => candidate.DirectoryPath, StringComparer.Ordinal)
			.ToArray();

		for (int index = 0; index < orderedCandidates.Length; index++)
		{
			OverrideTitleCatalogEntry candidate = orderedCandidates[index];
			if (!candidate.IsSuffixTagged)
			{
				return candidate;
			}
		}

		return orderedCandidates[0];
	}
}
