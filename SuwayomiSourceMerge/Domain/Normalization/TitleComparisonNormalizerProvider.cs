using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Provides shared cached comparison normalizers for matcher-aware and matcherless call sites.
/// </summary>
/// <remarks>
/// Matcher-aware normalizers are shared by matcher reference identity and keep strong references for
/// process lifetime. Consumers should supply deterministic matcher implementations so cached results
/// remain stable for identical inputs.
/// </remarks>
internal static class TitleComparisonNormalizerProvider
{
	/// <summary>
	/// Shared cached normalizer for call sites that do not use scene-tag matching.
	/// </summary>
	private static readonly ITitleComparisonNormalizer _sharedWithoutMatcher = new CachedTitleComparisonNormalizer();

	/// <summary>
	/// Shared cached normalizers keyed by matcher reference identity.
	/// </summary>
	private static readonly ConcurrentDictionary<ISceneTagMatcher, ITitleComparisonNormalizer> _sharedByMatcher =
		new(SceneTagMatcherReferenceComparer.Instance);

	/// <summary>
	/// Gets the shared cached normalizer for one matcher context.
	/// </summary>
	/// <param name="sceneTagMatcher">Optional matcher used for scene-tag-aware normalization.</param>
	/// <returns>Shared cached normalizer for the requested matcher context.</returns>
	/// <remarks>
	/// For non-null matchers, the returned normalizer is keyed by matcher reference identity and
	/// caches first-computed results by raw input value.
	/// </remarks>
	public static ITitleComparisonNormalizer Get(ISceneTagMatcher? sceneTagMatcher)
	{
		if (sceneTagMatcher is null)
		{
			return _sharedWithoutMatcher;
		}

		return _sharedByMatcher.GetOrAdd(
			sceneTagMatcher,
			static matcher => new CachedTitleComparisonNormalizer(matcher));
	}

	/// <summary>
	/// Compares scene-tag matcher instances by reference identity.
	/// </summary>
	private sealed class SceneTagMatcherReferenceComparer : IEqualityComparer<ISceneTagMatcher>
	{
		/// <summary>
		/// Gets the singleton comparer instance.
		/// </summary>
		public static SceneTagMatcherReferenceComparer Instance { get; } = new();

		/// <summary>
		/// Determines whether two matcher references are the same object instance.
		/// </summary>
		/// <param name="x">First matcher reference.</param>
		/// <param name="y">Second matcher reference.</param>
		/// <returns><see langword="true"/> when the matcher references point to the same instance.</returns>
		public bool Equals(ISceneTagMatcher? x, ISceneTagMatcher? y)
		{
			return ReferenceEquals(x, y);
		}

		/// <summary>
		/// Returns a reference-identity hash code for one matcher reference.
		/// </summary>
		/// <param name="obj">Matcher reference.</param>
		/// <returns>Reference-identity hash code.</returns>
		public int GetHashCode(ISceneTagMatcher obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}
}
