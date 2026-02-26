namespace SuwayomiSourceMerge.UnitTests.Application.Mounting;

using System.Net;

using SuwayomiSourceMerge.Application.Mounting;
using SuwayomiSourceMerge.Application.Watching;
using SuwayomiSourceMerge.Infrastructure.Metadata.Comick;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Metadata coordinator integration coverage for <see cref="MergeMountWorkflow"/>.
/// </summary>
public sealed partial class MergeMountWorkflowTests
{
	/// <summary>
	/// Verifies missing cover and details artifacts trigger one API call and both artifact ensure paths.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldEnsureCoverAndDetails_WhenBothArtifactsAreMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		ConfigureSuccessfulComickMatch(fixture);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(1, fixture.ComickApiGateway.SearchCallCount);
		Assert.Single(fixture.CoverService.Requests);
		Assert.Single(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Verifies missing-cover-only path triggers API and cover ensure while skipping details ensure.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldEnsureCoverOnly_WhenDetailsAlreadyExists()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string titleDirectory = Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Canonical Title");
		Directory.CreateDirectory(titleDirectory);
		File.WriteAllText(Path.Combine(titleDirectory, "details.json"), "{}");
		ConfigureSuccessfulComickMatch(fixture);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(1, fixture.ComickApiGateway.SearchCallCount);
		Assert.Single(fixture.CoverService.Requests);
		Assert.Empty(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Verifies missing-details-only path triggers API and details ensure while skipping cover ensure.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldEnsureDetailsOnly_WhenCoverAlreadyExists()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string titleDirectory = Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Canonical Title");
		Directory.CreateDirectory(titleDirectory);
		File.WriteAllBytes(Path.Combine(titleDirectory, "cover.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);
		ConfigureSuccessfulComickMatch(fixture);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(1, fixture.ComickApiGateway.SearchCallCount);
		Assert.Empty(fixture.CoverService.Requests);
		Assert.Single(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Verifies when both artifacts already exist the coordinator short-circuits without API calls.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldShortCircuitWithoutApi_WhenBothArtifactsAlreadyExist()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		string titleDirectory = Path.Combine(fixture.VolumeDiscoveryService.OverrideVolumePaths[0], "Canonical Title");
		Directory.CreateDirectory(titleDirectory);
		File.WriteAllBytes(Path.Combine(titleDirectory, "cover.jpg"), [0xFF, 0xD8, 0xFF, 0xD9]);
		File.WriteAllText(Path.Combine(titleDirectory, "details.json"), "{}");
		ConfigureSuccessfulComickMatch(fixture);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(0, fixture.ComickApiGateway.SearchCallCount);
		Assert.Empty(fixture.CoverService.Requests);
		Assert.Empty(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Verifies Comick service interruption outcomes fail the merge pass while preserving best-effort details fallback.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenComickSearchReportsServiceInterruption()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.ComickApiGateway.NextSearchResult = new ComickDirectApiResult<ComickSearchResponse>(
			ComickDirectApiOutcome.HttpFailure,
			payload: null,
			statusCode: HttpStatusCode.BadGateway,
			diagnostic: "upstream failure");
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Equal(1, fixture.ComickApiGateway.SearchCallCount);
		Assert.Empty(fixture.CoverService.Requests);
		Assert.Single(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Verifies no-match candidate resolution with interruption telemetry fails the merge pass.
	/// </summary>
	[Fact]
	public void RunMergePass_Failure_ShouldReturnFailure_WhenCandidateResolutionHasInterruptionAndNoMatch()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		fixture.ComickApiGateway.NextSearchResult = new ComickDirectApiResult<ComickSearchResponse>(
			ComickDirectApiOutcome.Success,
			new ComickSearchResponse(
			[
				new ComickSearchComic
				{
					Slug = "candidate-slug"
				}
			]),
			statusCode: HttpStatusCode.OK,
			diagnostic: "Success.");
		fixture.ComickCandidateMatcher.NextMatchResult = new ComickCandidateMatchResult(
			ComickCandidateMatchOutcome.NoHighConfidenceMatch,
			matchedCandidate: null,
			ComickCandidateMatchResult.NoMatchCandidateIndex,
			hadTopTie: false,
			matchScore: 0,
			hadServiceInterruption: true);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Failure, outcome);
		Assert.Equal(1, fixture.ComickApiGateway.SearchCallCount);
		Assert.Empty(fixture.CoverService.Requests);
		Assert.Single(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Verifies matched candidate resolution remains successful even when interruption telemetry is present.
	/// </summary>
	[Fact]
	public void RunMergePass_Expected_ShouldRemainSuccess_WhenCandidateResolutionMatchesWithInterruptionTelemetry()
	{
		using TemporaryDirectory temporaryDirectory = new();
		WorkflowFixture fixture = CreateFixture(temporaryDirectory);
		ConfigureSuccessfulComickMatch(fixture, hadServiceInterruption: true);
		MergeMountWorkflow workflow = fixture.CreateWorkflow();

		MergeScanDispatchOutcome outcome = workflow.RunMergePass("interval elapsed", force: false);

		Assert.Equal(MergeScanDispatchOutcome.Success, outcome);
		Assert.Equal(1, fixture.ComickApiGateway.SearchCallCount);
		Assert.Single(fixture.CoverService.Requests);
		Assert.Single(fixture.DetailsService.Requests);
	}

	/// <summary>
	/// Configures one deterministic successful Comick search and matched comic payload.
	/// </summary>
	/// <param name="fixture">Workflow fixture.</param>
	private static void ConfigureSuccessfulComickMatch(WorkflowFixture fixture, bool hadServiceInterruption = false)
	{
		ArgumentNullException.ThrowIfNull(fixture);

		fixture.ComickApiGateway.NextSearchResult = new ComickDirectApiResult<ComickSearchResponse>(
			ComickDirectApiOutcome.Success,
			new ComickSearchResponse(
			[
				new ComickSearchComic
				{
					Slug = "solo-leveling"
				}
			]),
			statusCode: HttpStatusCode.OK,
			diagnostic: "Success.");
		fixture.ComickCandidateMatcher.NextMatchResult = new ComickCandidateMatchResult(
			ComickCandidateMatchOutcome.Matched,
			new ComickComicResponse
			{
				Comic = new ComickComicDetails
				{
					Hid = "solo-leveling-hid",
					Slug = "solo-leveling",
					Title = "Canonical Title",
					MdCovers =
					[
						new ComickCover
						{
							B2Key = "solo-leveling-cover.webp"
						}
					],
					MdTitles =
					[
						new ComickTitleAlias
						{
							Title = "Title One",
							Language = "en"
						}
					]
				}
			},
			matchedCandidateIndex: 0,
			hadTopTie: false,
			matchScore: 2,
			hadServiceInterruption);
	}
}
