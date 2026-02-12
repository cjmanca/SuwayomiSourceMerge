namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Matches candidate scene-tag phrases against configured tags from <c>scene_tags.yml</c>.
/// </summary>
/// <remarks>
/// Matching is deterministic and data-driven. Tag values are converted into shared match keys so
/// punctuation-only tags can use exact-sequence matching while text tags remain normalization-based.
/// </remarks>
public sealed class SceneTagMatcher : ISceneTagMatcher
{
	/// <summary>
	/// Scene-tag lookup keyed by canonical matcher-equivalent keys.
	/// </summary>
	private readonly HashSet<SceneTagMatchKey> _matchKeys;

	/// <summary>
	/// Initializes a new instance of the <see cref="SceneTagMatcher"/> class.
	/// </summary>
	/// <param name="configuredTags">Configured scene-tag values sourced from <c>scene_tags.yml</c>.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="configuredTags"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">Thrown when any configured tag is null, empty, or whitespace.</exception>
	public SceneTagMatcher(IEnumerable<string> configuredTags)
	{
		ArgumentNullException.ThrowIfNull(configuredTags);

		_matchKeys = [];

		int index = 0;
		foreach (string? configuredTag in configuredTags)
		{
			if (string.IsNullOrWhiteSpace(configuredTag))
			{
				throw new ArgumentException(
					$"Configured scene tag at index {index} must not be null, empty, or whitespace.",
					nameof(configuredTags));
			}

			if (!SceneTagMatchKeyBuilder.TryCreate(configuredTag, out SceneTagMatchKey matchKey))
			{
				throw new ArgumentException(
					$"Configured scene tag at index {index} could not be converted into a match key.",
					nameof(configuredTags));
			}

			_matchKeys.Add(matchKey);
			index++;
		}
	}

	/// <inheritdoc />
	public bool IsMatch(string candidate)
	{
		ArgumentNullException.ThrowIfNull(candidate);

		if (string.IsNullOrWhiteSpace(candidate))
		{
			return false;
		}

		if (!SceneTagMatchKeyBuilder.TryCreate(candidate, out SceneTagMatchKey matchKey))
		{
			return false;
		}

		return _matchKeys.Contains(matchKey);
	}
}
