namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using System.Security.Cryptography;
using System.Text;
using System.Reflection;

using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Configuration.Resolution;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies integration-style branch planning behavior across override selection, source ordering, and identity generation.
/// </summary>
public sealed class MergerfsBranchPlanningServiceTests
{
	/// <summary>
	/// Verifies planner output uses preferred and additional overrides first, then ordered read-only sources.
	/// </summary>
	[Fact]
	public void Plan_Expected_ShouldBuildDeterministicBranchOrderAndAccessModes()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
		string diskTwoVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "disk2")).FullName;
		string diskThreeVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "disk3")).FullName;
		string canonicalTitle = "Manga Title 1";
		string groupKey = "group-key-1";

		string diskTwoTitlePath = Directory.CreateDirectory(Path.Combine(diskTwoVolumePath, canonicalTitle)).FullName;
		string sourceAPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "disk1", "source-a", canonicalTitle)).FullName;
		string sourceZPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "disk2", "source-z", canonicalTitle)).FullName;

		MergerfsBranchPlanningService service = new(CreateSourcePriorityService(["Source A", "Source Z"]));
		MergerfsBranchPlanningRequest request = new(
			groupKey,
			canonicalTitle,
			branchLinksRootPath,
			[diskThreeVolumePath, diskTwoVolumePath, priorityVolumePath],
			[
				new MergerfsSourceBranchCandidate("Source Z", sourceZPath),
				new MergerfsSourceBranchCandidate("Source A", sourceAPath)
			]);

		MergerfsBranchPlan plan = service.Plan(request);

		string expectedPreferredOverridePath = Path.Combine(priorityVolumePath, canonicalTitle);
		Assert.Equal(expectedPreferredOverridePath, plan.PreferredOverridePath);
		Assert.Equal("7aa98438a05a7ea3", plan.GroupId);
		Assert.Equal(Path.Combine(branchLinksRootPath, "7aa98438a05a7ea3"), plan.BranchDirectoryPath);

		Assert.Equal(4, plan.BranchLinks.Count);
		AssertBranchLink(
			plan.BranchLinks[0],
			"00_override_primary",
			expectedPreferredOverridePath,
			MergerfsBranchAccessMode.ReadWrite);
		AssertBranchLink(
			plan.BranchLinks[1],
			"01_override_disk2_000",
			diskTwoTitlePath,
			MergerfsBranchAccessMode.ReadWrite);
		AssertBranchLink(
			plan.BranchLinks[2],
			"10_source_Source_A_000",
			sourceAPath,
			MergerfsBranchAccessMode.ReadOnly);
		AssertBranchLink(
			plan.BranchLinks[3],
			"10_source_Source_Z_001",
			sourceZPath,
			MergerfsBranchAccessMode.ReadOnly);

		string expectedBranchSpecification = string.Join(
			':',
			plan.BranchLinks.Select(
				link => $"{link.LinkPath}={(link.AccessMode == MergerfsBranchAccessMode.ReadWrite ? "RW" : "RO")}"));
		Assert.Equal(expectedBranchSpecification, plan.BranchSpecification);
		Assert.Equal(
			$"suwayomi_{plan.GroupId}_{ComputeSha256Prefix(expectedBranchSpecification, 12)}",
			plan.DesiredIdentity);
	}

	/// <summary>
	/// Verifies planner falls back to the first ordered override volume when no <c>priority</c> volume is present.
	/// </summary>
	[Fact]
	public void Plan_Edge_ShouldFallbackToFirstOrderedOverrideVolume_WhenPriorityVolumeMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string aVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "a-volume")).FullName;
		string zVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "z-volume")).FullName;
		string canonicalTitle = "Manga Title 2";

		string zVolumeTitlePath = Directory.CreateDirectory(Path.Combine(zVolumePath, canonicalTitle)).FullName;

		MergerfsBranchPlanningService service = new(CreateSourcePriorityService([]));
		MergerfsBranchPlanningRequest request = new(
			"group-two",
			canonicalTitle,
			branchLinksRootPath,
			[zVolumePath, aVolumePath],
			[]);

		MergerfsBranchPlan plan = service.Plan(request);

		Assert.Equal(Path.Combine(aVolumePath, canonicalTitle), plan.PreferredOverridePath);
		Assert.Equal(2, plan.BranchLinks.Count);
		AssertBranchLink(
			plan.BranchLinks[0],
			"00_override_primary",
			Path.Combine(aVolumePath, canonicalTitle),
			MergerfsBranchAccessMode.ReadWrite);
		AssertBranchLink(
			plan.BranchLinks[1],
			"01_override_z_volume_000",
			zVolumeTitlePath,
			MergerfsBranchAccessMode.ReadWrite);
	}

	/// <summary>
	/// Verifies duplicate source paths are coalesced in planner output.
	/// </summary>
	[Fact]
	public void Plan_Edge_ShouldDeduplicateSourcePaths_AfterPriorityOrdering()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;
		string canonicalTitle = "Manga Title 3";
		string sharedSourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "shared")).FullName;

		MergerfsBranchPlanningService service = new(CreateSourcePriorityService(["Preferred", "Secondary"]));
		MergerfsBranchPlanningRequest request = new(
			"group-three",
			canonicalTitle,
			branchLinksRootPath,
			[priorityVolumePath],
			[
				new MergerfsSourceBranchCandidate("Secondary", sharedSourcePath),
				new MergerfsSourceBranchCandidate("Preferred", sharedSourcePath)
			]);

		MergerfsBranchPlan plan = service.Plan(request);

		Assert.Equal(2, plan.BranchLinks.Count);
		AssertBranchLink(
			plan.BranchLinks[1],
			"10_source_Preferred_000",
			sharedSourcePath,
			MergerfsBranchAccessMode.ReadOnly);
	}

	/// <summary>
	/// Verifies reserved dot-segment canonical titles are escaped before override and branch-link target path composition.
	/// </summary>
	/// <param name="canonicalTitle">Canonical title under test.</param>
	/// <param name="expectedEscapedSegment">Expected escaped segment used for filesystem paths.</param>
	[Theory]
	[InlineData(".", "_ssm_dot_")]
	[InlineData("..", "_ssm_dotdot_")]
	public void Plan_Edge_ShouldEscapeReservedDotSegmentCanonicalTitles(
		string canonicalTitle,
		string expectedEscapedSegment)
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
		string diskVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "disk1")).FullName;
		string additionalTitlePath = Directory.CreateDirectory(Path.Combine(diskVolumePath, expectedEscapedSegment)).FullName;

		MergerfsBranchPlanningService service = new(CreateSourcePriorityService([]));
		MergerfsBranchPlanningRequest request = new(
			"group-dot-segment",
			canonicalTitle,
			branchLinksRootPath,
			[diskVolumePath, priorityVolumePath],
			[]);

		MergerfsBranchPlan plan = service.Plan(request);

		string expectedPreferredOverridePath = Path.Combine(priorityVolumePath, expectedEscapedSegment);
		Assert.Equal(expectedPreferredOverridePath, plan.PreferredOverridePath);
		Assert.Equal(2, plan.BranchLinks.Count);
		AssertBranchLink(
			plan.BranchLinks[0],
			"00_override_primary",
			expectedPreferredOverridePath,
			MergerfsBranchAccessMode.ReadWrite);
		AssertBranchLink(
			plan.BranchLinks[1],
			"01_override_disk1_000",
			additionalTitlePath,
			MergerfsBranchAccessMode.ReadWrite);
	}

	/// <summary>
	/// Verifies invalid planner invocation arguments are rejected.
	/// </summary>
	[Fact]
	public void Plan_Failure_ShouldThrow_WhenRequestIsNull()
	{
		MergerfsBranchPlanningService service = new(CreateSourcePriorityService(["Source A"]));

		Assert.Throws<ArgumentNullException>(() => service.Plan(null!));
	}

	/// <summary>
	/// Verifies request input validation rejects malformed required values.
	/// </summary>
	[Fact]
	public void Plan_Failure_ShouldThrow_WhenRequestContainsInvalidValues()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string overrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;
		string sourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "source-a")).FullName;

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Bad/Title",
				branchLinksRootPath,
				[overrideVolumePath],
				[
					new MergerfsSourceBranchCandidate("Source A", sourcePath)
				]));
	}

	/// <summary>
	/// Verifies unknown branch access modes fail fast instead of silently defaulting to read-only.
	/// </summary>
	[Fact]
	public void ResolveModeToken_Failure_ShouldThrow_WhenAccessModeIsOutOfRange()
	{
		MethodInfo? method = typeof(MergerfsBranchPlanningService).GetMethod(
			"ResolveModeToken",
			BindingFlags.NonPublic | BindingFlags.Static);
		Assert.NotNull(method);

		TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
			() => method!.Invoke(null, [(MergerfsBranchAccessMode)999]));

		Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
	}

	/// <summary>
	/// Creates a source-priority service backed by the provided ordered source names.
	/// </summary>
	/// <param name="orderedSourceNames">Ordered source names from highest to lowest priority.</param>
	/// <returns>Source-priority service instance.</returns>
	private static ISourcePriorityService CreateSourcePriorityService(IReadOnlyList<string> orderedSourceNames)
	{
		return new SourcePriorityService(
			new SourcePriorityDocument
			{
				Sources = orderedSourceNames.ToList()
			});
	}

	/// <summary>
	/// Computes a lowercase SHA-256 hexadecimal prefix.
	/// </summary>
	/// <param name="value">Value to hash.</param>
	/// <param name="length">Prefix length to return.</param>
	/// <returns>Lowercase SHA-256 prefix.</returns>
	private static string ComputeSha256Prefix(string value, int length)
	{
		byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
		string hashText = Convert.ToHexString(hashBytes).ToLowerInvariant();
		return hashText[..length];
	}

	/// <summary>
	/// Asserts one branch-link definition against expected values.
	/// </summary>
	/// <param name="definition">Definition under test.</param>
	/// <param name="expectedLinkName">Expected link name.</param>
	/// <param name="expectedTargetPath">Expected target path.</param>
	/// <param name="expectedAccessMode">Expected access mode.</param>
	private static void AssertBranchLink(
		MergerfsBranchLinkDefinition definition,
		string expectedLinkName,
		string expectedTargetPath,
		MergerfsBranchAccessMode expectedAccessMode)
	{
		Assert.Equal(expectedLinkName, definition.LinkName);
		Assert.Equal(expectedTargetPath, definition.TargetPath);
		Assert.Equal(expectedAccessMode, definition.AccessMode);
	}
}
