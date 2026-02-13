namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies override branch selection behavior for preferred and additional override branches.
/// </summary>
public sealed class OverrideBranchSelectionServiceTests
{
	/// <summary>
	/// Verifies the <c>priority</c> override volume is preferred and existing non-preferred title directories are included.
	/// </summary>
	[Fact]
	public void Select_Expected_ShouldPreferPriorityVolume_AndIncludeExistingAdditionalOverrides()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
		string diskOneVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "disk1")).FullName;
		string diskTwoVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "disk2")).FullName;
		string canonicalTitle = "Manga Title 1";

		string diskTwoTitlePath = Directory.CreateDirectory(Path.Combine(diskTwoVolumePath, canonicalTitle)).FullName;

		OverrideBranchSelectionService service = new();

		OverrideBranchSelectionResult result = service.Select(
			canonicalTitle,
			[diskTwoVolumePath, diskOneVolumePath, priorityVolumePath]);

		Assert.Equal(Path.Combine(priorityVolumePath, canonicalTitle), result.PreferredOverridePath);
		Assert.Equal(2, result.OrderedEntries.Count);
		Assert.True(result.OrderedEntries[0].IsPreferred);
		Assert.Equal(priorityVolumePath, result.OrderedEntries[0].VolumeRootPath);
		Assert.Equal(Path.Combine(priorityVolumePath, canonicalTitle), result.OrderedEntries[0].TitlePath);
		Assert.False(result.OrderedEntries[1].IsPreferred);
		Assert.Equal(diskTwoVolumePath, result.OrderedEntries[1].VolumeRootPath);
		Assert.Equal(diskTwoTitlePath, result.OrderedEntries[1].TitlePath);
	}

	/// <summary>
	/// Verifies selection falls back to the first deterministic override volume when no <c>priority</c> volume exists.
	/// </summary>
	[Fact]
	public void Select_Edge_ShouldFallbackToFirstOrderedVolume_WhenPriorityVolumeMissing()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string zVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "z-volume")).FullName;
		string aVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "a-volume")).FullName;
		string canonicalTitle = "Manga Title 2";

		string zVolumeTitlePath = Directory.CreateDirectory(Path.Combine(zVolumePath, canonicalTitle)).FullName;

		OverrideBranchSelectionService service = new();

		OverrideBranchSelectionResult result = service.Select(canonicalTitle, [zVolumePath, aVolumePath]);

		Assert.Equal(Path.Combine(aVolumePath, canonicalTitle), result.PreferredOverridePath);
		Assert.Equal(2, result.OrderedEntries.Count);
		Assert.Equal(aVolumePath, result.OrderedEntries[0].VolumeRootPath);
		Assert.Equal(zVolumeTitlePath, result.OrderedEntries[1].TitlePath);
	}

	/// <summary>
	/// Verifies duplicate override volume paths are de-duplicated deterministically.
	/// </summary>
	[Fact]
	public void Select_Edge_ShouldDeduplicateDuplicateOverrideVolumePaths()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
		string canonicalTitle = "Manga Title 3";

		OverrideBranchSelectionService service = new();

		OverrideBranchSelectionResult result = service.Select(
			canonicalTitle,
			[priorityVolumePath, priorityVolumePath]);

		OverrideBranchSelectionEntry onlyEntry = Assert.Single(result.OrderedEntries);
		Assert.True(onlyEntry.IsPreferred);
		Assert.Equal(Path.Combine(priorityVolumePath, canonicalTitle), onlyEntry.TitlePath);
	}

	/// <summary>
	/// Verifies Windows-style case-variant override paths are treated as the same volume.
	/// </summary>
	[Fact]
	public void Select_Edge_ShouldTreatCaseVariantPathsAsSameVolume_OnWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
		string canonicalTitle = "Manga Title 4";
		string caseVariantPriorityPath = PathTestUtilities.InvertPathCase(priorityVolumePath);

		OverrideBranchSelectionService service = new();

		OverrideBranchSelectionResult result = service.Select(
			canonicalTitle,
			[priorityVolumePath, caseVariantPriorityPath]);

		OverrideBranchSelectionEntry onlyEntry = Assert.Single(result.OrderedEntries);
		Assert.True(onlyEntry.IsPreferred);
	}

	/// <summary>
	/// Verifies case-variant override-volume duplicates produce a deterministic representative independent of input order.
	/// </summary>
	[Fact]
	public void Select_Edge_ShouldChooseDeterministicCaseVariantRepresentative_IndependentOfInputOrder_OnWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string aVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "a-volume")).FullName;
		string bVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "b-volume")).FullName;
		string caseVariantAVolumePath = PathTestUtilities.InvertPathCase(aVolumePath);
		string canonicalTitle = "Manga Title 5";

		OverrideBranchSelectionService service = new();

		OverrideBranchSelectionResult firstOrderResult = service.Select(
			canonicalTitle,
			[caseVariantAVolumePath, bVolumePath, aVolumePath]);
		OverrideBranchSelectionResult secondOrderResult = service.Select(
			canonicalTitle,
			[aVolumePath, bVolumePath, caseVariantAVolumePath]);

		string normalizedA = Path.GetFullPath(aVolumePath);
		string normalizedCaseVariantA = Path.GetFullPath(caseVariantAVolumePath);
		string expectedRepresentative = string.Compare(normalizedA, normalizedCaseVariantA, StringComparison.Ordinal) <= 0
			? normalizedA
			: normalizedCaseVariantA;
		string expectedPreferredOverridePath = Path.Combine(expectedRepresentative, canonicalTitle);

		Assert.Equal(expectedPreferredOverridePath, firstOrderResult.PreferredOverridePath);
		Assert.Equal(expectedPreferredOverridePath, secondOrderResult.PreferredOverridePath);
		Assert.Equal(firstOrderResult.PreferredOverridePath, secondOrderResult.PreferredOverridePath);
	}

	/// <summary>
	/// Verifies preferred-override selection remains stable across input permutations when case-variant duplicates include a priority volume.
	/// </summary>
	[Fact]
	public void Select_Edge_ShouldKeepPriorityPreferredSelectionStableAcrossInputPermutations_OnWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
		string caseVariantPriorityPath = PathTestUtilities.InvertPathCase(priorityVolumePath);
		string zVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "z-volume")).FullName;
		string canonicalTitle = "Manga Title 6";

		OverrideBranchSelectionService service = new();

		OverrideBranchSelectionResult firstOrderResult = service.Select(
			canonicalTitle,
			[caseVariantPriorityPath, zVolumePath, priorityVolumePath]);
		OverrideBranchSelectionResult secondOrderResult = service.Select(
			canonicalTitle,
			[priorityVolumePath, zVolumePath, caseVariantPriorityPath]);

		Assert.Equal(firstOrderResult.PreferredOverridePath, secondOrderResult.PreferredOverridePath);
		Assert.EndsWith(
			$"{Path.DirectorySeparatorChar}priority{Path.DirectorySeparatorChar}{canonicalTitle}",
			firstOrderResult.PreferredOverridePath,
			StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Verifies reserved dot-segment canonical titles are escaped before building override paths.
	/// </summary>
	/// <param name="canonicalTitle">Canonical title under test.</param>
	/// <param name="expectedEscapedSegment">Expected escaped segment used for path composition.</param>
	[Theory]
	[InlineData(".", "_ssm_dot_")]
	[InlineData("..", "_ssm_dotdot_")]
	public void Select_Edge_ShouldEscapeReservedDotSegments_WhenBuildingTitlePaths(
		string canonicalTitle,
		string expectedEscapedSegment)
	{
		using TemporaryDirectory temporaryDirectory = new();
		string overrideRootPath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override")).FullName;
		string priorityVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "priority")).FullName;
		string diskVolumePath = Directory.CreateDirectory(Path.Combine(overrideRootPath, "disk1")).FullName;
		string existingAdditionalPath = Directory.CreateDirectory(Path.Combine(diskVolumePath, expectedEscapedSegment)).FullName;

		OverrideBranchSelectionService service = new();

		OverrideBranchSelectionResult result = service.Select(
			canonicalTitle,
			[diskVolumePath, priorityVolumePath]);

		string expectedPreferredPath = Path.Combine(priorityVolumePath, expectedEscapedSegment);
		Assert.Equal(expectedPreferredPath, result.PreferredOverridePath);
		Assert.Equal(2, result.OrderedEntries.Count);
		Assert.Equal(expectedPreferredPath, result.OrderedEntries[0].TitlePath);
		Assert.Equal(existingAdditionalPath, result.OrderedEntries[1].TitlePath);
	}

	/// <summary>
	/// Verifies invalid canonical titles and override volume inputs are rejected.
	/// </summary>
	[Fact]
	public void Select_Failure_ShouldThrow_WhenInputIsInvalid()
	{
		OverrideBranchSelectionService service = new();
		using TemporaryDirectory temporaryDirectory = new();
		string validOverrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;

		Assert.ThrowsAny<ArgumentException>(() => service.Select(null!, [validOverrideVolumePath]));
		Assert.ThrowsAny<ArgumentException>(() => service.Select("Title", []));
		Assert.ThrowsAny<ArgumentException>(() => service.Select("Title", [null!]));
		Assert.ThrowsAny<ArgumentException>(() => service.Select("Bad/Title", [validOverrideVolumePath]));
		Assert.ThrowsAny<ArgumentException>(() => service.Select(@"Bad\Title", [validOverrideVolumePath]));
	}

	/// <summary>
	/// Verifies Windows rooted-but-not-fully-qualified override volume inputs are rejected.
	/// </summary>
	[Fact]
	public void Select_Failure_ShouldThrow_WhenOverrideVolumePathsAreWindowsRootedButNotFullyQualified()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		OverrideBranchSelectionService service = new();
		string driveRelativePath = $"{Path.GetPathRoot(Path.GetTempPath())![0]}:drive-relative";

		Assert.ThrowsAny<ArgumentException>(() => service.Select("Title", [@"\root-relative"]));
		Assert.ThrowsAny<ArgumentException>(() => service.Select("Title", [driveRelativePath]));
	}
}
