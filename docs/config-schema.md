# Configuration Schema

This document defines the canonical YAML schema and deterministic validation rules for:

- `settings.yml`
- `manga_equivalents.yml`
- `scene_tags.yml`
- `source_priority.yml`

Validation mode is fail-fast. Startup must abort if any document has validation errors.

## settings.yml

### Schema

```yaml
paths:
  config_root_path: /ssm/config
  sources_root_path: /ssm/sources
  override_root_path: /ssm/override
  merged_root_path: /ssm/merged
  state_root_path: /ssm/state
  log_root_path: /ssm/config
  branch_links_root_path: /ssm/state/.mergerfs-branches
  unraid_cache_pool_name: ""

scan:
  merge_interval_seconds: 3600
  merge_trigger_poll_seconds: 5
  merge_min_seconds_between_scans: 15
  merge_lock_retry_seconds: 30
  watch_startup_mode: progressive
rename:
  rename_delay_seconds: 300
  rename_quiet_seconds: 120
  rename_poll_seconds: 20
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
  stop_timeout_seconds: 120
  child_exit_grace_seconds: 5
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
  max_consecutive_mount_failures: 5
  comick_metadata_cooldown_hours: 24
  metadata_api_request_delay_ms: 1000
  metadata_api_cache_ttl_hours: 24
  flaresolverr_server_url: ""
  flaresolverr_direct_retry_minutes: 60
  preferred_language: en
  details_description_mode: text
  mergerfs_options_base: allow_other,default_permissions,use_ino,threads=1,category.create=ff,cache.entry=0,cache.attr=0,cache.negative_entry=0
  excluded_sources:
    - Local source

logging:
  file_name: daemon.log
  max_file_size_mb: 10
  retained_file_count: 10
  level: normal
```

### Validation rules

- Required sections: `paths`, `scan`, `rename`, `diagnostics`, `shutdown`, `permissions`, `runtime`, `logging`
- Required fields: all fields shown in schema (except `unraid_cache_pool_name`, which may be empty)
- Path fields must be absolute paths
- Numeric fields:
  - Must be `>= 0`: `merge_min_seconds_between_scans`, `rename_delay_seconds`, `rename_quiet_seconds`, `debug_timing_min_item_ms`, `debug_scan_progress_every`, `debug_scan_progress_seconds`, `runtime.metadata_api_request_delay_ms`
  - Must be `> 0`: all other numeric fields except those with explicit bounded ranges
  - Must be in range `1..3`: `shutdown.cleanup_priority_ionice_class`
  - Must be in range `-20..19`: `shutdown.cleanup_priority_nice_value`
