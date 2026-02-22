namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Metadata;

using SuwayomiSourceMerge.Infrastructure.Metadata;

/// <summary>
/// Verifies expected and failure behavior for <see cref="MetadataStatePaths"/>.
/// </summary>
public sealed class MetadataStatePathsTests
{
	/// <summary>
	/// Verifies constructor resolves canonical metadata state file paths under the configured root.
	/// </summary>
	[Fact]
	public void Constructor_Expected_ShouldResolveMetadataStatePaths()
	{
		string stateRootPath = Path.Combine(Path.GetTempPath(), "ssm-state-paths-tests");
		MetadataStatePaths paths = new(stateRootPath);

		Assert.Equal(Path.GetFullPath(stateRootPath), paths.StateRootPath);
		Assert.Equal(
			Path.Combine(Path.GetFullPath(stateRootPath), MetadataStatePaths.MetadataStateFileName),
			paths.MetadataStateFilePath);
		Assert.Equal(
			Path.Combine(Path.GetFullPath(stateRootPath), MetadataStatePaths.MetadataStateCorruptFileName),
			paths.MetadataStateCorruptFilePath);
		Assert.Equal(
			Path.Combine(Path.GetFullPath(stateRootPath), MetadataStatePaths.MetadataStateCorruptDirectoryName),
			paths.MetadataStateCorruptDirectoryPath);
	}

	/// <summary>
	/// Verifies constructor rejects null, empty, or whitespace state-root values.
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void Constructor_Failure_ShouldThrow_WhenStateRootPathInvalid(string? stateRootPath)
	{
		Assert.ThrowsAny<ArgumentException>(() => new MetadataStatePaths(stateRootPath!));
	}
}
