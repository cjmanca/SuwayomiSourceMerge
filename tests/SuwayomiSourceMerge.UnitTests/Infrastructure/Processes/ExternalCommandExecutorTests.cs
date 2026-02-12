namespace SuwayomiSourceMerge.UnitTests.Infrastructure.Processes;

using System.ComponentModel;
using System.Diagnostics;
using SuwayomiSourceMerge.Infrastructure.Processes;

/// <summary>
/// Verifies expected, edge, and failure behavior for <see cref="ExternalCommandExecutor"/>.
/// </summary>
public sealed class ExternalCommandExecutorTests
{
	/// <summary>
	/// Verifies successful command execution returns success outcome and configured process start options.
	/// </summary>
	[Fact]
	public void Execute_Expected_ShouldReturnSuccessAndCaptureOutput_WhenProcessExitsZero()
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new StringReader("stdout-value"),
			StandardErrorReader = new StringReader("stderr-value")
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		ExternalCommandExecutor executor = new(() => process);
		ExternalCommandRequest request = new()
		{
			FileName = "findmnt",
			Arguments = ["-rn", "-P"],
			Timeout = TimeSpan.FromSeconds(5),
			PollInterval = TimeSpan.FromMilliseconds(25),
			MaxOutputCharacters = 128
		};

		ExternalCommandResult result = executor.Execute(request);