- `scan.watch_startup_mode` allowed values: `full`, `progressive` (default: `progressive`)
- `scan.merge_trigger_request_timeout_buffer_seconds` is optional, accepted for backward compatibility, and deprecated for runtime use; the persistent inotify monitor path ignores this value and emits a startup warning when present.
- `scan.merge_trigger_request_timeout_buffer_seconds` is not self-healed back into existing `settings.yml` files when omitted.
- `shutdown.cleanup_high_priority` controls startup/shutdown cleanup wrapper execution.
- `shutdown.cleanup_apply_high_priority` controls reconciliation apply-path wrapper execution.
- `runtime.max_consecutive_mount_failures` controls merge-pass apply fail-fast behavior after repeated mount/remount failures.
- Runtime bootstrap/settings parse (`ConfigurationSchemaService.ParseSettingsForRuntime`) uses strict validation for shutdown cleanup profile fields (`cleanup_apply_high_priority`, `cleanup_priority_ionice_class`, `cleanup_priority_nice_value`).
- Runtime bootstrap/settings parse (`ConfigurationSchemaService.ParseSettingsForRuntime`) also requires `runtime.comick_metadata_cooldown_hours`, `runtime.metadata_api_request_delay_ms`, `runtime.metadata_api_cache_ttl_hours`, `runtime.flaresolverr_server_url`, `runtime.flaresolverr_direct_retry_minutes`, and `runtime.preferred_language`.
- Tooling/schema-only settings parse (`ConfigurationSchemaService.ParseSettingsForTooling`) may omit those shutdown cleanup profile fields and the Comick/FlareSolverr/metadata API runtime fields; when provided, numeric ranges and URL/token constraints are still validated.
- `runtime.comick_metadata_cooldown_hours` and `runtime.flaresolverr_direct_retry_minutes` must be `> 0` when required or provided.
- `runtime.metadata_api_request_delay_ms` must be `>= 0` when required or provided.
- `runtime.metadata_api_cache_ttl_hours` must be `> 0` when required or provided.
- `runtime.flaresolverr_server_url` may be empty; when non-empty it must be an absolute `http` or `https` URI.
- `runtime.preferred_language` must be non-empty when required or provided.
- Settings self-heal canonicalizes `runtime.preferred_language` and `runtime.flaresolverr_server_url` by trimming surrounding whitespace when present; `runtime.preferred_language` falls back to default `en` when canonicalization yields an empty value.
- `details_description_mode` allowed values: `text`, `br`, `html`
- `logging.file_name` must be a single file name (no rooted path, directory separators, or traversal segments)
- `logging.file_name` must not contain cross-platform strict invalid file-name characters (`U+0000`-`U+001F`, `<`, `>`, `:`, `"`, `/`, `\`, `|`, `?`, `*`)
- `logging.file_name` must not end with `.` or space
- On Windows hosts, `logging.file_name` must not resolve to a reserved device name (`CON`, `PRN`, `AUX`, `NUL`, `COM1`-`COM9`, `LPT1`-`LPT9`)
- Entrypoint privileged log-file ownership adjustment is restricted to paths resolved under `/ssm/config`; custom absolute `paths.log_root_path` values remain valid but skip root-run ownership updates when outside that trusted root.
- `logging.level` allowed values: `trace`, `debug`, `normal`, `warning`, `error`, `none`
- `excluded_sources` cannot contain empty items or duplicates after normalization

## manga_equivalents.yml

### Schema

```yaml
groups:
  - canonical: Manga Title 1
    aliases:
      - Manga Title One
      - The Manga Title 1
  - canonical: Another Manga
    aliases: []
```

### Validation rules

- `groups` is required
- Each group must have non-empty `canonical`
- Each group must have `aliases` list (can be empty)
- No duplicate canonical entries after normalization
- No alias may map to conflicting canonical values after normalization

## scene_tags.yml

### Schema

```yaml
tags:
  - official
  - color
  - asura scan
```

### Validation rules

- `tags` is required and must contain at least one item
- Each tag must be a non-empty, non-whitespace string
- Punctuation-only tags are valid
- No duplicate tags after matcher-equivalent key comparison:
  - Text/mixed tags use normalized token keys
  - Punctuation-only tags use exact trimmed punctuation sequence keys

### Processing semantics

- Scene-tag stripping is applied before punctuation removal, whitespace collapsing, and ASCII folding.
- Scene-tag matching ignores punctuation differences for text/mixed tags.
- Punctuation-only tags match exact trimmed punctuation sequences.
- Punctuation removal/whitespace normalization/ASCII folding are comparison-key transforms only.
- Final merged directory display names preserve punctuation except where matched scene-tag suffixes are stripped.

## source_priority.yml

### Schema

```yaml
sources:
  - Source Name A
  - Source Name B
```

### Validation rules

- `sources` list is required
- Items must be non-empty strings
- No duplicate source names after case-insensitive normalized comparison
- Order in list is priority order (top to bottom)
- Runtime source-priority lookups are normalized-only (case/punctuation-insensitive token matching)

## Validation codes

| Code | Meaning |
|---|---|
| `CFG-YAML-001` | YAML parse/type/shape failure |
| `CFG-YAML-002` | YAML document is empty |
| `CFG-SET-001` | Missing required settings section |
| `CFG-SET-002` | Missing required settings field |
| `CFG-SET-003` | Invalid path field (not absolute) |
| `CFG-SET-004` | Numeric range violation in settings |
| `CFG-SET-005` | Invalid enum-like settings value |
| `CFG-SET-006` | Duplicate list item in settings |
| `CFG-SET-007` | Invalid `logging.file_name` value |
| `CFG-MEQ-001` | Missing `groups` in manga equivalents |
| `CFG-MEQ-002` | Missing canonical title |
| `CFG-MEQ-003` | Missing aliases list |
| `CFG-MEQ-004` | Duplicate canonical value after normalization |
| `CFG-MEQ-005` | Alias maps to conflicting canonical values |
| `CFG-MEQ-006` | Empty alias value |
| `CFG-STG-001` | Missing/empty tags list |
| `CFG-STG-002` | Empty scene tag value |
| `CFG-STG-003` | Duplicate scene tag after matcher-equivalent normalization |
| `CFG-SRC-001` | Missing sources list |
| `CFG-SRC-002` | Empty source name |
| `CFG-SRC-003` | Duplicate source name after normalization |

## Migration notes (legacy txt to YAML)

- `manga_equivalents.txt` (`Canonical|Alias1|Alias2`) -> `manga_equivalents.yml`:
  - First token becomes `canonical`
  - Remaining tokens become `aliases`
- `source_priority.txt` (one source per line) -> `source_priority.yml`:
  - Lines become ordered `sources` entries
- `scene_tags.yml` and `settings.yml`:
  - If missing, generate defaults from in-app defaults
- If `manga_equivalents.yml` or `source_priority.yml` is missing and no legacy txt exists:
  - Generated defaults include one starter example entry to make manual editing easier
