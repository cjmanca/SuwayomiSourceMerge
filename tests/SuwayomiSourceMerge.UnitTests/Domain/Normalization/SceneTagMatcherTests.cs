namespace SuwayomiSourceMerge.UnitTests.Domain.Normalization;

using SuwayomiSourceMerge.Domain.Normalization;

/// <summary>
/// Contract tests for <see cref="ISceneTagMatcher"/> implementations.
/// </summary>
public abstract class SceneTagMatcherContractTestsBase
{
	protected abstract ISceneTagMatcher CreateMatcher(params string[] configuredTags);

	[Theory]
	[InlineData("Asura-Scan!")]
	[InlineData("   Team   Argo\t")]
	public void IsMatch_ShouldReturnTrue_ForNormalizedEquivalents(string candidate)
	{
		ISceneTagMatcher matcher = CreateMatcher("asura scan", "team argo");

		bool isMatch = matcher.IsMatch(candidate);

		Assert.True(isMatch);
	}

	[Theory]
	[InlineData("   ")]
	[InlineData("unknown group")]
	public void IsMatch_ShouldReturnFalse_ForWhitespaceOrUnknownCandidates(string candidate)
	{
		ISceneTagMatcher matcher = CreateMatcher("asura scan", "team argo");
		bool isMatch = matcher.IsMatch(candidate);

		Assert.False(isMatch);
	}

	[Fact]
	public void IsMatch_ShouldThrow_WhenCandidateIsNull()
	{
		ISceneTagMatcher matcher = CreateMatcher("official");
		Assert.Throws<ArgumentNullException>(() => matcher.IsMatch(null!));
	}
}

/// <summary>
/// Tests <see cref="SceneTagMatcher"/> constructor and implementation details.
/// </summary>
public sealed class SceneTagMatcherTests : SceneTagMatcherContractTestsBase
{
	protected override ISceneTagMatcher CreateMatcher(params string[] configuredTags)
	{
		return new SceneTagMatcher(configuredTags);
	}

	[Fact]
	public void Constructor_ShouldThrow_WhenConfiguredTagsCollectionIsNull()
	{
		Assert.Throws<ArgumentNullException>(() => new SceneTagMatcher(null!));
	}

	[Theory]
	[MemberData(nameof(GetInvalidConfiguredTagSets))]
	public void Constructor_ShouldThrow_WhenConfiguredTagsContainInvalidValue(string[] configuredTags)
	{
		ArgumentException exception = Assert.Throws<ArgumentException>(() => new SceneTagMatcher(configuredTags));
		Assert.Equal("configuredTags", exception.ParamName);
	}

	[Fact]
	public void Constructor_ShouldAllowDuplicateNormalizedTags_AndStillMatchDeterministically()
	{
		SceneTagMatcher matcher = new(["official", "Official!!!", "official"]);
		Assert.True(matcher.IsMatch("official"));
	}

	[Fact]
	public void IsMatch_ShouldMatchPunctuationOnlyTag_WhenSequenceMatchesExactlyAfterTrim()
	{
		SceneTagMatcher matcher = new(["!!!"]);

		bool isMatch = matcher.IsMatch("  !!!  ");

		Assert.True(isMatch);
	}

	[Fact]
	public void IsMatch_ShouldNotMatchPunctuationOnlyTag_WhenSequenceDiffers()
	{
		SceneTagMatcher matcher = new(["!!!"]);

		bool isMatch = matcher.IsMatch("??!");

		Assert.False(isMatch);
	}

	public static IEnumerable<object[]> GetInvalidConfiguredTagSets()
	{
		yield return [new[] { "official", " " }];
	}
}
