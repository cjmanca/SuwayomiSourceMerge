using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Configuration.Validation;

/// <summary>
/// Produces normalized comparison keys used by configuration validators.
/// </summary>
/// <remarks>
/// This adapter delegates normalization behavior to domain-level primitives so validation call sites keep
/// stable APIs while sharing one canonical normalization implementation.
/// </remarks>
internal static class ValidationKeyNormalizer
{
	/// <summary>
	/// Shared cached normalizer for call sites that do not require scene-tag-aware stripping.
	/// </summary>
	private static readonly ITitleComparisonNormalizer _normalizerWithoutMatcher =
		TitleComparisonNormalizerProvider.Get(sceneTagMatcher: null);

	/// <summary>
	/// Normalizes a title into a compact alphanumeric key for equivalence comparisons.
	/// </summary>
	/// <param name="input">Raw title value.</param>
	/// <returns>
	/// A normalized key with ASCII folding, punctuation removal, leading-article stripping, and
	/// per-word trailing <c>s</c> trimming applied.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string NormalizeTitleKey(string input)
	{
		return _normalizerWithoutMatcher.NormalizeTitleKey(input);
	}

	/// <summary>
	/// Normalizes a title into a compact alphanumeric key for equivalence comparisons using optional
	/// scene-tag suffix stripping.
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
		return TitleComparisonNormalizerProvider
			.Get(sceneTagMatcher)
			.NormalizeTitleKey(input);
	}

	/// <summary>
	/// Normalizes a token-like value while preserving word boundaries.
	/// </summary>
	/// <param name="input">Raw token value.</param>
	/// <returns>A lower-case, ASCII-folded, punctuation-normalized token string.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static string NormalizeTokenKey(string input)
	{
		return _normalizerWithoutMatcher.NormalizeTokenKey(input);
	}
}
