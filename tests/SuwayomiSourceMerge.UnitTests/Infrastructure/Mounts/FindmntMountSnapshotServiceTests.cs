namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FindmntMountSnapshotService"/>.
/// </summary>
public sealed class FindmntMountSnapshotServiceTests
{
	/// <summary>
	/// Verifies valid <c>findmnt -P</c> output lines are parsed into snapshot entries.
	/// </summary>
	[Fact]
	public void Capture_Expected_ShouldParseValidFindmntOutput()
	{
		FakeExternalCommandExecutor commandExecutor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				"TARGET=\"/ssm/merged/Title\" FSTYPE=\"fuse3.mergerfs\" SOURCE=\"suwayomi_hash\" OPTIONS=\"rw,fsname=suwayomi_hash\"",
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		FindmntMountSnapshotService service = new(
			commandExecutor,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50),
			4096);

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Warnings);
		Assert.Single(snapshot.Entries);
		MountSnapshotEntry entry = snapshot.Entries[0];
		Assert.Equal("/ssm/merged/Title", entry.MountPoint);
		Assert.Equal("fuse3.mergerfs", entry.FileSystemType);
		Assert.Equal("suwayomi_hash", entry.Source);
		Assert.Equal("rw,fsname=suwayomi_hash", entry.Options);
		Assert.Null(entry.IsHealthy);
		Assert.NotNull(commandExecutor.LastRequest);
		Assert.Equal("findmnt", commandExecutor.LastRequest!.FileName);
		Assert.Equal(["-rn", "-P", "-o", "TARGET,FSTYPE,SOURCE,OPTIONS"], commandExecutor.LastRequest.Arguments);
	}

	/// <summary>
	/// Verifies escaped values are decoded and malformed lines are skipped with warnings.
	/// </summary>
	[Fact]
	public void Capture_Edge_ShouldDecodeEscapedValuesAndSkipMalformedLines()
	{
		string output =
			"TARGET=\"/ssm/merged/Space\\040Title\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"mergerfs#disk\\040one\" OPTIONS=\"rw,fsname=suwayomi\\040abc\""
			+ Environment.NewLine
			+ "BROKEN-LINE";
		FakeExternalCommandExecutor commandExecutor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				output,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		FindmntMountSnapshotService service = new(
			commandExecutor,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50),
			4096);

		MountSnapshot snapshot = service.Capture();

		Assert.Single(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-002", snapshot.Warnings[0].Code);
		Assert.Equal("/ssm/merged/Space Title", snapshot.Entries[0].MountPoint);
		Assert.Equal("mergerfs#disk one", snapshot.Entries[0].Source);
		Assert.Equal("rw,fsname=suwayomi abc", snapshot.Entries[0].Options);
	}

	/// <summary>
	/// Verifies lines with values ending in escaped backslashes are parsed without malformed-line warnings.
	/// </summary>
	[Fact]
	public void Capture_Edge_ShouldParseLine_WhenQuotedValueEndsWithEscapedBackslash()
	{
		string output = "TARGET=\"/ssm/merged/Trail\\\\\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"source-id\" OPTIONS=\"rw\"";
		FakeExternalCommandExecutor commandExecutor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				output,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		FindmntMountSnapshotService service = new(
			commandExecutor,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50),
			4096);

		MountSnapshot snapshot = service.Capture();

		Assert.Single(snapshot.Entries);
		Assert.Empty(snapshot.Warnings);
		Assert.Equal("/ssm/merged/Trail\\", snapshot.Entries[0].MountPoint);
	}

	/// <summary>
	/// Verifies parsed entries are ordered deterministically by mountpoint.
	/// </summary>
	[Fact]
	public void Capture_Edge_ShouldOrderEntriesByMountPoint()
	{
		string output =
			"TARGET=\"/ssm/merged/Zeta\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"zeta\" OPTIONS=\"rw\""
			+ Environment.NewLine
			+ "TARGET=\"/ssm/merged/Alpha\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"alpha\" OPTIONS=\"rw\"";
		FakeExternalCommandExecutor commandExecutor = new(
			new ExternalCommandResult(
				ExternalCommandOutcome.Success,
				ExternalCommandFailureKind.None,
				0,
				output,
				string.Empty,
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		FindmntMountSnapshotService service = new(
			commandExecutor,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50),
			4096);

		MountSnapshot snapshot = service.Capture();

		Assert.Equal(["/ssm/merged/Alpha", "/ssm/merged/Zeta"], snapshot.Entries.Select(entry => entry.MountPoint).ToArray());
	}

	/// <summary>
	/// Verifies non-success command outcomes return an empty snapshot with deterministic warning diagnostics.
	/// </summary>
	/// <param name="outcome">Outcome emitted by command execution.</param>
	/// <param name="failureKind">Failure kind emitted by command execution.</param>
	/// <param name="exitCode">Optional command exit code.</param>
	[Theory]
	[InlineData("TimedOut", "None", null)]
	[InlineData("Cancelled", "None", null)]
	[InlineData("StartFailed", "ToolNotFound", null)]
	[InlineData("NonZeroExit", "None", 3)]
	public void Capture_Failure_ShouldReturnWarningAndNoEntries_WhenCommandFails(
		string outcome,
		string failureKind,
		int? exitCode)
	{
		ExternalCommandOutcome parsedOutcome = Enum.Parse<ExternalCommandOutcome>(outcome);
		ExternalCommandFailureKind parsedFailureKind = Enum.Parse<ExternalCommandFailureKind>(failureKind);

		FakeExternalCommandExecutor commandExecutor = new(
			new ExternalCommandResult(
				parsedOutcome,
				parsedFailureKind,
				exitCode,
				string.Empty,
				"diagnostic stderr",
				false,
				false,
				TimeSpan.FromMilliseconds(5)));
		FindmntMountSnapshotService service = new(
			commandExecutor,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50),
			4096);

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-001", snapshot.Warnings[0].Code);
		Assert.Contains(parsedOutcome.ToString(), snapshot.Warnings[0].Message, StringComparison.Ordinal);
		Assert.Contains("diagnostic stderr", snapshot.Warnings[0].Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Fake command executor used to provide deterministic command results in tests.
	/// </summary>
	private sealed class FakeExternalCommandExecutor : IExternalCommandExecutor
	{
		/// <summary>
		/// Result returned by this fake executor.
		/// </summary>
		private readonly ExternalCommandResult _result;

		/// <summary>
		/// Initializes a new instance of the <see cref="FakeExternalCommandExecutor"/> class.
		/// </summary>
		/// <param name="result">Command result to return from <see cref="Execute"/>.</param>
		public FakeExternalCommandExecutor(ExternalCommandResult result)
		{
			_result = result ?? throw new ArgumentNullException(nameof(result));
		}

		/// <summary>
		/// Gets the last command request passed to <see cref="Execute"/>.
		/// </summary>
		public ExternalCommandRequest? LastRequest
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public ExternalCommandResult Execute(ExternalCommandRequest request, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);
			LastRequest = request;
			return _result;
		}
	}
}
