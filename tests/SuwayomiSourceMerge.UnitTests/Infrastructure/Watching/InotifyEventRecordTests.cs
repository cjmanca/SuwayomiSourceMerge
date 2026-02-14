namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Watching;

using SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Verifies parsing behavior for <see cref="InotifyEventRecord"/>.
/// </summary>
public sealed class InotifyEventRecordTests
{
	/// <summary>
	/// Verifies valid formatted lines parse into event records.
	/// </summary>
	[Fact]
	public void TryParse_Expected_ShouldReturnRecord_WhenLineValid()
	{
		bool parsed = InotifyEventRecord.TryParse("/ssm/sources/a/b/c|CREATE,MOVED_TO,ISDIR", out InotifyEventRecord? record);

		Assert.True(parsed);
		Assert.NotNull(record);
		Assert.Equal("/ssm/sources/a/b/c", record!.Path);
		Assert.True(record.IsDirectory);
		Assert.True((record.EventMask & InotifyEventMask.Create) != 0);
		Assert.True((record.EventMask & InotifyEventMask.MovedTo) != 0);
	}

	/// <summary>
	/// Verifies unknown tokens are preserved via <see cref="InotifyEventMask.Unknown"/>.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldSetUnknownFlag_WhenTokenUnknown()
	{
		bool parsed = InotifyEventRecord.TryParse("/ssm/sources/a/b/c|CREATE,CUSTOM_EVENT", out InotifyEventRecord? record);

		Assert.True(parsed);
		Assert.NotNull(record);
		Assert.True((record!.EventMask & InotifyEventMask.Unknown) != 0);
	}

	/// <summary>
	/// Verifies parser uses the last separator so paths containing '|' are preserved.
	/// </summary>
	[Fact]
	public void TryParse_Edge_ShouldPreservePathWithPipe_WhenPathContainsDelimiter()
	{
		bool parsed = InotifyEventRecord.TryParse("/ssm/sources/A|B/Ch|1|CREATE,ISDIR", out InotifyEventRecord? record);

		Assert.True(parsed);
		Assert.NotNull(record);
		Assert.Equal("/ssm/sources/A|B/Ch|1", record!.Path);
		Assert.Equal("CREATE,ISDIR", record.RawEvents);
		Assert.True(record.IsDirectory);
	}

	/// <summary>
	/// Verifies malformed lines fail without throwing.
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("missing-separator")]
	[InlineData("|CREATE")]
	[InlineData("/ssm/sources/a|")]
	public void TryParse_Failure_ShouldReturnFalse_WhenLineMalformed(string? line)
	{
		bool parsed = InotifyEventRecord.TryParse(line!, out InotifyEventRecord? record);

		Assert.False(parsed);
		Assert.Null(record);
	}
}
