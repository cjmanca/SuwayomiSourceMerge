namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Classifies scene-tag comparison keys by their matching semantics.
/// </summary>
internal enum SceneTagMatchKeyKind
{
	/// <summary>
	/// Token key based on normalized alphanumeric words.
	/// </summary>
	Token = 0,

	/// <summary>
	/// Exact punctuation sequence key.
	/// </summary>
	Punctuation = 1
}
