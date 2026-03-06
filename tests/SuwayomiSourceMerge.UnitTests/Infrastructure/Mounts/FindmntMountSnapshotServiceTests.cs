namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Mounts;

using System.ComponentModel;
using System.Diagnostics;
using SuwayomiSourceMerge.Infrastructure.Mounts;
using SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="FindmntMountSnapshotService"/>.
/// </summary>
public sealed class FindmntMountSnapshotServiceTests
{
	/// <summary>
	/// Verifies streaming capture parses large output without truncation-induced malformed tails.
	/// </summary>
	[Fact]
	public void Capture_Expected_ShouldParseLargeFindmntOutputWithoutTruncation()
	{
		const int entryCount = 1400;
		string output = string.Join(
			Environment.NewLine,
			Enumerable.Range(0, entryCount)
				.Select(static index => $"TARGET=\"/ssm/merged/Title {index:D4}\" FSTYPE=\"fuse3.mergerfs\" SOURCE=\"src-{index:D4}\" OPTIONS=\"rw,fsname=src-{index:D4}\""));
		FakeProcessFacade process = CreateSuccessfulProcess(output, string.Empty);
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Warnings);
		Assert.Equal(entryCount, snapshot.Entries.Count);
		Assert.Equal("/ssm/merged/Title 0000", snapshot.Entries[0].MountPoint);
		Assert.Equal($"/ssm/merged/Title {entryCount - 1:D4}", snapshot.Entries[^1].MountPoint);
		Assert.NotNull(process.ConfiguredStartInfo);
		Assert.Equal("findmnt", process.ConfiguredStartInfo!.FileName);
		Assert.Equal(["-n", "-P", "-o", "TARGET,FSTYPE,SOURCE,OPTIONS"], process.ConfiguredStartInfo.ArgumentList);
		Assert.Equal(0, process.KillCallCount);
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
		FakeProcessFacade process = CreateSuccessfulProcess(output, string.Empty);
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Single(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-002", snapshot.Warnings[0].Code);
		Assert.Equal(MountSnapshotWarningSeverity.DegradedVisibility, snapshot.Warnings[0].Severity);
		Assert.Equal("/ssm/merged/Space Title", snapshot.Entries[0].MountPoint);
		Assert.Equal("mergerfs#disk one", snapshot.Entries[0].Source);
		Assert.Equal("rw,fsname=suwayomi abc", snapshot.Entries[0].Options);
	}

	/// <summary>
	/// Verifies unknown escapes preserve backslashes when decoding captured output.
	/// </summary>
	[Fact]
	public void Capture_Edge_ShouldPreserveBackslashes_WhenEscapeSequenceIsUnknown()
	{
		string output = "TARGET=\"/ssm/merged/Unknown\\qValue\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"source\\qid\" OPTIONS=\"rw\"";
		FakeProcessFacade process = CreateSuccessfulProcess(output, string.Empty);
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Single(snapshot.Entries);
		Assert.Empty(snapshot.Warnings);
		Assert.Equal("/ssm/merged/Unknown\\qValue", snapshot.Entries[0].MountPoint);
		Assert.Equal("source\\qid", snapshot.Entries[0].Source);
	}

	/// <summary>
	/// Verifies lines with values ending in escaped backslashes are parsed without malformed-line warnings.
	/// </summary>
	[Fact]
	public void Capture_Edge_ShouldParseLine_WhenQuotedValueEndsWithEscapedBackslash()
	{
		string output = "TARGET=\"/ssm/merged/Trail\\\\\" FSTYPE=\"fuse.mergerfs\" SOURCE=\"source-id\" OPTIONS=\"rw\"";
		FakeProcessFacade process = CreateSuccessfulProcess(output, string.Empty);
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

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
		FakeProcessFacade process = CreateSuccessfulProcess(output, string.Empty);
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Equal(["/ssm/merged/Alpha", "/ssm/merged/Zeta"], snapshot.Entries.Select(entry => entry.MountPoint).ToArray());
	}

