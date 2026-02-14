using System.Collections.Concurrent;

namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Provides process-lifetime cached title and token normalization for comparison call sites.
/// </summary>
/// <remarks>
/// Cache keys are raw input values and use ordinal string comparison so repeat lookups avoid
/// recomputing normalization transforms. When scene-tag matching is enabled, cached title keys
/// assume matcher results are deterministic for identical inputs throughout process lifetime.
/// </remarks>
internal sealed class CachedTitleComparisonNormalizer : ITitleComparisonNormalizer
{
	/// <summary>
	/// Optional matcher used by title-key normalization for scene-tag suffix stripping.
	/// </summary>
	private readonly ISceneTagMatcher? _sceneTagMatcher;

	/// <summary>
	/// Cache of normalized title keys indexed by raw input title.
	/// </summary>
	private readonly ConcurrentDictionary<string, string> _titleKeyCache;

	/// <summary>
	/// Cache of normalized token keys indexed by raw input token.
	/// </summary>
	private readonly ConcurrentDictionary<string, string> _tokenKeyCache;

	/// <summary>
	/// Initializes a new instance of the <see cref="CachedTitleComparisonNormalizer"/> class.
	/// </summary>
	/// <param name="sceneTagMatcher">
	/// Optional matcher used to strip trailing scene-tag suffixes during title-key normalization.
	/// Implementations are expected to return stable results for identical candidate values.
	/// </param>
	public CachedTitleComparisonNormalizer(ISceneTagMatcher? sceneTagMatcher = null)
	{
		_sceneTagMatcher = sceneTagMatcher;
		_titleKeyCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
		_tokenKeyCache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
	}

	/// <inheritdoc />
	public string NormalizeTitleKey(string input)
	{
		ArgumentNullException.ThrowIfNull(input);

		return _titleKeyCache.GetOrAdd(input, NormalizeTitleKeyUncached);
	}

	/// <inheritdoc />
	public string NormalizeTokenKey(string input)
	{
		ArgumentNullException.ThrowIfNull(input);

		return _tokenKeyCache.GetOrAdd(input, NormalizeTokenKeyUncached);
	}

	/// <summary>
	/// Computes one uncached normalized title key for storage in the title cache.
	/// </summary>
	/// <param name="input">Raw title value.</param>
	/// <returns>Normalized title key.</returns>
	private string NormalizeTitleKeyUncached(string input)
	{
		return TitleKeyNormalizer.NormalizeTitleKey(input, _sceneTagMatcher);
	}

	/// <summary>
	/// Computes one uncached normalized token key for storage in the token cache.
	/// </summary>
	/// <param name="input">Raw token value.</param>
	/// <returns>Normalized token key.</returns>
	private static string NormalizeTokenKeyUncached(string input)
	{
		return TitleKeyNormalizer.NormalizeTokenKey(input);
	}
}
