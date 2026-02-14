namespace SuwayomiSourceMerge.Application.Supervision;

/// <summary>
/// Holds an exclusive process lock file for supervisor single-instance enforcement.
/// </summary>
internal sealed class SupervisorFileLock : IDisposable
{
	/// <summary>
	/// Open file stream whose lifetime controls lock ownership.
	/// </summary>
	private FileStream? _stream;

	/// <summary>
	/// Initializes a new instance of the <see cref="SupervisorFileLock"/> class.
	/// </summary>
	/// <param name="lockFilePath">Lock file path.</param>
	/// <param name="stream">Opened exclusive file stream.</param>
	private SupervisorFileLock(string lockFilePath, FileStream stream)
	{
		LockFilePath = lockFilePath ?? throw new ArgumentNullException(nameof(lockFilePath));
		_stream = stream ?? throw new ArgumentNullException(nameof(stream));
	}

	/// <summary>
	/// Gets the lock-file path.
	/// </summary>
	public string LockFilePath
	{
		get;
	}

	/// <summary>
	/// Acquires an exclusive supervisor lock file.
	/// </summary>
	/// <param name="lockFilePath">Lock-file path.</param>
	/// <returns>Acquired lock handle.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="lockFilePath"/> is invalid.</exception>
	/// <exception cref="IOException">Thrown when the lock is already held by another process.</exception>
	public static SupervisorFileLock Acquire(string lockFilePath)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(lockFilePath);

		FileStream stream = new(
			lockFilePath,
			FileMode.OpenOrCreate,
			FileAccess.ReadWrite,
			FileShare.None);
		return new SupervisorFileLock(lockFilePath, stream);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		FileStream? stream = Interlocked.Exchange(ref _stream, null);
		stream?.Dispose();
	}
}
