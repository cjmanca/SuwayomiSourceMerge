using SuwayomiSourceMerge.Configuration.Documents;

namespace SuwayomiSourceMerge.Application.Mounting;

/// <summary>
/// Carries validated settings for merge mount workflow execution.
/// </summary>
internal sealed class MergeMountWorkflowOptions
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MergeMountWorkflowOptions"/> class.
	/// </summary>
	/// <param name="sourcesRootPath">Sources root path.</param>
	/// <param name="overrideRootPath">Override root path.</param>
	/// <param name="mergedRootPath">Merged mount root path.</param>
	/// <param name="branchLinksRootPath">Branch-link root path.</param>
	/// <param name="detailsDescriptionMode">details.json description mode.</param>
	/// <param name="mergerfsOptionsBase">Base mergerfs options.</param>
	/// <param name="excludedSources">Excluded source names.</param>
	/// <param name="enableMountHealthcheck">Whether health checks are enabled in reconciliation.</param>
	/// <param name="maxConsecutiveMountFailures">Maximum consecutive mount/remount failures before apply actions fail fast.</param>
	/// <param name="startupCleanupEnabled">Whether startup cleanup is enabled.</param>
	/// <param name="unmountOnExit">Whether shutdown unmount is enabled.</param>
	/// <param name="cleanupHighPriority">Whether cleanup should prefer high-priority wrapper commands.</param>
	/// <param name="cleanupApplyHighPriority">Whether apply-path actions should prefer high-priority wrapper commands.</param>
	/// <param name="cleanupPriorityIoniceClass">Ionice class value for cleanup wrapper execution.</param>
	/// <param name="cleanupPriorityNiceValue">Nice value for cleanup wrapper execution.</param>
	/// <param name="unmountCommandTimeout">Unmount command timeout.</param>
	/// <param name="commandPollInterval">Command polling interval.</param>
	public MergeMountWorkflowOptions(
		string sourcesRootPath,
		string overrideRootPath,
		string mergedRootPath,
		string branchLinksRootPath,
		string detailsDescriptionMode,
		string mergerfsOptionsBase,
		IReadOnlyList<string> excludedSources,
		bool enableMountHealthcheck,
		int maxConsecutiveMountFailures,
		bool startupCleanupEnabled,
		bool unmountOnExit,
		bool cleanupHighPriority,
		bool cleanupApplyHighPriority,
		int cleanupPriorityIoniceClass,
		int cleanupPriorityNiceValue,
		TimeSpan unmountCommandTimeout,
		TimeSpan commandPollInterval)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcesRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(overrideRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(mergedRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(branchLinksRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(detailsDescriptionMode);
		ArgumentException.ThrowIfNullOrWhiteSpace(mergerfsOptionsBase);
		ArgumentNullException.ThrowIfNull(excludedSources);

		if (unmountCommandTimeout <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(unmountCommandTimeout), unmountCommandTimeout, "Unmount timeout must be > 0.");
		}

		if (commandPollInterval <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(commandPollInterval), commandPollInterval, "Command poll interval must be > 0.");
		}

		if (cleanupPriorityIoniceClass < 1 || cleanupPriorityIoniceClass > 3)
		{
			throw new ArgumentOutOfRangeException(nameof(cleanupPriorityIoniceClass), cleanupPriorityIoniceClass, "Ionice class must be between 1 and 3.");
		}

		if (cleanupPriorityNiceValue < -20 || cleanupPriorityNiceValue > 19)
		{
			throw new ArgumentOutOfRangeException(nameof(cleanupPriorityNiceValue), cleanupPriorityNiceValue, "Nice value must be between -20 and 19.");
		}

		if (maxConsecutiveMountFailures <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxConsecutiveMountFailures), maxConsecutiveMountFailures, "Maximum consecutive mount failures must be > 0.");
		}

		SourcesRootPath = Path.GetFullPath(sourcesRootPath);
		OverrideRootPath = Path.GetFullPath(overrideRootPath);
		MergedRootPath = Path.GetFullPath(mergedRootPath);
		BranchLinksRootPath = Path.GetFullPath(branchLinksRootPath);
		DetailsDescriptionMode = detailsDescriptionMode.Trim();
		MergerfsOptionsBase = mergerfsOptionsBase.Trim();
		ExcludedSources = excludedSources
			.Where(static sourceName => !string.IsNullOrWhiteSpace(sourceName))
			.Select(static sourceName => sourceName.Trim())
			.ToArray();
		EnableMountHealthcheck = enableMountHealthcheck;
		MaxConsecutiveMountFailures = maxConsecutiveMountFailures;
		StartupCleanupEnabled = startupCleanupEnabled;
		UnmountOnExit = unmountOnExit;
		CleanupHighPriority = cleanupHighPriority;
		CleanupApplyHighPriority = cleanupApplyHighPriority;
		CleanupPriorityIoniceClass = cleanupPriorityIoniceClass;
		CleanupPriorityNiceValue = cleanupPriorityNiceValue;
		UnmountCommandTimeout = unmountCommandTimeout;
		CommandPollInterval = commandPollInterval;
	}

	/// <summary>
	/// Gets sources root path.
	/// </summary>
	public string SourcesRootPath
	{
		get;
	}

	/// <summary>
	/// Gets override root path.
	/// </summary>
	public string OverrideRootPath
	{
		get;
	}

	/// <summary>
	/// Gets merged mount root path.
	/// </summary>
	public string MergedRootPath
	{
		get;
	}

	/// <summary>
	/// Gets branch-link root path.
	/// </summary>
	public string BranchLinksRootPath
	{
		get;
	}

	/// <summary>
	/// Gets details.json description mode.
	/// </summary>
	public string DetailsDescriptionMode
	{
		get;
	}

	/// <summary>
	/// Gets base mergerfs options.
	/// </summary>
	public string MergerfsOptionsBase
	{
		get;
	}

	/// <summary>
	/// Gets excluded source names.
	/// </summary>
	public IReadOnlyList<string> ExcludedSources
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether mount health checks are enabled.
	/// </summary>
	public bool EnableMountHealthcheck
	{
		get;
	}

	/// <summary>
	/// Gets the maximum consecutive mount/remount failures before apply actions fail fast.
	/// </summary>
	public int MaxConsecutiveMountFailures
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether startup cleanup should run before tick processing.
	/// </summary>
	public bool StartupCleanupEnabled
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether shutdown cleanup should unmount managed mountpoints.
	/// </summary>
	public bool UnmountOnExit
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether cleanup commands should attempt high-priority wrappers.
	/// </summary>
	public bool CleanupHighPriority
	{
		get;
	}

	/// <summary>
	/// Gets a value indicating whether apply-path mount actions should attempt high-priority wrappers.
	/// </summary>
	public bool CleanupApplyHighPriority
	{
		get;
	}

	/// <summary>
	/// Gets ionice class value used for cleanup wrapper execution.
	/// </summary>
	public int CleanupPriorityIoniceClass
	{
		get;
	}

	/// <summary>
	/// Gets nice value used for cleanup wrapper execution.
	/// </summary>
	public int CleanupPriorityNiceValue
	{
		get;
	}

	/// <summary>
	/// Gets per-command unmount timeout.
	/// </summary>
	public TimeSpan UnmountCommandTimeout
	{
		get;
	}

	/// <summary>
	/// Gets process polling interval for external command execution.
	/// </summary>
	public TimeSpan CommandPollInterval
	{
		get;
	}

	/// <summary>
	/// Builds options from validated settings.
	/// </summary>
	/// <param name="settings">Settings document.</param>
	/// <returns>Resolved workflow options.</returns>
	public static MergeMountWorkflowOptions FromSettings(SettingsDocument settings)
	{
		ArgumentNullException.ThrowIfNull(settings);

		if (settings.Paths is null)
		{
			throw new ArgumentException("Settings paths section is required.", nameof(settings));
		}

		if (settings.Runtime is null)
		{
			throw new ArgumentException("Settings runtime section is required.", nameof(settings));
		}

		if (settings.Shutdown is null)
		{
			throw new ArgumentException("Settings shutdown section is required.", nameof(settings));
		}

		if (settings.Diagnostics is null)
		{
			throw new ArgumentException("Settings diagnostics section is required.", nameof(settings));
		}

		SettingsPathsSection paths = settings.Paths;
		SettingsRuntimeSection runtime = settings.Runtime;
		SettingsShutdownSection shutdown = settings.Shutdown;
		SettingsDiagnosticsSection diagnostics = settings.Diagnostics;

		if (paths.SourcesRootPath is null ||
			paths.OverrideRootPath is null ||
			paths.MergedRootPath is null ||
			paths.BranchLinksRootPath is null)
		{
			throw new ArgumentException(
				"Settings paths.sources_root_path, paths.override_root_path, paths.merged_root_path, and paths.branch_links_root_path are required.",
				nameof(settings));
		}

		if (runtime.EnableMountHealthcheck is null ||
			runtime.MaxConsecutiveMountFailures is null ||
			runtime.DetailsDescriptionMode is null ||
			runtime.MergerfsOptionsBase is null ||
			runtime.StartupCleanup is null)
		{
			throw new ArgumentException(
				"Settings runtime.enable_mount_healthcheck, runtime.max_consecutive_mount_failures, runtime.details_description_mode, runtime.mergerfs_options_base, and runtime.startup_cleanup are required.",
				nameof(settings));
		}

		if (shutdown.UnmountOnExit is null ||
			shutdown.UnmountCommandTimeoutSeconds is null ||
			shutdown.CleanupHighPriority is null ||
			shutdown.CleanupApplyHighPriority is null ||
			shutdown.CleanupPriorityIoniceClass is null ||
			shutdown.CleanupPriorityNiceValue is null)
		{
			throw new ArgumentException(
				"Settings shutdown.unmount_on_exit, shutdown.unmount_command_timeout_seconds, shutdown.cleanup_high_priority, shutdown.cleanup_apply_high_priority, shutdown.cleanup_priority_ionice_class, and shutdown.cleanup_priority_nice_value are required.",
				nameof(settings));
		}

		if (diagnostics.TimeoutPollMsFast is null)
		{
			throw new ArgumentException("Settings diagnostics.timeout_poll_ms_fast is required.", nameof(settings));
		}

		return new MergeMountWorkflowOptions(
			paths.SourcesRootPath,
			paths.OverrideRootPath,
			paths.MergedRootPath,
			paths.BranchLinksRootPath,
			runtime.DetailsDescriptionMode,
			runtime.MergerfsOptionsBase,
			runtime.ExcludedSources ?? [],
			runtime.EnableMountHealthcheck.Value,
			runtime.MaxConsecutiveMountFailures.Value,
			runtime.StartupCleanup.Value,
			shutdown.UnmountOnExit.Value,
			shutdown.CleanupHighPriority.Value,
			shutdown.CleanupApplyHighPriority.Value,
			shutdown.CleanupPriorityIoniceClass.Value,
			shutdown.CleanupPriorityNiceValue.Value,
			TimeSpan.FromSeconds(shutdown.UnmountCommandTimeoutSeconds.Value),
			TimeSpan.FromMilliseconds(diagnostics.TimeoutPollMsFast.Value));
	}
}
