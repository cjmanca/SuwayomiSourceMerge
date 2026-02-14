namespace SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Defines canonical scene-tag phrase matching behavior for normalized title processing.
/// </summary>
/// <remarks>
/// Implementations are expected to be deterministic and side-effect free for identical candidate
/// inputs during one process lifetime. Cached title-comparison pipelines rely on this contract
/// when reusing matcher-aware normalization results.
/// </remarks>
public interface ISceneTagMatcher
{
	/// <summary>
	/// Determines whether a candidate phrase matches one configured scene tag.
	/// </summary>
	/// <param name="candidate">Candidate suffix phrase to compare against configured scene tags.</param>
	/// <returns>
	/// <see langword="true"/> when <paramref name="candidate"/> matches a configured scene tag after
	/// deterministic normalization; otherwise, <see langword="false"/>.
	/// </returns>
	bool IsMatch(string candidate);
}
