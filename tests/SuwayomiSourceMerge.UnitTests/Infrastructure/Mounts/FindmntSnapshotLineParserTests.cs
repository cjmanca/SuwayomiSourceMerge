namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FindmntSnapshotLineParser"/>.
/// </summary>
public sealed class FindmntSnapshotLineParserTests
{
	/// <summary>
	/// Verifies null input returns parse failure instead of throwing.
	/// </summary>
	[Fact]
	public void TryParse_Failure_ShouldReturnWarning_WhenInputIsNull()
	{
		bool success = FindmntSnapshotLineParser.TryParse(null, out MountSnapshotEntry? entry, out string? warningMessage);

		Assert.False(success);
		Assert.Null(entry);
		Assert.Equal("line is null, empty, or whitespace", warningMessage);
	}

	/// <summary>
	/// Verifies whitespace input returns parse failure instead of throwing.
	/// </summary>
	[Fact]
	public void TryParse_Failure_ShouldReturnWarning_WhenInputIsWhitespace()
	{
		bool success = FindmntSnapshotLineParser.TryParse("   ", out MountSnapshotEntry? entry, out string? warningMessage);

		Assert.False(success);
		Assert.Null(entry);
		Assert.Equal("line is null, empty, or whitespace", warningMessage);
	}

	/// <summary>
	/// Verifies values ending with escaped backslashes are parsed and terminated correctly.
	/// </summary>
	[Fact]
	public void TryParse_Expected_ShouldParseLine_WhenQuotedValueEndsWithEscapedBackslash()
	{
		string line = "TARGET=\"/ssm/merged/Trail\\\\\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"source-id\" OPTIONS=\"rw\"";

		bool success = FindmntSnapshotLineParser.TryParse(line, out MountSnapshotEntry? entry, out string? warningMessage);

		Assert.True(success);
		Assert.Null(warningMessage);
		Assert.NotNull(entry);
		Assert.Equal("/ssm/merged/Trail\\", entry!.MountPoint);
		Assert.Equal("fuse.mergerfs", entry.FileSystemType);
		Assert.Equal("source-id", entry.Source);
		Assert.Equal("rw", entry.Options);
	}

	/// <summary>
	/// Verifies escaped quotes inside quoted values do not terminate value parsing early.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldParseEscapedQuotesInsideQuotedValues()
	{
		string line = "TARGET=\"/ssm/merged/Quote\\\"Title\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"source-id\" OPTIONS=\"rw\"";

		bool success = FindmntSnapshotLineParser.TryParse(line, out MountSnapshotEntry? entry, out string? warningMessage);

		Assert.True(success);
		Assert.Null(warningMessage);
		Assert.NotNull(entry);
		Assert.Equal("/ssm/merged/Quote\"Title", entry!.MountPoint);
	}

	/// <summary>
	/// Verifies unknown escapes preserve their backslash to avoid data loss.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldPreserveBackslash_WhenEscapeSequenceIsUnknown()
	{
		string line = "TARGET=\"/ssm/merged/Unknown\\qValue\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"source-id\" OPTIONS=\"rw\"";

		bool success = FindmntSnapshotLineParser.TryParse(line, out MountSnapshotEntry? entry, out string? warningMessage);

		Assert.True(success);
		Assert.Null(warningMessage);
		Assert.NotNull(entry);
		Assert.Equal("/ssm/merged/Unknown\\qValue", entry!.MountPoint);
	}

	/// <summary>
	/// Verifies unterminated quoted fields fail safely with deterministic warning text.
	/// </summary>
	[Fact]
	public void TryParse_Failure_ShouldReturnWarning_WhenQuotedValueIsUnterminated()
	{
		string line = "TARGET=\"/ssm/merged/Bad";

		bool success = FindmntSnapshotLineParser.TryParse(line, out MountSnapshotEntry? entry, out string? warningMessage);

		Assert.False(success);
		Assert.Null(entry);
		Assert.NotNull(warningMessage);
		Assert.Contains("unterminated quoted value", warningMessage, StringComparison.Ordinal);
	}
}
