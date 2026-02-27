namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Logging;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Logging coverage for <see cref="ComickCandidateMatcher"/>.
/// </summary>
public sealed partial class ComickCandidateMatcherTests
{
	/// <summary>
	/// Verifies top-similarity ties emit ambiguity warnings even when one candidate matches.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Edge_ShouldLogCandidateAmbiguityWarning_WhenTopRankingSimilarityIsTiedAndResultMatches()
	{
		RecordingLogger logger = new();
		RecordingComickApiGateway gateway = new(
			slug => CreateSuccessResult(CreateDetailPayload(slug == "slug-1" ? "Target Title" : "Other Title")));
		ComickCandidateMatcher matcher = new(gateway, sceneTagMatcher: null, logger: logger);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("slug-1", "Target Title"),
				CreateSearchCandidate("slug-2", "Target Title")
			],
			["Target Title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.candidate.ambiguity");
		Assert.Equal(LogLevel.Warning, logEvent.Level);
		Assert.Equal("2", logEvent.Context!["tied_candidate_count"]);
		Assert.Equal("2", logEvent.Context["candidate_count"]);
	}

	/// <summary>
	/// Verifies unique top-similarity matches do not emit ambiguity warnings.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Expected_ShouldNotLogCandidateAmbiguity_WhenTopRankingSimilarityIsUniqueAndResultMatches()
	{
		RecordingLogger logger = new();
		RecordingComickApiGateway gateway = new(
			slug => CreateSuccessResult(CreateDetailPayload(slug == "slug-1" ? "Target Title" : "Other Title")));
		ComickCandidateMatcher matcher = new(gateway, sceneTagMatcher: null, logger: logger);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("slug-1", "Target Title"),
				CreateSearchCandidate("slug-2", "Other Title")
			],
			["Target Title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.DoesNotContain(logger.Events, static entry => entry.EventId == "metadata.candidate.ambiguity");
	}

	/// <summary>
	/// Verifies explicit top-similarity ties emit candidate ambiguity warnings.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Edge_ShouldLogCandidateAmbiguityWarning_WhenTopRankingSimilarityIsTied()
	{
		RecordingLogger logger = new();
		RecordingComickApiGateway gateway = new(_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound));
		ComickCandidateMatcher matcher = new(gateway, sceneTagMatcher: null, logger: logger);

		_ = await matcher.MatchAsync(
			[
				CreateSearchCandidate("slug-1", "Target Title"),
				CreateSearchCandidate("slug-2", "Target Title")
			],
			["Target Title"]);

		RecordingLogger.CapturedLogEvent logEvent = Assert.Single(
			logger.Events,
			static entry => entry.EventId == "metadata.candidate.ambiguity");
		Assert.Equal(LogLevel.Warning, logEvent.Level);
		Assert.Equal("2", logEvent.Context!["tied_candidate_count"]);
		Assert.Equal("2", logEvent.Context["candidate_count"]);
	}

	/// <summary>
	/// Verifies no ambiguity warning is emitted when the top ranking-hint similarity is unique.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Expected_ShouldNotLogCandidateAmbiguity_WhenTopRankingSimilarityIsUnique()
	{
		RecordingLogger logger = new();
		RecordingComickApiGateway gateway = new(_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound));
		ComickCandidateMatcher matcher = new(gateway, sceneTagMatcher: null, logger: logger);

		_ = await matcher.MatchAsync(
			[
				CreateSearchCandidate("slug-1", "Target Title"),
				CreateSearchCandidate("slug-2", "Other Title")
			],
			["Target Title"]);

		Assert.DoesNotContain(logger.Events, static entry => entry.EventId == "metadata.candidate.ambiguity");
	}

	/// <summary>
	/// Verifies zero-similarity ties do not emit candidate ambiguity warnings.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Failure_ShouldNotLogCandidateAmbiguity_WhenTopSimilarityIsZero()
	{
		RecordingLogger logger = new();
		RecordingComickApiGateway gateway = new(_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound));
		ComickCandidateMatcher matcher = new(gateway, sceneTagMatcher: null, logger: logger);

		_ = await matcher.MatchAsync(
			[
				CreateSearchCandidate("slug-1", "111"),
				CreateSearchCandidate("slug-2", "222")
			],
			["target"]);

		Assert.DoesNotContain(logger.Events, static entry => entry.EventId == "metadata.candidate.ambiguity");
	}
}
