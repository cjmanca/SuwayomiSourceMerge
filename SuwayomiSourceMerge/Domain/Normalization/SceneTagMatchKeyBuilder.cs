namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Builds deterministic scene-tag comparison keys for both matcher lookup and configuration duplicate checks.
/// </summary>
internal static class SceneTagMatchKeyBuilder
{
	/// <summary>
	/// Attempts to create a scene-tag match key from raw text.
	/// </summary>
	/// <param name="input">Raw scene-tag or candidate phrase.</param>
	/// <param name="matchKey">Resulting key when creation succeeds.</param>
	/// <returns>
	/// <see langword="true"/> when a key can be produced; otherwise, <see langword="false"/> when input
	/// is empty after trimming.
	/// </returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="input"/> is <see langword="null"/>.</exception>
	public static bool TryCreate(string input, out SceneTagMatchKey matchKey)
	{
		ArgumentNullException.ThrowIfNull(input);

		string trimmed = input.Trim();
		if (trimmed.Length == 0)
		{
			matchKey = default;
			return false;
		}

		string tokenKey = ComparisonTextNormalizer.NormalizeTokenKey(trimmed);
		if (!string.IsNullOrEmpty(tokenKey))
		{
			matchKey = new SceneTagMatchKey(SceneTagMatchKeyKind.Token, tokenKey);
			return true;
		}

		// Punctuation/symbol-only tags use exact-sequence matching semantics.
		matchKey = new SceneTagMatchKey(SceneTagMatchKeyKind.Punctuation, trimmed);
		return true;
	}
}
