namespace SuwayomiSourceMerge.Infrastructure.Watching;

/// <summary>
/// Defines watcher startup behavior for inotify session initialization.
/// </summary>
internal enum InotifyWatchStartupMode
{
	/// <summary>
	/// Starts recursive watcher sessions immediately for each configured watch root.
	/// </summary>
	Full,

	/// <summary>
	/// Starts shallow root watchers first, then incrementally adds recursive child-volume watchers.
	/// </summary>
	Progressive
}
