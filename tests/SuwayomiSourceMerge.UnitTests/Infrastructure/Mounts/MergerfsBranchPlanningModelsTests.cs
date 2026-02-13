namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies constructor contracts for mergerfs branch planning models.
/// </summary>
public sealed class MergerfsBranchPlanningModelsTests
{
	/// <summary>
	/// Verifies source branch candidates preserve logical source names and normalize rooted paths.
	/// </summary>
	[Fact]
	public void MergerfsSourceBranchCandidate_Expected_ShouldStoreSourceNameAndPath()
	{
		MergerfsSourceBranchCandidate candidate = new("Source A", "/ssm/sources/disk1/source-a");

		Assert.Equal("Source A", candidate.SourceName);
		Assert.Equal(Path.GetFullPath("/ssm/sources/disk1/source-a"), candidate.SourcePath);
	}

	/// <summary>
	/// Verifies source branch candidate validation rejects malformed values.
	/// </summary>
	[Theory]
	[InlineData(null, "/ssm/source")]
	[InlineData("", "/ssm/source")]
	[InlineData(" ", "/ssm/source")]
	[InlineData("Source", null)]
	[InlineData("Source", "")]
	[InlineData("Source", " ")]
	[InlineData("Source", "relative/path")]
	public void MergerfsSourceBranchCandidate_Failure_ShouldThrow_WhenInputIsInvalid(
		string? sourceName,
		string? sourcePath)
	{
		Assert.ThrowsAny<ArgumentException>(() => new MergerfsSourceBranchCandidate(sourceName!, sourcePath!));
	}

	/// <summary>
	/// Verifies planning requests accept rooted override/source paths and expose copied values.
	/// </summary>
	[Fact]
	public void MergerfsBranchPlanningRequest_Expected_ShouldStoreRequestValues()
	{
		MergerfsBranchPlanningRequest request = new(
			"group-1",
			"Manga Title",
			"/ssm/state/.mergerfs-branches",
			["/ssm/override/priority"],
			[
				new MergerfsSourceBranchCandidate("Source A", "/ssm/sources/disk1/source-a")
			]);

		Assert.Equal("group-1", request.GroupKey);
		Assert.Equal("Manga Title", request.CanonicalTitle);
		Assert.Single(request.OverrideVolumePaths);
		Assert.Single(request.SourceBranches);
	}

	/// <summary>
	/// Verifies planning requests preserve reserved dot-segment canonical titles for later path-safe escaping.
	/// </summary>
	/// <param name="canonicalTitle">Canonical title under test.</param>
	[Theory]
	[InlineData(".")]
	[InlineData("..")]
	public void MergerfsBranchPlanningRequest_Edge_ShouldAllowReservedDotSegmentCanonicalTitles(string canonicalTitle)
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string overrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;
		string sourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "source-a")).FullName;

		MergerfsBranchPlanningRequest request = new(
			"group-2",
			canonicalTitle,
			branchLinksRootPath,
			[overrideVolumePath],
			[
				new MergerfsSourceBranchCandidate("Source A", sourcePath)
			]);

		Assert.Equal(canonicalTitle, request.CanonicalTitle);
	}

	/// <summary>
	/// Verifies planning request validation rejects malformed group/title/path/list values.
	/// </summary>
	[Fact]
	public void MergerfsBranchPlanningRequest_Failure_ShouldThrow_WhenInputIsInvalid()
	{
		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Bad/Title",
				"/ssm/branchlinks",
				["/ssm/override/priority"],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				@"Bad\Title",
				"/ssm/branchlinks",
				["/ssm/override/priority"],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				"/ssm/branchlinks",
				[],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				"/ssm/branchlinks",
				[null!],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				"/ssm/branchlinks",
				["/ssm/override/priority"],
				[null!]));
	}

	/// <summary>
	/// Verifies branch-link definitions reject malformed link names and paths.
	/// </summary>
	[Fact]
	public void MergerfsBranchLinkDefinition_Failure_ShouldThrow_WhenInputIsInvalid()
	{
		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"bad/name",
				"/ssm/branchlinks/abc/bad_name",
				"/ssm/target",
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				@"bad\name",
				"/ssm/branchlinks/abc/bad_name",
				"/ssm/target",
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"good_name",
				"relative/link",
				"/ssm/target",
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"good_name",
				"/ssm/branchlinks/abc/good_name",
				"relative/target",
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				".",
				"/ssm/branchlinks/abc/current",
				"/ssm/target",
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"..",
				"/ssm/branchlinks/abc/parent",
				"/ssm/target",
				MergerfsBranchAccessMode.ReadWrite));
	}

	/// <summary>
	/// Verifies branch-link definitions reject deterministic cross-platform invalid filename characters and trailing dot/space.
	/// </summary>
	/// <param name="linkName">Link name under test.</param>
	[Theory]
	[InlineData("name:bad")]
	[InlineData("name*bad")]
	[InlineData("name?bad")]
	[InlineData("name\"bad")]
	[InlineData("name<bad")]
	[InlineData("name>bad")]
	[InlineData("name|bad")]
	[InlineData("bad\tname")]
	[InlineData("bad.")]
	[InlineData("bad ")]
	public void MergerfsBranchLinkDefinition_Failure_ShouldThrow_WhenLinkNameContainsInvalidCharactersOrSuffix(
		string linkName)
	{
		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				linkName,
				"/ssm/branchlinks/abc/safe_link",
				"/ssm/target",
				MergerfsBranchAccessMode.ReadWrite));
	}

	/// <summary>
	/// Verifies branch plans reject missing branch-link entries.
	/// </summary>
	[Fact]
	public void MergerfsBranchPlan_Failure_ShouldThrow_WhenBranchLinksMissing()
	{
		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlan(
				"/ssm/override/priority/Manga Title",
				"/ssm/state/.mergerfs-branches/abc",
				"/ssm/state/.mergerfs-branches/abc/00_override_primary=RW",
				"suwayomi_abc_hash",
				"abc",
				[]));
	}

	/// <summary>
	/// Verifies branch-plan model preserves ordered branch-link entries and key identity values.
	/// </summary>
	[Fact]
	public void MergerfsBranchPlan_Expected_ShouldStorePlanValues()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string branchDirectoryPath = Directory.CreateDirectory(Path.Combine(branchLinksRootPath, "groupid")).FullName;
		string preferredOverridePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority", "Manga Title")).FullName;
		string linkPath = Path.Combine(branchDirectoryPath, "00_override_primary");
		MergerfsBranchLinkDefinition linkDefinition = new(
			"00_override_primary",
			linkPath,
			preferredOverridePath,
			MergerfsBranchAccessMode.ReadWrite);

		MergerfsBranchPlan plan = new(
			preferredOverridePath,
			branchDirectoryPath,
			$"{linkPath}=RW",
			"suwayomi_groupid_hash",
			"groupid",
			[linkDefinition]);

		Assert.Equal(preferredOverridePath, plan.PreferredOverridePath);
		Assert.Equal(branchDirectoryPath, plan.BranchDirectoryPath);
		Assert.Equal("groupid", plan.GroupId);
		Assert.Single(plan.BranchLinks);
	}
}
