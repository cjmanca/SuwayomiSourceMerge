using System.Text;

namespace SuwayomiSourceMerge.Infrastructure.Logging;

internal sealed class RollingFileSink : ILogSink
{
    private static readonly UTF8Encoding Utf8EncodingWithoutBom = new(false);

    private readonly object _syncRoot = new();
    private readonly string _logFilePath;
    private readonly long _maxFileSizeBytes;
    private readonly int _retainedFileCount;

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

    public void WriteLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        byte[] payload = Utf8EncodingWithoutBom.GetBytes(line + "\n");

        lock (_syncRoot)
        {
            RotateIfNeeded(payload.Length);
            WritePayload(payload);
        }
    }

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

    private string BuildArchivePath(int index)
    {
        return $"{_logFilePath}.{index}";
    }
}
