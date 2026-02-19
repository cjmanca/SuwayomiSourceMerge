using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;

namespace SuwayomiSourceMerge.Application.Hosting;

/// <summary>
/// Provides diagnostics helpers for scene-tag configuration drift against recommended defaults.
/// </summary>
internal static class SceneTagConfigurationAdvisor
{
	/// <summary>
	/// Returns recommended default scene tags that are missing from the configured matcher set.
	/// </summary>
	/// <param name="sceneTagMatcher">Matcher built from configured <c>scene_tags.yml</c> tags.</param>
	/// <returns>Missing recommended tags in deterministic default-order traversal.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="sceneTagMatcher"/> is <see langword="null"/>.</exception>
	public static IReadOnlyList<string> GetMissingRecommendedTags(ISceneTagMatcher sceneTagMatcher)
	{
		ArgumentNullException.ThrowIfNull(sceneTagMatcher);

		IReadOnlyList<string> recommendedTags = SceneTagsDocumentDefaults.Create().Tags ?? [];
		List<string> missingTags = [];
		for (int index = 0; index < recommendedTags.Count; index++)
		{
			string recommendedTag = recommendedTags[index];
			if (!sceneTagMatcher.IsMatch(recommendedTag))
			{
				missingTags.Add(recommendedTag);
			}
		}

		return missingTags;
	}
}
