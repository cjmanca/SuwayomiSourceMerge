namespace SuwayomiSourceMerge.Infrastructure.Logging;

/// <summary>
/// Represents supported runtime log severities in ascending order.
/// </summary>
/// <remarks>
/// The numeric ordering is used for threshold comparisons:
/// lower values are more verbose; higher values are more restrictive.
/// </remarks>
internal enum LogLevel
{
	/// <summary>
	/// Highly verbose diagnostic detail intended for deep troubleshooting.
	/// </summary>
	Trace = 0,

	/// <summary>
	/// Debug-level information useful during development or focused investigation.
	/// </summary>
	Debug = 1,

	/// <summary>
	/// General runtime status information for normal operation visibility.
	/// </summary>
	Normal = 2,

	/// <summary>
	/// Non-fatal issues or noteworthy conditions that should be reviewed.
	/// </summary>
	Warning = 3,

	/// <summary>
	/// Failure conditions that indicate an operation did not complete successfully.
	/// </summary>
	Error = 4,

	/// <summary>
	/// Sentinel level that disables normal file log emission.
	/// </summary>
	/// <remarks>
	/// This value is intended for configuration thresholding and should not be emitted as an event level.
	/// </remarks>
	None = 5
}
