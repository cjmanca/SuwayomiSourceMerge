namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Defines cached normalization operations used by title and token comparison call sites.
/// </summary>
internal interface ITitleComparisonNormalizer
{
	/// <summary>
	/// Normalizes one title into a compact alphanumeric comparison key.
	/// </summary>
	/// <param name="input">Raw title value.</param>
	/// <returns>Normalized title key for comparison lookups.</returns>
	string NormalizeTitleKey(string input);

	/// <summary>
	/// Normalizes one token-like value while preserving word boundaries.
	/// </summary>
	/// <param name="input">Raw token value.</param>
	/// <returns>Normalized token key for comparison lookups.</returns>
	string NormalizeTokenKey(string input);
}
