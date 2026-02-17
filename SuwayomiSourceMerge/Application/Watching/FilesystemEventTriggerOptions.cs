using SuwayomiSourceMerge.Configuration.Documents;
using SuwayomiSourceMerge.Infrastructure.Rename;
using SuwayomiSourceMerge.Infrastructure.Watching;

namespace SuwayomiSourceMerge.Application.Watching;

/// <summary>
/// Carries validated settings required by <see cref="FilesystemEventTriggerPipeline"/>.
/// </summary>
internal sealed class FilesystemEventTriggerOptions
{
	/// <summary>
	/// Default additional timeout buffer in seconds for each inotify command request.
	/// </summary>
	private const int DefaultInotifyRequestTimeoutBufferSeconds = 300;

	/// <summary>
	/// Initializes a new instance of the <see cref="FilesystemEventTriggerOptions"/> class.
	/// </summary>
	/// <param name="renameOptions">Rename queue options and excluded-source behavior.</param>
	/// <param name="overrideRootPath">Override root path used for override event routing.</param>
	/// <param name="inotifyPollSeconds">Inotify one-shot polling timeout in seconds.</param>
	/// <param name="mergeIntervalSeconds">Periodic merge request interval in seconds.</param>
	/// <param name="mergeMinSecondsBetweenScans">Minimum seconds between successful merge dispatches.</param>
	/// <param name="mergeLockRetrySeconds">Retry delay in seconds after busy/failure dispatch outcomes.</param>
	/// <param name="startupRenameRescanEnabled">Whether startup rename rescan should run once.</param>
	/// <param name="inotifyRequestTimeoutBufferSeconds">Additional timeout buffer in seconds for each inotify command request.</param>
	/// <param name="watchStartupMode">Watcher startup mode controlling full vs progressive inotify session initialization.</param>
	public FilesystemEventTriggerOptions(
		ChapterRenameOptions renameOptions,
		string overrideRootPath,
		int inotifyPollSeconds,
		int mergeIntervalSeconds,
		int mergeMinSecondsBetweenScans,
		int mergeLockRetrySeconds,
		bool startupRenameRescanEnabled,
		int inotifyRequestTimeoutBufferSeconds = DefaultInotifyRequestTimeoutBufferSeconds,
		InotifyWatchStartupMode watchStartupMode = InotifyWatchStartupMode.Progressive)
	{
		RenameOptions = renameOptions ?? throw new ArgumentNullException(nameof(renameOptions));
		ArgumentException.ThrowIfNullOrWhiteSpace(overrideRootPath);

		if (inotifyPollSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(inotifyPollSeconds), "Inotify poll seconds must be > 0.");
		}

