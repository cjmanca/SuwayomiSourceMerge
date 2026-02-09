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
  level: warning
```

### Validation rules

- Required sections: `paths`, `scan`, `rename`, `diagnostics`, `shutdown`, `permissions`, `runtime`, `logging`
- Required fields: all fields shown in schema (except `unraid_cache_pool_name`, which may be empty)
- Path fields must be absolute paths
- Numeric fields:
  - Must be `>= 0`: `merge_min_seconds_between_scans`, `rename_delay_seconds`, `rename_quiet_seconds`, `debug_timing_min_item_ms`, `debug_scan_progress_every`, `debug_scan_progress_seconds`
  - Must be `> 0`: all other numeric fields
- `details_description_mode` allowed values: `text`, `br`, `html`
- `logging.level` allowed values: `trace`, `debug`, `warning`, `error`, `none`
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
- Each tag must be a non-empty string
- No duplicate tags after case-insensitive normalized comparison

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
| `CFG-MEQ-001` | Missing `groups` in manga equivalents |
| `CFG-MEQ-002` | Missing canonical title |
| `CFG-MEQ-003` | Missing aliases list |
| `CFG-MEQ-004` | Duplicate canonical value after normalization |
| `CFG-MEQ-005` | Alias maps to conflicting canonical values |
| `CFG-MEQ-006` | Empty alias value |
| `CFG-STG-001` | Missing/empty tags list |
| `CFG-STG-002` | Empty scene tag value |
| `CFG-STG-003` | Duplicate scene tag after normalization |
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
