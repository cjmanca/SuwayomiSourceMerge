using System.Text;

namespace SuwayomiSourceMerge.IntegrationTests.TestInfrastructure;

/// <summary>
/// Provides a disposable filesystem workspace for one container integration test.
/// </summary>
internal sealed class ContainerFixtureWorkspace : IDisposable
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ContainerFixtureWorkspace"/> class.
	/// </summary>
	public ContainerFixtureWorkspace()
	{
		RootPath = Path.Combine(Path.GetTempPath(), $"ssm-integration-{Guid.NewGuid():N}");
		ConfigRootPath = Path.Combine(RootPath, "config");
		SourcesRootPath = Path.Combine(RootPath, "sources");
		OverrideRootPath = Path.Combine(RootPath, "override");
		MergedRootPath = Path.Combine(RootPath, "merged");
		StateRootPath = Path.Combine(RootPath, "state");
		MockBinPath = Path.Combine(RootPath, "mock-bin");

		Directory.CreateDirectory(ConfigRootPath);
		Directory.CreateDirectory(SourcesRootPath);
		Directory.CreateDirectory(OverrideRootPath);
		Directory.CreateDirectory(MergedRootPath);
		Directory.CreateDirectory(StateRootPath);
		Directory.CreateDirectory(MockBinPath);
	}

	/// <summary>
	/// Gets the workspace root path.
	/// </summary>
	public string RootPath
	{
		get;
	}

	/// <summary>
	/// Gets the config bind-mount path.
	/// </summary>
	public string ConfigRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the sources bind-mount path.
	/// </summary>
	public string SourcesRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the override bind-mount path.
	/// </summary>
	public string OverrideRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the merged bind-mount path.
	/// </summary>
	public string MergedRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the state bind-mount path.
	/// </summary>
	public string StateRootPath
	{
		get;
	}

	/// <summary>
	/// Gets the mock tool bind-mount path.
	/// </summary>
	public string MockBinPath
	{
		get;
	}

	/// <summary>
	/// Writes a valid settings file using supplied sources/override paths.
	/// </summary>
	/// <param name="sourcesRootPath">Container sources root path value.</param>
	/// <param name="overrideRootPath">Container override root path value.</param>
	/// <param name="loggingLevel">Logging level value.</param>
	public void WriteSettingsFile(
		string sourcesRootPath,
		string overrideRootPath,
		string loggingLevel = "debug")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sourcesRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(overrideRootPath);
		ArgumentException.ThrowIfNullOrWhiteSpace(loggingLevel);

		string yaml = $$"""
		paths:
		  config_root_path: /ssm/config
		  sources_root_path: {{sourcesRootPath}}
		  override_root_path: {{overrideRootPath}}
		  merged_root_path: /ssm/merged
		  state_root_path: /ssm/state
		  log_root_path: /ssm/config
		  branch_links_root_path: /ssm/state/.mergerfs-branches
		  unraid_cache_pool_name: ""

		scan:
		  merge_interval_seconds: 3600
		  merge_trigger_poll_seconds: 1
		  merge_min_seconds_between_scans: 0
		  merge_lock_retry_seconds: 1
		  merge_trigger_request_timeout_buffer_seconds: 300
		  watch_startup_mode: progressive
		rename:
		  rename_delay_seconds: 300
		  rename_quiet_seconds: 120
		  rename_poll_seconds: 1
		  rename_rescan_seconds: 172800

		diagnostics:
		  debug_timing: true
		  debug_timing_top_n: 15
		  debug_timing_min_item_ms: 250
		  debug_timing_slow_ms: 5000
		  debug_timing_live: true
		  debug_scan_progress_every: 250
		  debug_scan_progress_seconds: 60
		  debug_comic_info: false
		  timeout_poll_ms: 100
		  timeout_poll_ms_fast: 10

		shutdown:
		  unmount_on_exit: true
		  stop_timeout_seconds: 10
		  child_exit_grace_seconds: 1
		  unmount_command_timeout_seconds: 8
		  unmount_detach_wait_seconds: 5
		  cleanup_high_priority: true
		  cleanup_apply_high_priority: false
		  cleanup_priority_ionice_class: 3
		  cleanup_priority_nice_value: -20

		permissions:
		  inherit_from_parent: true
		  enforce_existing: false
		  reference_path: /ssm/sources

		runtime:
		  low_priority: true
		  startup_cleanup: true
		  rescan_now: true
		  enable_mount_healthcheck: false
		  details_description_mode: text
		  mergerfs_options_base: allow_other,default_permissions,use_ino,category.create=ff,cache.entry=0,cache.attr=0,cache.negative_entry=0
		  excluded_sources:
		    - Local source

		logging:
		  file_name: daemon.log
		  max_file_size_mb: 10
		  retained_file_count: 10
		  level: {{loggingLevel}}
		""";

		WriteConfigFile("settings.yml", yaml);
	}

	/// <summary>
	/// Writes one config file under config root.
	/// </summary>
	/// <param name="fileName">Config file name.</param>
	/// <param name="content">File content.</param>
	public void WriteConfigFile(string fileName, string content)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
		ArgumentNullException.ThrowIfNull(content);

		string path = Path.Combine(ConfigRootPath, fileName);
		File.WriteAllText(
			path,
			content.ReplaceLineEndings("\n"),
			new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
	}

	/// <summary>
	/// Creates default mock external tool scripts for container tests.
	/// </summary>
	public void CreateMockToolScripts()
	{
		WriteExecutableScript(
			"inotifywait",
			"""
			#!/usr/bin/env sh
			LOG_FILE="${MOCK_COMMAND_LOG_PATH:-/ssm/state/mock-commands.log}"
			printf "%s %s\n" "inotifywait" "$*" >> "$LOG_FILE"
			if [ -n "${INOTIFYWAIT_STDOUT:-}" ]; then
			  printf "%b" "${INOTIFYWAIT_STDOUT}"
			fi
			exit "${INOTIFYWAIT_EXIT_CODE:-2}"
			""");

		WriteExecutableScript(
			"findmnt",
			"""
			#!/usr/bin/env sh
			LOG_FILE="${MOCK_COMMAND_LOG_PATH:-/ssm/state/mock-commands.log}"
			printf "%s %s\n" "findmnt" "$*" >> "$LOG_FILE"
			if [ -n "${FINDMNT_STDOUT:-}" ]; then
			  printf "%b" "${FINDMNT_STDOUT}"
			fi
			exit "${FINDMNT_EXIT_CODE:-0}"
			""");

		WriteExecutableScript(
			"mergerfs",
			"""
			#!/usr/bin/env sh
			LOG_FILE="${MOCK_COMMAND_LOG_PATH:-/ssm/state/mock-commands.log}"
			printf "%s %s\n" "mergerfs" "$*" >> "$LOG_FILE"
			exit "${MERGERFS_EXIT_CODE:-0}"
			""");

		WriteExecutableScript(
			"fusermount",
			"""
			#!/usr/bin/env sh
			LOG_FILE="${MOCK_COMMAND_LOG_PATH:-/ssm/state/mock-commands.log}"
			printf "%s %s\n" "fusermount" "$*" >> "$LOG_FILE"
			exit "${FUSERMOUNT_EXIT_CODE:-0}"
			""");

		WriteExecutableScript(
			"fusermount3",
			"""
			#!/usr/bin/env sh
			LOG_FILE="${MOCK_COMMAND_LOG_PATH:-/ssm/state/mock-commands.log}"
			printf "%s %s\n" "fusermount3" "$*" >> "$LOG_FILE"
			exit "${FUSERMOUNT3_EXIT_CODE:-0}"
			""");
	}

	/// <summary>
	/// Writes one custom mock tool script under <see cref="MockBinPath"/>.
	/// </summary>
	/// <param name="name">Script file name.</param>
	/// <param name="content">Script content.</param>
	public void WriteMockToolScript(string name, string content)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentNullException.ThrowIfNull(content);
		WriteExecutableScript(name, content);
	}

	/// <summary>
	/// Disposes workspace directories.
	/// </summary>
	public void Dispose()
	{
		try
		{
			if (Directory.Exists(RootPath))
			{
				Directory.Delete(RootPath, recursive: true);
			}
		}
		catch
		{
			// Best-effort workspace cleanup.
		}
	}

	/// <summary>
	/// Writes one executable script in mock bin path.
	/// </summary>
	/// <param name="name">Script name.</param>
	/// <param name="content">Script content.</param>
	private void WriteExecutableScript(string name, string content)
	{
		string path = Path.Combine(MockBinPath, name);
		File.WriteAllText(path, content.ReplaceLineEndings("\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		EnsureExecutable(path);
	}

	/// <summary>
	/// Ensures executable mode on Unix hosts.
	/// </summary>
	/// <param name="path">File path.</param>
	private static void EnsureExecutable(string path)
	{
		if (OperatingSystem.IsWindows())
		{
			return;
		}

		File.SetUnixFileMode(
			path,
			UnixFileMode.UserRead |
			UnixFileMode.UserWrite |
			UnixFileMode.UserExecute |
			UnixFileMode.GroupRead |
			UnixFileMode.GroupExecute |
			UnixFileMode.OtherRead |
			UnixFileMode.OtherExecute);
	}
}