		if (mergeIntervalSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(mergeIntervalSeconds), "Merge interval seconds must be > 0.");
		}

		if (mergeMinSecondsBetweenScans < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(mergeMinSecondsBetweenScans), "Merge minimum seconds between scans must be >= 0.");
		}

		if (mergeLockRetrySeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(mergeLockRetrySeconds), "Merge lock retry seconds must be > 0.");
		}

		if (inotifyRequestTimeoutBufferSeconds <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(inotifyRequestTimeoutBufferSeconds), "Inotify request timeout buffer seconds must be > 0.");
		}

		OverrideRootPath = Path.GetFullPath(overrideRootPath);
		InotifyPollSeconds = inotifyPollSeconds;
		MergeIntervalSeconds = mergeIntervalSeconds;
		MergeMinSecondsBetweenScans = mergeMinSecondsBetweenScans;
		MergeLockRetrySeconds = mergeLockRetrySeconds;
		StartupRenameRescanEnabled = startupRenameRescanEnabled;
		InotifyRequestTimeoutBufferSeconds = inotifyRequestTimeoutBufferSeconds;
		WatchStartupMode = watchStartupMode;
	}

	/// <summary>
	/// Gets rename queue options.
	/// </summary>
	public ChapterRenameOptions RenameOptions
	{
		get;
	}

	/// <summary>
	/// Gets source root path used for source-event routing.
	/// </summary>
	public string SourcesRootPath
	{
		get
		{
			return RenameOptions.SourcesRootPath;
		}
	}

	/// <summary>
	/// Gets override root path used for override-event routing.
	/// </summary>
	public string OverrideRootPath
	{
		get;
	}

	/// <summary>
	/// Gets one-shot inotify polling timeout in seconds.
	/// </summary>
	public int InotifyPollSeconds
	{
		get;
	}

	/// <summary>
	/// Gets periodic merge request interval in seconds.
	/// </summary>
	public int MergeIntervalSeconds
	{
		get;
	}

	/// <summary>
	/// Gets minimum seconds between successful merge dispatches.
	/// </summary>
	public int MergeMinSecondsBetweenScans
	{
		get;
	}

	/// <summary>
	/// Gets retry delay in seconds after busy/failure merge dispatch outcomes.
	/// </summary>
	public int MergeLockRetrySeconds
	{
		get;
	}

	/// <summary>
	/// Gets additional timeout buffer in seconds for each inotify command request.
	/// </summary>
	public int InotifyRequestTimeoutBufferSeconds
	{
		get;
	}

	/// <summary>
	/// Gets watcher startup mode.
	/// </summary>
	public InotifyWatchStartupMode WatchStartupMode
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether startup rename rescan should run once.
	/// </summary>
	public bool StartupRenameRescanEnabled
	{
		get;
	}

	/// <summary>
	/// Builds watcher options from validated settings documents.
	/// </summary>
	/// <param name="settings">Settings document.</param>
	/// <returns>Resolved watcher options.</returns>
	public static FilesystemEventTriggerOptions FromSettings(SettingsDocument settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		if (settings.Paths?.OverrideRootPath is null)
		{
			throw new ArgumentException("Settings paths.override_root_path is required.", nameof(settings));
		}

		if (settings.Scan is null)
		{
			throw new ArgumentException("Settings scan section is required.", nameof(settings));
		}

		if (settings.Runtime?.RescanNow is null)
		{
			throw new ArgumentException("Settings runtime.rescan_now is required.", nameof(settings));
		}

		SettingsScanSection scan = settings.Scan;
		if (!scan.MergeTriggerPollSeconds.HasValue ||
			!scan.MergeIntervalSeconds.HasValue ||
			!scan.MergeMinSecondsBetweenScans.HasValue ||
			!scan.MergeLockRetrySeconds.HasValue ||
			!scan.MergeTriggerRequestTimeoutBufferSeconds.HasValue)
		{
			throw new ArgumentException("Settings scan section contains missing values.", nameof(settings));
		}

		ChapterRenameOptions renameOptions = ChapterRenameOptions.FromSettings(settings);
		return new FilesystemEventTriggerOptions(
			renameOptions,
			settings.Paths.OverrideRootPath,
			scan.MergeTriggerPollSeconds.Value,
			scan.MergeIntervalSeconds.Value,
			scan.MergeMinSecondsBetweenScans.Value,
			scan.MergeLockRetrySeconds.Value,
			settings.Runtime.RescanNow.Value,
			scan.MergeTriggerRequestTimeoutBufferSeconds.Value,
			ParseWatchStartupMode(scan.WatchStartupMode));
	}

	/// <summary>
	/// Parses the watch-startup-mode token.
	/// </summary>
	/// <param name="value">Token value from settings.</param>
	/// <returns>Parsed startup mode.</returns>
	private static InotifyWatchStartupMode ParseWatchStartupMode(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return InotifyWatchStartupMode.Progressive;
		}

		string mode = value.Trim().ToLowerInvariant();
		return mode switch
		{
			"full" => InotifyWatchStartupMode.Full,
			"progressive" => InotifyWatchStartupMode.Progressive,
			_ => throw new ArgumentException("Settings scan.watch_startup_mode must be 'full' or 'progressive'.", nameof(value))
		};
	}
}
