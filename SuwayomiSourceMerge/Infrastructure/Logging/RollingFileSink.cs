using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// File-based sink that appends lines to a primary log file and rotates archives by size.
/// </summary>
/// <remarks>
/// Rotation uses numbered suffixes (<c>.1</c> through <c>.N</c>) with bounded retention.
/// Writes are synchronized for thread-safe concurrent logging.
/// </remarks>
internal sealed class RollingFileSink : ILogSink
{
	/// <summary>
	/// UTF-8 encoding without BOM for deterministic log file output.
	/// </summary>
	private static readonly UTF8Encoding _utf8EncodingWithoutBom = new(false);

	/// <summary>
	/// Process-local lock used to serialize rotation and write operations.
	/// </summary>
	private readonly object _syncRoot = new();

	/// <summary>
	/// Full path to the active log file.
	/// </summary>
	private readonly string _logFilePath;

	/// <summary>
	/// Maximum allowed size of the active file before rotation.
	/// </summary>
	private readonly long _maxFileSizeBytes;

	/// <summary>
	/// Number of archive files to retain.
	/// </summary>
	private readonly int _retainedFileCount;

	/// <summary>
	/// Creates a rolling file sink.
	/// </summary>
	/// <param name="logFilePath">Full path to the active log file.</param>
	/// <param name="maxFileSizeBytes">Maximum allowed active file size in bytes.</param>
	/// <param name="retainedFileCount">Number of rotated archives to keep.</param>
	/// <exception cref="ArgumentException">Thrown when <paramref name="logFilePath"/> is null, empty, or whitespace.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxFileSizeBytes"/> or <paramref name="retainedFileCount"/> is not greater than zero.
	/// </exception>
	public RollingFileSink(string logFilePath, long maxFileSizeBytes, int retainedFileCount)
	{
		_logFilePath = string.IsNullOrWhiteSpace(logFilePath)
			? throw new ArgumentException("Log file path is required.", nameof(logFilePath))
			: logFilePath;

		if (maxFileSizeBytes <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxFileSizeBytes), "Log file size limit must be greater than 0.");
		}

		if (retainedFileCount <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(retainedFileCount), "Retained file count must be greater than 0.");
		}

		_maxFileSizeBytes = maxFileSizeBytes;
		_retainedFileCount = retainedFileCount;
	}

	/// <summary>
	/// Appends a line to the active log file, rotating archives when needed.
	/// </summary>
	/// <param name="line">Formatted log line payload.</param>
	public void WriteLine(string line)
	{
		ArgumentNullException.ThrowIfNull(line);

		byte[] payload = _utf8EncodingWithoutBom.GetBytes(line + "\n");

		lock (_syncRoot)
		{
			RotateIfNeeded(payload.Length);
			WritePayload(payload);
		}
	}

	/// <summary>
	/// Rotates the active file if appending the next payload would exceed the configured size.
	/// </summary>
	/// <param name="nextWriteByteLength">Byte length of the pending write payload.</param>
	private void RotateIfNeeded(int nextWriteByteLength)
	{
		long currentLength = File.Exists(_logFilePath)
			? new FileInfo(_logFilePath).Length
			: 0;

		if (currentLength + nextWriteByteLength <= _maxFileSizeBytes)
		{
			return;
		}

		if (!File.Exists(_logFilePath))
		{
			return;
		}

		string oldestArchivePath = BuildArchivePath(_retainedFileCount);
		if (File.Exists(oldestArchivePath))
		{
			File.Delete(oldestArchivePath);
		}

		for (int archiveIndex = _retainedFileCount - 1; archiveIndex >= 1; archiveIndex--)
		{
			string sourcePath = BuildArchivePath(archiveIndex);
			if (!File.Exists(sourcePath))
			{
				continue;
			}

			string destinationPath = BuildArchivePath(archiveIndex + 1);
			File.Move(sourcePath, destinationPath, true);
		}

		File.Move(_logFilePath, BuildArchivePath(1), true);
	}

	/// <summary>
	/// Writes raw bytes to the active log file, ensuring parent directory existence.
	/// </summary>
	/// <param name="payload">Encoded line payload including newline terminator.</param>
	private void WritePayload(byte[] payload)
	{
		string? directoryPath = Path.GetDirectoryName(_logFilePath);
		if (!string.IsNullOrWhiteSpace(directoryPath))
		{
			Directory.CreateDirectory(directoryPath);
		}

		using FileStream stream = new(
			_logFilePath,
			FileMode.Append,
			FileAccess.Write,
			FileShare.Read);

		stream.Write(payload, 0, payload.Length);
	}

	/// <summary>
	/// Builds an archive file path for the specified rotation index.
	/// </summary>
	/// <param name="index">1-based archive index.</param>
	/// <returns>Archive path derived from the active log file path.</returns>
	private string BuildArchivePath(int index)
	{
		return $"{_logFilePath}.{index}";
	}
}
