using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace SuwayomiSourceMerge.Infrastructure.Watching;

internal sealed partial class PersistentInotifywaitEventReader
{
	/// <summary>
	/// Represents one long-running inotify monitor process session.
	/// </summary>
	private sealed class InotifyMonitorSession : IPersistentInotifyMonitorSession
	{
		/// <summary>
		/// Maximum best-effort wait for background monitor read/exit tasks during session dispose.
		/// </summary>
		private static readonly TimeSpan _backgroundTaskDisposeWaitTimeout = TimeSpan.FromMilliseconds(250);

		private readonly Process _process;
		private readonly ConcurrentQueue<InotifyEventRecord> _events = new();
		private readonly ConcurrentQueue<string> _warnings = new();
		private readonly CancellationTokenSource _disposeTokenSource = new();
		private readonly Task _stdoutTask;
		private readonly Task _stderrTask;
		private readonly Task _exitTask;
		private bool _disposed;

		private InotifyMonitorSession(string watchPath, bool recursive, Process process)
		{
			WatchPath = watchPath;
			IsRecursive = recursive;
			_process = process;
			_stdoutTask = Task.Run(ReadStandardOutputAsync);
			_stderrTask = Task.Run(ReadStandardErrorAsync);
			_exitTask = Task.Run(ObserveExit);
		}

		public string WatchPath
		{
			get;
		}

		public bool IsRecursive
		{
			get;
		}

		public bool IsRunning
		{
			get
			{
				try
				{
					return !_process.HasExited;
				}
				catch
				{
					return false;
				}
			}
		}

		public bool TryDequeueEvent(out InotifyEventRecord record)
		{
			return _events.TryDequeue(out record!);
		}

		public bool TryDequeueWarning(out string warning)
		{
			return _warnings.TryDequeue(out warning!);
		}

		public static bool TryStart(
			string watchPath,
			bool recursive,
			out InotifyMonitorSession? session,
			out SessionStartFailureKind failureKind,
			out string warning)
		{
			session = null;
			failureKind = SessionStartFailureKind.None;
			warning = string.Empty;

			Process process = new();
			process.StartInfo = BuildStartInfo(watchPath, recursive);
			try
			{
				if (!process.Start())
				{
					failureKind = SessionStartFailureKind.CommandFailed;
					warning = $"inotifywait monitor failed to start for '{watchPath}'.";
					process.Dispose();
					return false;
				}

				session = new InotifyMonitorSession(watchPath, recursive, process);
				return true;
			}
			catch (Win32Exception exception)
			{
				if (IsToolNotFoundStartFailure(exception))
				{
					failureKind = SessionStartFailureKind.ToolNotFound;
					warning = $"inotifywait executable was not found while starting monitor for '{watchPath}': {exception.Message}";
				}
				else
				{
					failureKind = SessionStartFailureKind.CommandFailed;
					warning = $"inotifywait monitor startup failed for '{watchPath}': {exception.GetType().Name}: {exception.Message}";
				}

				process.Dispose();
				return false;
			}
			catch (Exception exception)
			{
				failureKind = SessionStartFailureKind.CommandFailed;
				warning = $"inotifywait monitor startup failed for '{watchPath}': {exception.GetType().Name}: {exception.Message}";
				process.Dispose();
				return false;
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			_disposeTokenSource.Cancel();
			try
			{
				if (!_process.HasExited)
				{
					_process.Kill(entireProcessTree: true);
				}
			}
			catch
			{
				// Best-effort session teardown.
			}

			WaitTask(_stdoutTask);
			WaitTask(_stderrTask);
			WaitTask(_exitTask);
			_process.Dispose();
			_disposeTokenSource.Dispose();
		}

		private static ProcessStartInfo BuildStartInfo(string watchPath, bool recursive)
		{
			ProcessStartInfo startInfo = new()
			{
				FileName = "inotifywait",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			};

			startInfo.ArgumentList.Add("-qq");
			startInfo.ArgumentList.Add("-m");
			if (recursive)
			{
				startInfo.ArgumentList.Add("-r");
			}

			for (int index = 0; index < _watchEvents.Length; index++)
			{
				startInfo.ArgumentList.Add("-e");
				startInfo.ArgumentList.Add(_watchEvents[index]);
			}

			startInfo.ArgumentList.Add("--format");
			startInfo.ArgumentList.Add("%w%f|%e");
			startInfo.ArgumentList.Add(watchPath);
			return startInfo;
		}

		private async Task ReadStandardOutputAsync()
		{
			while (!_disposeTokenSource.IsCancellationRequested)
			{
				string? line = await _process.StandardOutput.ReadLineAsync().ConfigureAwait(false);
				if (line is null)
				{
					break;
				}

				if (InotifyEventRecord.TryParse(line, out InotifyEventRecord? record) && record is not null)
				{
					_events.Enqueue(record);
				}
				else
				{
					_warnings.Enqueue("Ignoring malformed inotify monitor output line.");
				}
			}
		}

		private async Task ReadStandardErrorAsync()
		{
			while (!_disposeTokenSource.IsCancellationRequested)
			{
				string? line = await _process.StandardError.ReadLineAsync().ConfigureAwait(false);
				if (line is null)
				{
					break;
				}

				string trimmed = line.Trim();
				if (!string.IsNullOrWhiteSpace(trimmed))
				{
					_warnings.Enqueue(trimmed);
				}
			}
		}

		private void ObserveExit()
		{
			try
			{
				_process.WaitForExit();
				if (!_disposeTokenSource.IsCancellationRequested)
				{
					_warnings.Enqueue(
						string.Create(
							CultureInfo.InvariantCulture,
							$"inotifywait monitor exited unexpectedly for '{WatchPath}' with exit code {_process.ExitCode}."));
				}
			}
			catch (Exception exception)
			{
				if (!_disposeTokenSource.IsCancellationRequested)
				{
					_warnings.Enqueue(
						string.Create(
							CultureInfo.InvariantCulture,
							$"inotifywait monitor exit observer failed for '{WatchPath}': {exception.GetType().Name}: {exception.Message}"));
				}
			}
		}

		private static void WaitTask(Task task)
		{
			try
			{
				// Bound teardown latency so dispose never blocks indefinitely on background readers.
				task.Wait(_backgroundTaskDisposeWaitTimeout);
			}
			catch
			{
				// Best-effort background task wait.
			}
		}
	}

	/// <summary>
	/// Returns whether one process-start Win32 failure indicates a missing executable.
	/// </summary>
	/// <param name="exception">Win32 startup exception.</param>
	/// <returns><see langword="true"/> when startup failure indicates missing executable.</returns>
	internal static bool IsToolNotFoundStartFailure(Win32Exception exception)
	{
		ArgumentNullException.ThrowIfNull(exception);
		if (exception.NativeErrorCode == 2)
		{
			return true;
		}

		return exception.Message.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
			|| exception.Message.Contains("cannot find the file", StringComparison.OrdinalIgnoreCase)
			|| exception.Message.Contains("cannot find the path", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// Classifies monitor-session startup failures.
	/// </summary>
	private enum SessionStartFailureKind
	{
		None,
		ToolNotFound,
		CommandFailed
	}
}
