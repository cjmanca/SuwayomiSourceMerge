namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using System.Net;
using System.Text.Json;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;

/// <summary>
/// Regression coverage for <see cref="ComickCandidateMatcher"/>.
/// </summary>
public sealed partial class ComickCandidateMatcherTests
{
	/// <summary>
	/// Verifies a first candidate with links.mb encoded as a numeric string parses and short-circuits matching.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Regression_ShouldShortCircuitOnFirstCandidate_WhenLinksMangaBuddyIsNumericString()
	{
		(bool firstSuccess, ComickComicResponse? firstPayload, string firstDiagnostic) = ComickPayloadParser.TryParseComicPayload(
			CreateComicJsonWithMangaBuddy("first-slug", "Target Title", "\"1489\""));
		Assert.True(firstSuccess, firstDiagnostic);
		Assert.NotNull(firstPayload);

		RecordingComickApiGateway gateway = new(
			slug => slug switch
			{
				"first-slug" => new ComickDirectApiResult<ComickComicResponse>(
					ComickDirectApiOutcome.Success,
					firstPayload,
					HttpStatusCode.OK,
					"Success."),
				"second-slug" => CreateSuccessResult(CreateDetailPayload("Target Title")),
				_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound)
			});
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "Target Title"),
				CreateSearchCandidate("second-slug", "Target Title")
			],
			["Target Title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.Equal(["first-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Verifies a first candidate with links.mb encoded as a large numeric string parses and short-circuits matching.
	/// </summary>
	[Fact]
	public async Task MatchAsync_Regression_ShouldShortCircuitOnFirstCandidate_WhenLinksMangaBuddyIsInt64MaxNumericString()
	{
		(bool firstSuccess, ComickComicResponse? firstPayload, string firstDiagnostic) = ComickPayloadParser.TryParseComicPayload(
			CreateComicJsonWithMangaBuddy("first-slug", "Target Title", "\"9223372036854775807\""));
		Assert.True(firstSuccess, firstDiagnostic);
		Assert.NotNull(firstPayload);
		ComickComicLinks links = Assert.IsType<ComickComicLinks>(firstPayload.Comic?.Links);
		Assert.True(links.TryGetEntry("mb", out JsonElement mangaBuddyValue));
		Assert.Equal("9223372036854775807", mangaBuddyValue.GetString());

		RecordingComickApiGateway gateway = new(
			slug => slug switch
			{
				"first-slug" => new ComickDirectApiResult<ComickComicResponse>(
					ComickDirectApiOutcome.Success,
					firstPayload,
					HttpStatusCode.OK,
					"Success."),
				"second-slug" => CreateSuccessResult(CreateDetailPayload("Target Title")),
				_ => CreateOutcomeOnlyResult(ComickDirectApiOutcome.NotFound)
			});
		ComickCandidateMatcher matcher = new(gateway);

		ComickCandidateMatchResult result = await matcher.MatchAsync(
			[
				CreateSearchCandidate("first-slug", "Target Title"),
				CreateSearchCandidate("second-slug", "Target Title")
			],
			["Target Title"]);

		Assert.Equal(ComickCandidateMatchOutcome.Matched, result.Outcome);
		Assert.Equal(0, result.MatchedCandidateIndex);
		Assert.Equal(["first-slug"], gateway.RequestedSlugs);
	}

	/// <summary>
	/// Creates one minimal valid comic-detail JSON payload with configurable links.mb token.
	/// </summary>
	/// <param name="slug">Comic slug value.</param>
	/// <param name="title">Comic title value.</param>
	/// <param name="mangaBuddyToken">Raw JSON token value for links.mb.</param>
	/// <returns>Comic-detail JSON payload.</returns>
	private static string CreateComicJsonWithMangaBuddy(string slug, string title, string mangaBuddyToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(slug);
		ArgumentException.ThrowIfNullOrWhiteSpace(title);
		ArgumentException.ThrowIfNullOrWhiteSpace(mangaBuddyToken);

		return
			$$"""
			{
			  "comic": {
			    "id": 1,
			    "hid": "hid-{{slug}}",
			    "title": "{{title}}",
			    "slug": "{{slug}}",
			    "links": { "al": "100", "mb": {{mangaBuddyToken}} },
			    "statistics": [],
			    "recommendations": [],
			    "relate_from": [],
			    "md_titles": [ { "title": "{{title}}" } ],
			    "md_covers": [ { "w": 100, "h": 200, "b2key": "cover.jpg" } ],
			    "md_comic_md_genres": []
			  }
			}
			""";
	}
}