		Assert.Equal(ExternalCommandOutcome.Success, result.Outcome);
		Assert.Equal(ExternalCommandFailureKind.None, result.FailureKind);
		Assert.Equal(0, result.ExitCode);
		Assert.Equal("stdout-value", result.StandardOutput);
		Assert.Equal("stderr-value", result.StandardError);
		Assert.False(result.IsStandardOutputTruncated);
		Assert.False(result.IsStandardErrorTruncated);
		Assert.NotNull(process.ConfiguredStartInfo);
		Assert.False(process.ConfiguredStartInfo!.UseShellExecute);
		Assert.True(process.ConfiguredStartInfo.RedirectStandardOutput);
		Assert.True(process.ConfiguredStartInfo.RedirectStandardError);
		Assert.True(process.ConfiguredStartInfo.CreateNoWindow);
		Assert.Equal("findmnt", process.ConfiguredStartInfo.FileName);
		Assert.Equal(["-rn", "-P"], process.ConfiguredStartInfo.ArgumentList);
	}

	/// <summary>
	/// Verifies non-zero process exit maps to <see cref="ExternalCommandOutcome.NonZeroExit"/>.
	/// </summary>
	[Fact]
	public void Execute_Failure_ShouldReturnNonZeroExit_WhenProcessExitCodeIsNotZero()
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 42,
			StandardOutputReader = new StringReader(string.Empty),
			StandardErrorReader = new StringReader("failure")
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "mergerfs",
				Timeout = TimeSpan.FromSeconds(2),
				PollInterval = TimeSpan.FromMilliseconds(20),
				MaxOutputCharacters = 64
			});

		Assert.Equal(ExternalCommandOutcome.NonZeroExit, result.Outcome);
		Assert.Equal(42, result.ExitCode);
		Assert.Equal("failure", result.StandardError);
	}

	/// <summary>
	/// Verifies output exactly at capture capacity is preserved without truncation flags.
	/// </summary>
	[Fact]
	public void Execute_Edge_ShouldNotTruncate_WhenOutputExactlyMatchesMaxCharacters()
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new StringReader("abcde"),
			StandardErrorReader = new StringReader("vwxyz")
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "findmnt",
				Timeout = TimeSpan.FromSeconds(2),
				PollInterval = TimeSpan.FromMilliseconds(10),
				MaxOutputCharacters = 5
			});

		Assert.Equal("abcde", result.StandardOutput);
		Assert.Equal("vwxyz", result.StandardError);
		Assert.False(result.IsStandardOutputTruncated);
		Assert.False(result.IsStandardErrorTruncated);
	}

	/// <summary>
	/// Verifies output above capture capacity is truncated and flagged.
	/// </summary>
	[Fact]
	public void Execute_Edge_ShouldTruncateOutput_WhenOutputExceedsMaxCharacters()
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new StringReader("abcdefgh"),
			StandardErrorReader = new StringReader("uvwxyz")
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "fusermount3",
				Timeout = TimeSpan.FromSeconds(2),
				PollInterval = TimeSpan.FromMilliseconds(10),
				MaxOutputCharacters = 5
			});

		Assert.Equal("abcde", result.StandardOutput);
		Assert.Equal("uvwxy", result.StandardError);
		Assert.True(result.IsStandardOutputTruncated);
		Assert.True(result.IsStandardErrorTruncated);
	}

	/// <summary>
	/// Verifies empty output streams are captured as empty strings.
	/// </summary>
	[Fact]
	public void Execute_Edge_ShouldCaptureEmptyStreams_WhenProcessWritesNothing()
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new StringReader(string.Empty),
			StandardErrorReader = new StringReader(string.Empty)
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "inotifywait",
				Timeout = TimeSpan.FromSeconds(1),
				PollInterval = TimeSpan.FromMilliseconds(10),
				MaxOutputCharacters = 32
			});

		Assert.Equal(string.Empty, result.StandardOutput);
		Assert.Equal(string.Empty, result.StandardError);
		Assert.False(result.IsStandardOutputTruncated);
		Assert.False(result.IsStandardErrorTruncated);
	}

	/// <summary>
	/// Verifies delayed output is fully drained after a normal process exit.
	/// </summary>
	[Fact]
	public void Execute_Edge_ShouldDrainDelayedOutput_WhenProcessExitsSuccessfully()
	{
		FakeProcessFacade process = new()
		{
			ExitCode = 0,
			StandardOutputReader = new DelayedTextReader("delayed-output", 150),
			StandardErrorReader = new StringReader(string.Empty)
		};
		process.WaitForExitHandler = _ =>
		{
			process.HasExited = true;
			return true;
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "findmnt",
				Timeout = TimeSpan.FromSeconds(1),
				PollInterval = TimeSpan.FromMilliseconds(10),
				MaxOutputCharacters = 128
			});

		Assert.Equal(ExternalCommandOutcome.Success, result.Outcome);
		Assert.Equal("delayed-output", result.StandardOutput);
	}

	/// <summary>
	/// Verifies timeout path returns timed-out outcome and kills the process tree.
	/// </summary>
	[Fact]
	public void Execute_Failure_ShouldReturnTimedOutAndKillProcess_WhenTimeoutExpires()
	{
		FakeProcessFacade process = new()
		{
			HasExited = false,
			StandardOutputReader = new StringReader("partial-out"),
			StandardErrorReader = new StringReader("partial-err")
		};
		process.WaitForExitHandler = _ => false;
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "findmnt",
				Timeout = TimeSpan.FromMilliseconds(25),
				PollInterval = TimeSpan.FromMilliseconds(5),
				MaxOutputCharacters = 64
			});

		Assert.Equal(ExternalCommandOutcome.TimedOut, result.Outcome);
		Assert.Equal(ExternalCommandFailureKind.None, result.FailureKind);
		Assert.Null(result.ExitCode);
		Assert.Equal(1, process.KillCallCount);
		Assert.True(process.KillEntireProcessTree);
	}

	/// <summary>
	/// Verifies process exit wins over timeout classification at boundary timing.
	/// </summary>
	[Fact]
	public void Execute_Edge_ShouldPreferExitCodeOverTimeout_WhenProcessExitsAtTimeoutBoundary()
	{
		FakeProcessFacade process = new()
		{
			HasExited = false,
			ExitCode = 0,
			StandardOutputReader = new StringReader(string.Empty),
			StandardErrorReader = new StringReader(string.Empty)
		};
		int waitCount = 0;
		process.WaitForExitHandler = _ =>
		{
			waitCount++;
			if (waitCount == 1)
			{
				process.HasExited = true;
			}

			return false;
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "findmnt",
				Timeout = TimeSpan.FromMilliseconds(1),
				PollInterval = TimeSpan.FromMilliseconds(1),
				MaxOutputCharacters = 64
			});

		Assert.Equal(ExternalCommandOutcome.Success, result.Outcome);
		Assert.Equal(0, result.ExitCode);
		Assert.Equal(0, process.KillCallCount);
	}

	/// <summary>
	/// Verifies cancellation path returns canceled outcome and kills the process tree.
	/// </summary>
	[Fact]
	public void Execute_Failure_ShouldReturnCancelledAndKillProcess_WhenCancellationRequested()
	{
		FakeProcessFacade process = new()
		{
			HasExited = false,
			StandardOutputReader = new StringReader(string.Empty),
			StandardErrorReader = new StringReader(string.Empty)
		};
		process.WaitForExitHandler = _ => false;
		ExternalCommandExecutor executor = new(() => process);
		CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "mergerfs",
				Timeout = TimeSpan.FromSeconds(10),
				PollInterval = TimeSpan.FromMilliseconds(20),
				MaxOutputCharacters = 64
			},
			cancellationTokenSource.Token);

		Assert.Equal(ExternalCommandOutcome.Cancelled, result.Outcome);
		Assert.Equal(ExternalCommandFailureKind.None, result.FailureKind);
		Assert.Null(result.ExitCode);
		Assert.Equal(1, process.KillCallCount);
	}

	/// <summary>
	/// Verifies process exit wins over cancellation classification at boundary timing.
	/// </summary>
	[Fact]
	public void Execute_Edge_ShouldPreferExitCodeOverCancellation_WhenProcessExitsAtCancellationBoundary()
	{
		FakeProcessFacade process = new()
		{
			HasExited = false,
			ExitCode = 0,
			StandardOutputReader = new StringReader(string.Empty),
			StandardErrorReader = new StringReader(string.Empty)
		};
		int waitCount = 0;
		process.WaitForExitHandler = _ =>
		{
			waitCount++;
			if (waitCount == 1)
			{
				process.HasExited = true;
			}

			return false;
		};
		ExternalCommandExecutor executor = new(() => process);
		CancellationTokenSource cancellationTokenSource = new();
		cancellationTokenSource.Cancel();

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "findmnt",
				Timeout = TimeSpan.FromSeconds(5),
				PollInterval = TimeSpan.FromMilliseconds(10),
				MaxOutputCharacters = 64
			},
			cancellationTokenSource.Token);

		Assert.Equal(ExternalCommandOutcome.Success, result.Outcome);
		Assert.Equal(0, result.ExitCode);
		Assert.Equal(0, process.KillCallCount);
	}

	/// <summary>
	/// Verifies missing executable startup failures are classified as tool-not-found.
	/// </summary>
	[Fact]
	public void Execute_Failure_ShouldReturnToolNotFound_WhenProcessStartThrowsMissingExecutable()
	{
		FakeProcessFacade process = new()
		{
			StartException = new Win32Exception(2)
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "missing-command",
				Timeout = TimeSpan.FromSeconds(1),
				PollInterval = TimeSpan.FromMilliseconds(20),
				MaxOutputCharacters = 64
			});

		Assert.Equal(ExternalCommandOutcome.StartFailed, result.Outcome);
		Assert.Equal(ExternalCommandFailureKind.ToolNotFound, result.FailureKind);
		Assert.Null(result.ExitCode);
	}

	/// <summary>
	/// Verifies non-missing startup exceptions are classified as generic start failures.
	/// </summary>
	[Fact]
	public void Execute_Failure_ShouldReturnStartFailure_WhenProcessStartThrowsOtherException()
	{
		FakeProcessFacade process = new()
		{
			StartException = new InvalidOperationException("start failed")
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "findmnt",
				Timeout = TimeSpan.FromSeconds(1),
				PollInterval = TimeSpan.FromMilliseconds(20),
				MaxOutputCharacters = 64
			});

		Assert.Equal(ExternalCommandOutcome.StartFailed, result.Outcome);
		Assert.Equal(ExternalCommandFailureKind.StartFailure, result.FailureKind);
	}

	/// <summary>
	/// Verifies start returning false is treated as startup failure.
	/// </summary>
	[Fact]
	public void Execute_Failure_ShouldReturnStartFailure_WhenProcessStartReturnsFalse()
	{
		FakeProcessFacade process = new()
		{
			StartReturnValue = false
		};
		ExternalCommandExecutor executor = new(() => process);

		ExternalCommandResult result = executor.Execute(
			new ExternalCommandRequest
			{
				FileName = "findmnt",
				Timeout = TimeSpan.FromSeconds(1),
				PollInterval = TimeSpan.FromMilliseconds(20),
				MaxOutputCharacters = 64
			});

		Assert.Equal(ExternalCommandOutcome.StartFailed, result.Outcome);
		Assert.Equal(ExternalCommandFailureKind.StartFailure, result.FailureKind);
	}

	/// <summary>
	/// Verifies null request validation.
	/// </summary>
	[Fact]
	public void Execute_Exception_ShouldThrow_WhenRequestIsNull()
	{
		ExternalCommandExecutor executor = new(() => new FakeProcessFacade());

		Assert.Throws<ArgumentNullException>(() => executor.Execute(null!));
	}

	/// <summary>
	/// Verifies invalid file names are rejected.
	/// </summary>
	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void Execute_Exception_ShouldThrow_WhenFileNameInvalid(string? fileName)
	{
		ExternalCommandExecutor executor = new(() => new FakeProcessFacade());

		Assert.ThrowsAny<ArgumentException>(
			() => executor.Execute(
				new ExternalCommandRequest
				{
					FileName = fileName!,
					Timeout = TimeSpan.FromSeconds(1),
					PollInterval = TimeSpan.FromMilliseconds(10),
					MaxOutputCharacters = 10
				}));
	}

	/// <summary>
	/// Verifies invalid timeout values are rejected.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Execute_Exception_ShouldThrow_WhenTimeoutNotPositive(int milliseconds)
	{
		ExternalCommandExecutor executor = new(() => new FakeProcessFacade());

		Assert.Throws<ArgumentOutOfRangeException>(
			() => executor.Execute(
				new ExternalCommandRequest
				{
					FileName = "findmnt",
					Timeout = TimeSpan.FromMilliseconds(milliseconds),
					PollInterval = TimeSpan.FromMilliseconds(10),
					MaxOutputCharacters = 10
				}));
	}

	/// <summary>
	/// Verifies invalid poll intervals are rejected.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Execute_Exception_ShouldThrow_WhenPollIntervalNotPositive(int milliseconds)
	{
		ExternalCommandExecutor executor = new(() => new FakeProcessFacade());

		Assert.Throws<ArgumentOutOfRangeException>(
			() => executor.Execute(
				new ExternalCommandRequest
				{
					FileName = "findmnt",
					Timeout = TimeSpan.FromSeconds(1),
					PollInterval = TimeSpan.FromMilliseconds(milliseconds),
					MaxOutputCharacters = 10
				}));
	}

	/// <summary>
	/// Verifies invalid max output values are rejected.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	public void Execute_Exception_ShouldThrow_WhenMaxOutputCharactersNotPositive(int maxOutputCharacters)
	{
		ExternalCommandExecutor executor = new(() => new FakeProcessFacade());

		Assert.Throws<ArgumentOutOfRangeException>(
			() => executor.Execute(
				new ExternalCommandRequest
				{
					FileName = "findmnt",
					Timeout = TimeSpan.FromSeconds(1),
					PollInterval = TimeSpan.FromMilliseconds(10),
					MaxOutputCharacters = maxOutputCharacters
				}));
	}

	/// <summary>
	/// Verifies null argument values are rejected.
	/// </summary>
	[Fact]
	public void Execute_Exception_ShouldThrow_WhenArgumentsContainNullItem()
	{
		ExternalCommandExecutor executor = new(() => new FakeProcessFacade());

		Assert.Throws<ArgumentException>(
			() => executor.Execute(
				new ExternalCommandRequest
				{
					FileName = "findmnt",
					Arguments = ["-rn", null!],
					Timeout = TimeSpan.FromSeconds(1),
					PollInterval = TimeSpan.FromMilliseconds(10),
					MaxOutputCharacters = 10
				}));
	}

	/// <summary>
	/// Reader that delays first async read to simulate late-arriving process output.
	/// </summary>
	private sealed class DelayedTextReader : TextReader
	{
		/// <summary>
		/// Backing content returned by the reader.
		/// </summary>
		private readonly string _content;

		/// <summary>
		/// Delay applied before the first read.
		/// </summary>
		private readonly int _delayMilliseconds;

		/// <summary>
		/// Current read position in <see cref="_content"/>.
		/// </summary>
		private int _position;

		/// <summary>
		/// Tracks whether the initial delay has been applied.
		/// </summary>
		private bool _delayApplied;

		/// <summary>
		/// Initializes a new instance of the <see cref="DelayedTextReader"/> class.
		/// </summary>
		/// <param name="content">Text content returned by the reader.</param>
		/// <param name="delayMilliseconds">Delay applied before the first read operation.</param>
		public DelayedTextReader(string content, int delayMilliseconds)
		{
			_content = content ?? throw new ArgumentNullException(nameof(content));
			_delayMilliseconds = delayMilliseconds;
		}

		/// <inheritdoc />
		public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
		{
			if (!_delayApplied)
			{
				_delayApplied = true;
				await Task.Delay(_delayMilliseconds, cancellationToken).ConfigureAwait(false);
			}

			if (_position >= _content.Length)
			{
				return 0;
			}

			int count = Math.Min(buffer.Length, _content.Length - _position);
			_content.AsMemory(_position, count).CopyTo(buffer);
			_position += count;
			return count;
		}
	}

	/// <summary>
	/// Test process facade used to script executor behavior without spawning real host processes.
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
		/// Gets or sets handler used by <see cref="WaitForExit(int)"/>.
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
		/// Gets the configured startup info assigned by the executor.
		/// </summary>
		public ProcessStartInfo? ConfiguredStartInfo
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the number of kill attempts performed by the executor.
		/// </summary>
		public int KillCallCount
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets a value indicating whether the last kill requested full process-tree termination.
		/// </summary>
		public bool KillEntireProcessTree
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
			KillEntireProcessTree = entireProcessTree;
			HasExited = true;
		}

		/// <inheritdoc />
		public void Dispose()
		{
			StandardOutputReader.Dispose();
			StandardErrorReader.Dispose();
		}
	}
}