	/// <summary>
	/// Verifies start failures return degraded-visibility warnings with command-failure code.
	/// </summary>
	[Fact]
	public void Capture_Failure_ShouldReturnWarningAndNoEntries_WhenProcessStartFails()
	{
		FakeProcessFacade process = new()
		{
			StartException = new Win32Exception(2)
		};
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-001", snapshot.Warnings[0].Code);
		Assert.Equal(MountSnapshotWarningSeverity.DegradedVisibility, snapshot.Warnings[0].Severity);
		Assert.Contains("StartFailed", snapshot.Warnings[0].Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies timeout outcomes return degraded-visibility warnings with command-failure code.
	/// </summary>
	[Fact]
	public void Capture_Failure_ShouldReturnWarningAndNoEntries_WhenProcessTimesOut()
	{
		FakeProcessFacade process = new()
		{
			StandardOutputReader = new StringReader(string.Empty),
			StandardErrorReader = new StringReader("stalled stderr")
		};
		process.WaitForExitHandler = _ => false;
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromMilliseconds(15),
			TimeSpan.FromMilliseconds(1));

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-001", snapshot.Warnings[0].Code);
		Assert.Equal(MountSnapshotWarningSeverity.DegradedVisibility, snapshot.Warnings[0].Severity);
		Assert.Contains("TimedOut", snapshot.Warnings[0].Message, StringComparison.Ordinal);
		Assert.Equal(1, process.KillCallCount);
	}

	/// <summary>
	/// Verifies non-zero exits return degraded-visibility warnings with command-failure code.
	/// </summary>
	[Fact]
	public void Capture_Failure_ShouldReturnWarningAndNoEntries_WhenProcessExitsNonZero()
	{
		FakeProcessFacade process = CreateSuccessfulProcess(string.Empty, "findmnt error");
		process.ExitCode = 3;
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-001", snapshot.Warnings[0].Code);
		Assert.Equal(MountSnapshotWarningSeverity.DegradedVisibility, snapshot.Warnings[0].Severity);
		Assert.Contains("NonZeroExit", snapshot.Warnings[0].Message, StringComparison.Ordinal);
		Assert.Contains("findmnt error", snapshot.Warnings[0].Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies output capture task faults produce command-failure warnings using fallback diagnostics.
	/// </summary>
	[Fact]
	public void Capture_Failure_ShouldReturnCommandFailure_WhenOutputCaptureTaskFaultsAndStderrIsEmpty()
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new FatalReadLineTextReader(),
			StandardErrorReader = new StringReader(string.Empty)
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-001", snapshot.Warnings[0].Code);
		Assert.Equal(MountSnapshotWarningSeverity.DegradedVisibility, snapshot.Warnings[0].Severity);
		Assert.Contains("NonZeroExit", snapshot.Warnings[0].Message, StringComparison.Ordinal);
		Assert.Contains("findmnt output capture failed", snapshot.Warnings[0].Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Verifies output capture task faults preserve stderr diagnostics when available.
	/// </summary>
	[Fact]
	public void Capture_Failure_ShouldPreserveStderr_WhenOutputCaptureTaskFaults()
	{
		const string standardError = "stderr from findmnt";
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new FatalReadLineTextReader(),
			StandardErrorReader = new StringReader(standardError)
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		FindmntMountSnapshotService service = new(
			() => process,
			TimeSpan.FromSeconds(3),
			TimeSpan.FromMilliseconds(50));

		MountSnapshot snapshot = service.Capture();

		Assert.Empty(snapshot.Entries);
		Assert.Single(snapshot.Warnings);
		Assert.Equal("MOUNT-SNAP-001", snapshot.Warnings[0].Code);
		Assert.Equal(MountSnapshotWarningSeverity.DegradedVisibility, snapshot.Warnings[0].Severity);
		Assert.Contains("NonZeroExit", snapshot.Warnings[0].Message, StringComparison.Ordinal);
		Assert.Contains(standardError, snapshot.Warnings[0].Message, StringComparison.Ordinal);
	}

	/// <summary>
	/// Creates a fake process that exits successfully on first wait probe.
	/// </summary>
	/// <param name="standardOutput">Process standard output text.</param>
	/// <param name="standardError">Process standard error text.</param>
	/// <returns>Configured process facade.</returns>
	private static FakeProcessFacade CreateSuccessfulProcess(string standardOutput, string standardError)
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new StringReader(standardOutput),
			StandardErrorReader = new StringReader(standardError)
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		return process;
	}

	/// <summary>
	/// Test process facade used to script snapshot command behavior without spawning host processes.
	/// </summary>
	private sealed class FakeProcessFacade : IProcessFacade
	{
		/// <summary>
		/// Gets or sets startup exception to throw from <see cref="Start"/>.
		/// </summary>
		public Exception? StartException
		{
			get;
			set;
		}

		/// <summary>
		/// Gets or sets start return value when <see cref="StartException"/> is not set.
		/// </summary>
		public bool StartReturnValue
		{
			get;
			set;
		} = true;

		/// <summary>
		/// Gets or sets handler used by <see cref="WaitForExit"/>.
		/// </summary>
		public Func<int, bool> WaitForExitHandler
		{
			get;
			set;
		} = _ => true;

		/// <inheritdoc />
		public bool HasExited
		{
			get;
			set;
		}

		/// <inheritdoc />
		public int ExitCode
		{
			get;
			set;
		}

		/// <inheritdoc />
		public TextReader StandardOutputReader
		{
			get;
			set;
		} = new StringReader(string.Empty);

		/// <inheritdoc />
		public TextReader StandardErrorReader
		{
			get;
			set;
		} = new StringReader(string.Empty);

		/// <summary>
		/// Gets configured startup settings.
		/// </summary>
		public ProcessStartInfo? ConfiguredStartInfo
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets kill-call count.
		/// </summary>
		public int KillCallCount
		{
			get;
			private set;
		}

		/// <inheritdoc />
		public void ConfigureStartInfo(ProcessStartInfo startInfo)
		{
			ConfiguredStartInfo = startInfo;
		}

		/// <inheritdoc />
		public bool Start()
		{
			if (StartException is not null)
			{
				throw StartException;
			}

			return StartReturnValue;
		}

		/// <inheritdoc />
		public bool WaitForExit(int milliseconds)
		{
			return WaitForExitHandler(milliseconds);
		}

		/// <inheritdoc />
		public void Kill(bool entireProcessTree)
		{
			KillCallCount++;
			HasExited = true;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			StandardOutputReader.Dispose();
			StandardErrorReader.Dispose();
		}
	}

	/// <summary>
	/// Reader that throws a fatal exception to simulate capture task failure.
	/// </summary>
	private sealed class FatalReadLineTextReader : TextReader
	{
		/// <inheritdoc />
		public override string? ReadLine()
		{
			throw new OutOfMemoryException("fatal read failure");
		}
	}
}
