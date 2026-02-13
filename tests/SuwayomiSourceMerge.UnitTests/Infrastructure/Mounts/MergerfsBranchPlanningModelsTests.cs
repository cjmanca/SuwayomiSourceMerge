namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies constructor contracts for mergerfs branch planning models.
/// </summary>
public sealed class MergerfsBranchPlanningModelsTests
{
	/// <summary>
	/// Verifies source branch candidates preserve logical source names and normalize fully-qualified paths.
	/// </summary>
	[Fact]
	public void MergerfsSourceBranchCandidate_Expected_ShouldStoreSourceNameAndPath()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string sourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "disk1", "source-a")).FullName;
		MergerfsSourceBranchCandidate candidate = new("Source A", sourcePath);

		Assert.Equal("Source A", candidate.SourceName);
		Assert.Equal(Path.GetFullPath(sourcePath), candidate.SourcePath);
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
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "state", ".mergerfs-branches")).FullName;
		string overrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;
		string sourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "disk1", "source-a")).FullName;

		MergerfsBranchPlanningRequest request = new(
			"group-1",
			"Manga Title",
			branchLinksRootPath,
			[overrideVolumePath],
			[
				new MergerfsSourceBranchCandidate("Source A", sourcePath)
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
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks")).FullName;
		string overrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Bad/Title",
				branchLinksRootPath,
				[overrideVolumePath],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				@"Bad\Title",
				branchLinksRootPath,
				[overrideVolumePath],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				branchLinksRootPath,
				[],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				branchLinksRootPath,
				[null!],
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				branchLinksRootPath,
				[overrideVolumePath],
				[null!]));
	}

	/// <summary>
	/// Verifies source branch candidates reject Windows rooted-but-not-fully-qualified path inputs.
	/// </summary>
	[Fact]
	public void MergerfsSourceBranchCandidate_Failure_ShouldThrow_WhenPathIsWindowsRootedButNotFullyQualified()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		string driveRelativePath = $"{Path.GetPathRoot(Path.GetTempPath())![0]}:drive-relative";

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsSourceBranchCandidate("Source A", @"\root-relative"));
		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsSourceBranchCandidate("Source A", driveRelativePath));
	}

	/// <summary>
	/// Verifies branch-link definitions reject malformed link names and paths.
	/// </summary>
	[Fact]
	public void MergerfsBranchLinkDefinition_Failure_ShouldThrow_WhenInputIsInvalid()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string branchLinksRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks", "abc")).FullName;
		string targetPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "target")).FullName;
		string safeLinkPath = Path.Combine(branchLinksRootPath, "safe_link");

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"bad/name",
				Path.Combine(branchLinksRootPath, "bad_name"),
				targetPath,
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				@"bad\name",
				Path.Combine(branchLinksRootPath, "bad_name"),
				targetPath,
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"good_name",
				"relative/link",
				targetPath,
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"good_name",
				safeLinkPath,
				"relative/target",
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				".",
				Path.Combine(branchLinksRootPath, "current"),
				targetPath,
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"..",
				Path.Combine(branchLinksRootPath, "parent"),
				targetPath,
				MergerfsBranchAccessMode.ReadWrite));
	}

	/// <summary>
	/// Verifies branch-link definitions reject Windows rooted-but-not-fully-qualified path inputs.
	/// </summary>
	[Fact]
	public void MergerfsBranchLinkDefinition_Failure_ShouldThrow_WhenPathsAreWindowsRootedButNotFullyQualified()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		string driveRelativePath = $"{Path.GetPathRoot(Path.GetTempPath())![0]}:drive-relative";

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"good_name",
				@"\root-relative",
				@"\root-relative-target",
				MergerfsBranchAccessMode.ReadWrite));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				"good_name",
				driveRelativePath,
				driveRelativePath,
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
		using TemporaryDirectory temporaryDirectory = new();
		string linkPath = Path.Combine(Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "branchlinks", "abc")).FullName, "safe_link");
		string targetPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "target")).FullName;

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchLinkDefinition(
				linkName,
				linkPath,
				targetPath,
				MergerfsBranchAccessMode.ReadWrite));
	}

	/// <summary>
	/// Verifies branch plans reject missing branch-link entries.
	/// </summary>
	[Fact]
	public void MergerfsBranchPlan_Failure_ShouldThrow_WhenBranchLinksMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string preferredOverridePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority", "Manga Title")).FullName;
		string branchDirectoryPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "state", ".mergerfs-branches", "abc")).FullName;

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlan(
				preferredOverridePath,
				branchDirectoryPath,
				$"{Path.Combine(branchDirectoryPath, "00_override_primary")}=RW",
				"suwayomi_abc_hash",
				"abc",
				[]));
	}

	/// <summary>
	/// Verifies planning requests reject Windows rooted-but-not-fully-qualified path inputs.
	/// </summary>
	[Fact]
	public void MergerfsBranchPlanningRequest_Failure_ShouldThrow_WhenPathsAreWindowsRootedButNotFullyQualified()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string sourcePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "sources", "source-a")).FullName;
		MergerfsSourceBranchCandidate sourceBranch = new("Source A", sourcePath);
		string driveRelativePath = $"{Path.GetPathRoot(Path.GetTempPath())![0]}:drive-relative";

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				@"\branchlinks-root-relative",
				[Path.GetFullPath(Path.Combine(temporaryDirectory.Path, "override", "priority"))],
				[sourceBranch]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlanningRequest(
				"group",
				"Title",
				driveRelativePath,
				[driveRelativePath],
				[sourceBranch]));
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

	/// <summary>
	/// Verifies override selection entries reject Windows rooted-but-not-fully-qualified path inputs.
	/// </summary>
	[Fact]
	public void OverrideBranchSelectionEntry_Failure_ShouldThrow_WhenPathsAreWindowsRootedButNotFullyQualified()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		string driveRelativePath = $"{Path.GetPathRoot(Path.GetTempPath())![0]}:drive-relative";

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideBranchSelectionEntry(
				@"\volume-root-relative",
				@"\title-root-relative",
				isPreferred: true));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideBranchSelectionEntry(
				driveRelativePath,
				driveRelativePath,
				isPreferred: true));
	}

	/// <summary>
	/// Verifies branch plans reject Windows rooted-but-not-fully-qualified path inputs.
	/// </summary>
	[Fact]
	public void MergerfsBranchPlan_Failure_ShouldThrow_WhenPathsAreWindowsRootedButNotFullyQualified()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		string driveRelativePath = $"{Path.GetPathRoot(Path.GetTempPath())![0]}:drive-relative";

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlan(
				@"\preferred-root-relative",
				@"\branch-dir-root-relative",
				@"\branch-dir-root-relative\00_override_primary=RW",
				"suwayomi_abc_hash",
				"abc",
				[
					new MergerfsBranchLinkDefinition(
						"00_override_primary",
						Path.GetFullPath(Path.Combine(Path.GetTempPath(), "valid-link")),
						Path.GetFullPath(Path.Combine(Path.GetTempPath(), "valid-target")),
						MergerfsBranchAccessMode.ReadWrite)
				]));

		Assert.ThrowsAny<ArgumentException>(
			() => new MergerfsBranchPlan(
				driveRelativePath,
				driveRelativePath,
				$"{driveRelativePath}=RW",
				"suwayomi_abc_hash",
				"abc",
				[
					new MergerfsBranchLinkDefinition(
						"00_override_primary",
						Path.GetFullPath(Path.Combine(Path.GetTempPath(), "valid-link-two")),
						Path.GetFullPath(Path.Combine(Path.GetTempPath(), "valid-target-two")),
						MergerfsBranchAccessMode.ReadWrite)
				]));
	}
}
