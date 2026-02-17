namespace SuwayomiSourceMerge.Infrastructure.Mounts;

/// <summary>
/// Represents a point-in-time snapshot of mount entries.
/// </summary>
internal sealed class MountSnapshot
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MountSnapshot"/> class.
	/// </summary>
	/// <param name="entries">Captured mount entries.</param>
	/// <param name="warnings">Non-fatal warnings emitted while capturing the snapshot.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> or <paramref name="warnings"/> is <see langword="null"/>.</exception>
	public MountSnapshot(
		IReadOnlyList<MountSnapshotEntry> entries,
		IReadOnlyList<MountSnapshotWarning> warnings)
	{
		ArgumentNullException.ThrowIfNull(entries);
		ArgumentNullException.ThrowIfNull(warnings);

		Entries = entries.ToArray();
		Warnings = warnings.ToArray();
	}

	/// <summary>
	/// Gets the captured mount entries.
	/// </summary>
	public IReadOnlyList<MountSnapshotEntry> Entries
	{
		get;
	}

	/// <summary>
	/// Gets non-fatal warnings emitted while capturing the snapshot.
	/// </summary>
	public IReadOnlyList<MountSnapshotWarning> Warnings
	{
		get;
	}
}

/// <summary>
/// Represents a single mount entry in a captured snapshot.
/// </summary>
internal sealed class MountSnapshotEntry
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MountSnapshotEntry"/> class.
	/// </summary>
	/// <param name="mountPoint">Absolute mountpoint path.</param>
	/// <param name="fileSystemType">Mounted filesystem type.</param>
	/// <param name="source">Mount source value from the system snapshot.</param>
	/// <param name="options">Mount options value from the system snapshot.</param>
	/// <param name="isHealthy">Optional health probe result for the mounted target.</param>
	/// <exception cref="ArgumentException">Thrown when required string values are null, empty, or whitespace.</exception>
	public MountSnapshotEntry(
		string mountPoint,
		string fileSystemType,
		string source,
		string options,
		bool? isHealthy)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(mountPoint);
		ArgumentException.ThrowIfNullOrWhiteSpace(fileSystemType);
		ArgumentNullException.ThrowIfNull(source);
		ArgumentNullException.ThrowIfNull(options);

		MountPoint = mountPoint;
		FileSystemType = fileSystemType;
		Source = source;
		Options = options;
		IsHealthy = isHealthy;
	}

	/// <summary>
	/// Gets the absolute mountpoint path.
	/// </summary>
	public string MountPoint
	{
		get;
	}

	/// <summary>
	/// Gets the mounted filesystem type.
	/// </summary>
	public string FileSystemType
	{
		get;
	}

	/// <summary>
	/// Gets the mount source value.
	/// </summary>
	public string Source
	{
		get;
	}

	/// <summary>
	/// Gets the mount options value.
	/// </summary>
	public string Options
	{
		get;
	}

	/// <summary>
	/// Gets an optional health indicator supplied by a caller.
	/// </summary>
	public bool? IsHealthy
	{
		get;
	}
}

/// <summary>
/// Represents a non-fatal warning emitted while capturing mount state.
/// </summary>
internal sealed class MountSnapshotWarning
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MountSnapshotWarning"/> class.
	/// </summary>
	/// <param name="code">Stable warning code.</param>
	/// <param name="message">Warning message text.</param>
	/// <param name="severity">Warning severity classification.</param>
	/// <exception cref="ArgumentException">Thrown when required values are null, empty, or whitespace.</exception>
	public MountSnapshotWarning(
		string code,
		string message,
		MountSnapshotWarningSeverity severity = MountSnapshotWarningSeverity.NonFatal)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(code);
		ArgumentException.ThrowIfNullOrWhiteSpace(message);
		if (!Enum.IsDefined(severity))
		{
			throw new ArgumentOutOfRangeException(nameof(severity), severity, "Warning severity must be a defined value.");
		}

		Code = code;
		Message = message;
		Severity = severity;
	}

	/// <summary>
	/// Gets the stable warning code.
	/// </summary>
	public string Code
	{
		get;
	}

	/// <summary>
	/// Gets the warning message text.
	/// </summary>
	public string Message
	{
		get;
	}

	/// <summary>
	/// Gets the warning severity classification.
	/// </summary>
	public MountSnapshotWarningSeverity Severity
	{
		get;
	}
}

/// <summary>
/// Classifies mount snapshot warning severity for downstream safety decisions.
/// </summary>
internal enum MountSnapshotWarningSeverity
{
	/// <summary>
	/// Warning is informational and should not imply degraded mount visibility.
	/// </summary>
	NonFatal,

	/// <summary>
	/// Warning indicates mount visibility may be incomplete or unreliable.
	/// </summary>
	DegradedVisibility
}
