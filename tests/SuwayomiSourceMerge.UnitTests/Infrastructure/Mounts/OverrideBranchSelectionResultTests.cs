namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.UnitTests.TestInfrastructure;

/// <summary>
/// Verifies constructor contracts for <see cref="OverrideBranchSelectionResult"/>.
/// </summary>
public sealed class OverrideBranchSelectionResultTests
{
	/// <summary>
	/// Verifies valid preferred path and ordered entries are preserved.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldStorePreferredPathAndEntries()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string overrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;
		string titlePath = Directory.CreateDirectory(Path.Combine(overrideVolumePath, "Manga Title")).FullName;
		OverrideBranchSelectionEntry preferredEntry = new(overrideVolumePath, titlePath, isPreferred: true);

		OverrideBranchSelectionResult result = new(titlePath, [preferredEntry]);

		Assert.Equal(Path.GetFullPath(titlePath), result.PreferredOverridePath);
		Assert.Single(result.OrderedEntries);
		Assert.True(result.OrderedEntries[0].IsPreferred);
	}

	/// <summary>
	/// Verifies preferred/entry path equality checks are case-insensitive on Windows.
	/// </summary>
	[Fact]
	public void Constructor_Edge_ShouldAcceptCaseVariantPreferredPath_OnWindows()
	{
		if (!OperatingSystem.IsWindows())
		{
			return;
		}

		using TemporaryDirectory temporaryDirectory = new();
		string overrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;
		string titlePath = Directory.CreateDirectory(Path.Combine(overrideVolumePath, "Manga Title")).FullName;
		string caseVariantPreferredPath = PathTestUtilities.InvertPathCase(titlePath);
		OverrideBranchSelectionEntry preferredEntry = new(overrideVolumePath, titlePath, isPreferred: true);

		OverrideBranchSelectionResult result = new(caseVariantPreferredPath, [preferredEntry]);

		Assert.Equal(Path.GetFullPath(caseVariantPreferredPath), result.PreferredOverridePath);
	}

	/// <summary>
	/// Verifies constructor rejects malformed preferred path and entry invariants.
	/// </summary>
	[Fact]
	public void Constructor_Failure_ShouldThrow_WhenInputIsInvalid()
	{
		using TemporaryDirectory temporaryDirectory = new();
		string overrideVolumePath = Directory.CreateDirectory(Path.Combine(temporaryDirectory.Path, "override", "priority")).FullName;
		string titlePath = Directory.CreateDirectory(Path.Combine(overrideVolumePath, "Manga Title")).FullName;
		string differentTitlePath = Directory.CreateDirectory(Path.Combine(overrideVolumePath, "Other Title")).FullName;
		OverrideBranchSelectionEntry preferredEntry = new(overrideVolumePath, titlePath, isPreferred: true);
		OverrideBranchSelectionEntry nonPreferredEntry = new(overrideVolumePath, titlePath, isPreferred: false);

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideBranchSelectionResult(
				"relative/path",
				[preferredEntry]));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideBranchSelectionResult(
				titlePath,
				[]));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideBranchSelectionResult(
				titlePath,
				[nonPreferredEntry]));

		Assert.ThrowsAny<ArgumentException>(
			() => new OverrideBranchSelectionResult(
				differentTitlePath,
				[preferredEntry]));
	}
}
