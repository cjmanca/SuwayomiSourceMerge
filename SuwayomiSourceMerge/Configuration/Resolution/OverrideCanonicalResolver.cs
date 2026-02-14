using System.Collections.Frozen;

using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Resolution;

/// <summary>
/// Resolves canonical override titles from existing override directory names.
/// </summary>
/// <remarks>
/// Lookup collisions are resolved by first-seen order from the constructor input sequence.
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
	/// Initializes a new instance of the <see cref="OverrideCanonicalResolver"/> class.
	/// </summary>
	/// <param name="existingOverrideTitles">
	/// Existing override title directory names in caller-preferred order.
	/// </param>
	/// <param name="sceneTagMatcher">
	/// Optional matcher used by title normalization when scene-tag-aware key comparison is required.
	/// </param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="existingOverrideTitles"/> is <see langword="null"/>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// Thrown when any title entry is null/whitespace or normalizes to an empty key.
	/// </exception>
	public OverrideCanonicalResolver(
		IEnumerable<string> existingOverrideTitles,
		ISceneTagMatcher? sceneTagMatcher = null)
	{
		ArgumentNullException.ThrowIfNull(existingOverrideTitles);

		_titleComparisonNormalizer = TitleComparisonNormalizerProvider.Get(sceneTagMatcher);
		_overrideTitleByNormalizedKey = BuildLookup(existingOverrideTitles, _titleComparisonNormalizer);
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
	/// <param name="existingOverrideTitles">Titles to index in priority order.</param>
	/// <param name="titleComparisonNormalizer">Cached normalizer used to derive title comparison keys.</param>
	/// <returns>Immutable lookup from normalized keys to exact override titles.</returns>
	/// <exception cref="ArgumentException">
	/// Thrown when any entry is null/whitespace or normalizes to an empty key.
	/// </exception>
	private static IReadOnlyDictionary<string, string> BuildLookup(
		IEnumerable<string> existingOverrideTitles,
		ITitleComparisonNormalizer titleComparisonNormalizer)
	{
		Dictionary<string, string> lookup = new(StringComparer.Ordinal);
		int index = 0;

		foreach (string? title in existingOverrideTitles)
		{
			if (string.IsNullOrWhiteSpace(title))
			{
				throw new ArgumentException(
					$"Override title at index {index} must not be null, empty, or whitespace.",
					nameof(existingOverrideTitles));
			}

			string normalizedKey = titleComparisonNormalizer.NormalizeTitleKey(title);
			if (string.IsNullOrEmpty(normalizedKey))
			{
				throw new ArgumentException(
					$"Override title at index {index} becomes empty after normalization.",
					nameof(existingOverrideTitles));
			}

			lookup.TryAdd(normalizedKey, title);
			index++;
		}

		return lookup.ToFrozenDictionary(StringComparer.Ordinal);
	}
}
