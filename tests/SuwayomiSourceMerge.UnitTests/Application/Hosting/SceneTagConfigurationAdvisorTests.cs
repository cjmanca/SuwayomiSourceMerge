namespace SuwayomiSourceMerge.UnitTests.Application.Hosting;

using SuwayomiSourceMerge.Application.Hosting;
using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Verifies recommended scene-tag drift diagnostics.
/// </summary>
public sealed class SceneTagConfigurationAdvisorTests
{
	/// <summary>
	/// Verifies no recommended tags are reported missing when matcher includes default tags.
	/// </summary>
	[Fact]
	public void GetMissingRecommendedTags_ShouldReturnEmpty_WhenMatcherContainsDefaultTags()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(SceneTagsDocumentDefaults.Create().Tags ?? []);

		IReadOnlyList<string> missingTags = SceneTagConfigurationAdvisor.GetMissingRecommendedTags(matcher);

		Assert.Empty(missingTags);
	}

	/// <summary>
	/// Verifies punctuation-equivalent configured tags satisfy recommended defaults.
	/// </summary>
	[Fact]
	public void GetMissingRecommendedTags_ShouldNotReportEquivalentRecommendedTag_WhenPunctuationVariantConfigured()
	{
		ISceneTagMatcher matcher = new SceneTagMatcher(["official", "tapas-official"]);

		IReadOnlyList<string> missingTags = SceneTagConfigurationAdvisor.GetMissingRecommendedTags(matcher);

		Assert.DoesNotContain("tapas official", missingTags);
		Assert.Contains("asura scan", missingTags);
	}

	/// <summary>
	/// Verifies guard clauses reject null matcher input.
	/// </summary>
	[Fact]
	public void GetMissingRecommendedTags_ShouldThrow_WhenMatcherIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => SceneTagConfigurationAdvisor.GetMissingRecommendedTags(null!));
	}
}
