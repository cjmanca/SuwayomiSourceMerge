#!/usr/bin/env bash
set -euo pipefail
IFS=$'\n\t'

################################################################################
# Helper script for Suwayomi on unraid.
# Sets up mergerFS to link the same manga's from different sources into a combined local source.
# Also renames chapters with release groups containing numbers, since the numbers confuse suwayomi's local chapter ordering.
# Uses inotify to detect new sources/mangas/chapters as long as the script continues to run.
################################################################################


################################################################################
# CONFIG (EDIT THESE)
################################################################################

LOG_DIR="/mnt/user/data/media/manga/daemon"

# Downloads: /<source>/<manga>/<chapter>/<pages>.png
DOWNLOAD_ROOT="/mnt/user/data/media/manga/downloads/mangas"

# Unraid write cache pool name (if using an SSD to buffer new writes before mover moves to array)
# Leave empty if not on unraid or not using a write cache pool
UNRAID_CACHE_POOL="raid"

# Output union mountpoints:
LOCAL_ROOT="/mnt/suwayomi_manga_daemon/local-manga"

# Local overrides branch (RW) per manga title:
OVERRIDE_ROOT="/mnt/user/data/media/manga/local-override"

# Internal: symlink-based branch directories (avoids ':' issues in mergerfs branch strings)
BRANCHLINK_ROOT="/mnt/suwayomi_manga_daemon/.mergerfs-branches"

# Optional: canonical title equivalences file, format:
# Canonical Title|Alias 1|Alias 2|...
EQUIV_FILE="/mnt/user/data/media/manga/manga_equivalents.txt"

# Optional: source priority file, one source name per line (top = highest priority)
PRIORITY_FILE="/mnt/user/data/media/manga/source_priority.txt"

# MergerFS options
# NOTE: We set a per-manga fsname (suwayomi_<branchdirid>_<hash>) when mounting.
#       This avoids FUSE option parsing issues when titles contain commas, and it gives us a stable
#       change-detection key (we compare findmnt SOURCE to the desired fsname).
MERGERFS_OPTS_BASE="allow_other,default_permissions,use_ino,category.create=ff,cache.entry=0,cache.attr=0,cache.negative_entry=0"
# NOTE: cache.entry/cache.attr set to 0 so changes made directly in underlying branches (e.g. local-override) appear immediately in the union view without needing a remount.
# Sources to ignore under DOWNLOAD_ROOT (avoids Suwayomi "Local source" feedback loop)
EXCLUDED_SOURCES=("Local source")

# Timing/profiling (helps diagnose slow startup/rescans)
DEBUG_TIMING=1                 # 1=emit per-scan timing summaries to the log
DEBUG_TIMING_TOP_N=15          # list this many "slowest" items per category
DEBUG_TIMING_MIN_ITEM_MS=250   # ignore items faster than this in "slowest" lists
DEBUG_TIMING_SLOW_MS=5000      # emit extra detail logs when a single op exceeds this (ms)
DEBUG_TIMING_LIVE=1            # 1=emit slow-op timing lines as they happen (not just summary)
DEBUG_SCAN_PROGRESS_EVERY=250   # log progress every N titles during a scan (0=disable)
DEBUG_SCAN_PROGRESS_SECONDS=60  # log progress at least every N seconds during a scan (0=disable)

# run_cmd_timeout polling interval (ms). Lower = less overhead for short commands; higher = less CPU.
TIMEOUT_POLL_MS=100
TIMEOUT_POLL_MS_FAST=10  # used for fast file ops (mkdir/ln/rm/mv); keeps overhead low without changing safety-critical polling

# Optional mount "health" checks.
#
# Background: Suwayomi keeps these mounts busy (open files / inotify / scanning), so unmounts can
# legitimately fail with EBUSY. If the health check is too aggressive (or flaky due to transient
# IO stalls), every scan can try to remount everything, fail to unmount, and spam logs.
#
# Default is OFF. Enable only when you suspect a stuck FUSE mount and want the daemon to try to
# self-heal on the next scan.
ENABLE_MOUNT_HEALTHCHECK="${ENABLE_MOUNT_HEALTHCHECK:-0}"

# Internal: when we detect a stale/broken mountpoint (e.g. FUSE "Transport endpoint is not connected"),
# we flag it here so the per-title logic will force an unmount+remount even if the initial findmnt snapshot misses it.
declare -gA FORCE_REMOUNT_MP=()


################################################################################
# Normalize root paths + watcher roots
################################################################################

DOWNLOAD_ROOT="${DOWNLOAD_ROOT%/}"
LOCAL_ROOT="${LOCAL_ROOT%/}"
OVERRIDE_ROOT="${OVERRIDE_ROOT%/}"
BRANCHLINK_ROOT="${BRANCHLINK_ROOT%/}"
LOG_DIR="${LOG_DIR%/}"
LOG_DIR_REAL="$LOG_DIR"

# On Unraid, mounting a FUSE filesystem (mergerfs) *under* /mnt/user (also FUSE)
# can be flaky or hang. Prefer using the cache-pool path as the *actual* mount
# target when possible; /mnt/user will still reflect the mount via the user-share.
LOCAL_ROOT_REAL="$LOCAL_ROOT"
OVERRIDE_ROOT_REAL="$OVERRIDE_ROOT"
BRANCHLINK_ROOT_REAL="$BRANCHLINK_ROOT"
if [[ -n "${UNRAID_CACHE_POOL:-}" && -d "/mnt/${UNRAID_CACHE_POOL}" ]]; then
  if [[ "$LOCAL_ROOT" == /mnt/user/* ]]; then
    LOCAL_ROOT_REAL="/mnt/${UNRAID_CACHE_POOL}${LOCAL_ROOT#/mnt/user}"
  fi
  if [[ "$OVERRIDE_ROOT" == /mnt/user/* ]]; then
    OVERRIDE_ROOT_REAL="/mnt/${UNRAID_CACHE_POOL}${OVERRIDE_ROOT#/mnt/user}"
  fi
  if [[ "$BRANCHLINK_ROOT" == /mnt/user/* ]]; then
    BRANCHLINK_ROOT_REAL="/mnt/${UNRAID_CACHE_POOL}${BRANCHLINK_ROOT#/mnt/user}"
  fi
  if [[ "$LOG_DIR" == /mnt/user/* ]]; then
    LOG_DIR_REAL="/mnt/${UNRAID_CACHE_POOL}${LOG_DIR#/mnt/user}"
  fi
fi


is_tag_suffix() {
  # Determine whether a trailing bracket/tag/suffix should be stripped.
  # IMPORTANT: match whole tokens/phrases, not substrings (e.g. 'es' inside 'classes').
  local s="${1-}"
  s="$(trim_spaces "$s")"
  s="${s,,}"

  local re='(^|[^[:alnum:]])(official|color|colour|colorized|colourized|colored|coloured|uncensored|censored|asura scan|asura scans|team argo|tapas official|valir scans|digital|webtoon|web|scan|scans|scanlation|hq|hd|raw|raws|manga|manhwa|manhua|comic|translated|translation|english|eng|en|jpn|jp|kor|kr|chi|cn|fr|es|de|pt|it|ru)($|[^[:alnum:]])'
  [[ "$s" =~ $re ]] && return 0

  # common compact forms
  [[ "$s" =~ ^[[:space:]]*(en|eng|jp|jpn|kr|kor|cn|chi|raw|hd|hq)[[:space:]]*$ ]] && return 0
  [[ "$s" =~ ^[[:space:]]*(vol|volume|v)[[:space:]]*[0-9]+[[:space:]]*$ ]] && return 0
  return 1
}

# Unraid shfs (FUSE at /mnt/user) can deadlock if a FUSE filesystem (mergerfs) mounted on the cache/array
# is visible through /mnt/user and ALSO uses /mnt/user paths as its branch sources. In that case, shfs may
# call into mergerfs which calls back into shfs, and /mnt/user can become unresponsive.
#
# To avoid this, we NEVER use /mnt/user paths as mergerfs branch sources. We resolve them to the real cache-pool
# path (/mnt/<pool>/...) or to the first matching /mnt/diskX/... path.
resolve_user_path_to_real() {
  local p="${1:-}"
  [[ -n "$p" ]] || return 1
  if [[ "$p" != /mnt/user/* ]]; then
    printf '%s' "$p"
    return 0
  fi

  local rel="${p#/mnt/user/}"
  local cand=""

  if [[ -n "${UNRAID_CACHE_POOL:-}" && -d "/mnt/${UNRAID_CACHE_POOL}" ]]; then
    cand="/mnt/${UNRAID_CACHE_POOL}/${rel}"
    [[ -e "$cand" ]] && { printf '%s' "$cand"; return 0; }
  fi

  local d
  for d in /mnt/disk*; do
    [[ -d "$d" ]] || continue
    cand="$d/$rel"
    [[ -e "$cand" ]] && { printf '%s' "$cand"; return 0; }
  done

  # Fallback: keep original (best-effort). This should be rare.
  printf '%s' "$p"
  return 0
}

# List all "real" filesystem paths for a /mnt/user/... directory.
# - Includes the cache pool (/mnt/$UNRAID_CACHE_POOL) if present
# - Includes any /mnt/disk* locations that exist
# This avoids relying solely on /mnt/user (FUSE), which can be flaky on Unraid.
#
# Performance note: we keep a sorted list of /mnt/disk* mountpoints and refresh it once per scan,
# so per-manga processing stays fast (no per-call external sort/awk).
DISK_MOUNTS_SORTED=()

init_disk_mounts() {
  local -a _disks=()
  local d
  for d in /mnt/disk*; do
    [[ -d "$d" ]] && _disks+=("$d")
  done
  if (( ${#_disks[@]} )); then
    # Natural sort (disk2 before disk10). One sort per scan only.
    mapfile -t DISK_MOUNTS_SORTED < <(printf '%s\n' "${_disks[@]}" | sort -V)
  else
    DISK_MOUNTS_SORTED=()
  fi
}

list_real_dirs_for_user_dir() {
  local p="$1"
  [[ "$p" != /mnt/user/* ]] && { printf '%s\n' "$p"; return 0; }
  local rel="${p#/mnt/user/}"
  local -a out=()
  local pool="/mnt/${UNRAID_CACHE_POOL:-cache}"
  [[ -d "$pool/$rel" ]] && out+=("$pool/$rel")

  local d cand
  for d in "${DISK_MOUNTS_SORTED[@]}"; do
    cand="$d/$rel"
    [[ -d "$cand" ]] && out+=("$cand")
  done

  if (( ${#out[@]} == 0 )); then
    printf '%s\n' "$p"
  else
    printf '%s\n' "${out[@]}"
  fi
}




# Resolve the per-title override directories to *real* paths (cache-pool + any /mnt/diskX),
# avoiding /mnt/user (shfs) in mergerfs branch strings.
#
# Policy:
#   - The cache-pool path (UNRAID_CACHE_POOL) is always the PRIMARY branch when available,
#     so new files created through the union land on the pool (category.create=ff + branch order).
#   - Any other existing override dirs (e.g. mover/old copies on disks) are also included as RW
#     so in-place edits to files that live there still work.
choose_override_dirs_for_title() {
  local title="$1"
  local __out_primary="${2:-}"
  local __out_array="${3:-}"

  local rel=""
  local primary=""
  local pool_primary=""
  local -a existing=()

  # If OVERRIDE_ROOT is a user-share path, derive equivalent disk/cache locations.
  if [[ "$OVERRIDE_ROOT" == /mnt/user/* ]]; then
    rel="${OVERRIDE_ROOT#/mnt/user}"  # includes leading /

    local d cand

    # Cache/pool is always the PRIMARY target when available so new files created via the union
    # always land on the pool (category.create=ff + branch order).
    if [[ -n "${UNRAID_CACHE_POOL:-}" && -d "/mnt/${UNRAID_CACHE_POOL}" ]]; then
      pool_primary="/mnt/${UNRAID_CACHE_POOL}$rel/$title"
    fi

    # Collect all existing real override dirs for this title (pool + all disks).
    if [[ -n "$pool_primary" && -d "$pool_primary" ]]; then
      existing+=("$pool_primary")
    fi

    for d in "${DISK_MOUNTS_SORTED[@]}"; do
      [[ -d "$d" ]] || continue
      cand="$d$rel/$title"
      [[ -d "$cand" ]] && existing+=("$cand")
    done

    # Default creation target: pool if available; otherwise, choose the lowest-numbered /mnt/diskX
    # to avoid using /mnt/user in mergerfs branch sources (which can deadlock shfs on Unraid).
    if [[ -n "$pool_primary" ]]; then
      primary="$pool_primary"
    else
      local first_disk=""
      for d in "${DISK_MOUNTS_SORTED[@]}"; do
        [[ -d "$d" ]] || continue
        first_disk="$d"
        break
      done
      if [[ -n "$first_disk" ]]; then
        primary="$first_disk$rel/$title"
      else
        # Last resort fallback
        primary="$OVERRIDE_ROOT_REAL/$title"
      fi
    fi
  else
    # Non-user-share override root (already a real path)
    if [[ -n "${OVERRIDE_ROOT_REAL:-}" ]]; then
      primary="$OVERRIDE_ROOT_REAL/$title"
    else
      primary="$OVERRIDE_ROOT/$title"
    fi

    # If the directory already exists, include it.
    [[ -d "$primary" ]] && existing+=("$primary")
  fi

  # De-dup while preserving order (stable).
  if (( ${#existing[@]} )); then
    local -A _seen=()
    local -a _uniq=()
    local _e
    for _e in "${existing[@]}"; do
      [[ -n "$_e" ]] || continue
      [[ -n "${_seen["$_e"]+x}" ]] && continue
      _seen["$_e"]=1
      _uniq+=("$_e")
    done
    existing=("${_uniq[@]}")
  fi

  # Output list: primary (even if it doesn't exist yet) + all other existing real dirs.
  local -A seen=()
  local -a out=()

  if [[ -n "$primary" ]]; then
    out+=("$primary")
    seen["$primary"]=1
  fi

  local p
  for p in "${existing[@]}"; do
    [[ -n "${seen[$p]+x}" ]] && continue
    out+=("$p")
    seen["$p"]=1
  done

  if [[ -n "$__out_primary" ]]; then
    printf -v "$__out_primary" '%s' "$primary"
  fi

  if [[ -n "$__out_array" ]]; then
    declare -n _out_arr="$__out_array"
    _out_arr=("${out[@]}")
  else
    printf '%s\n' "${out[@]}"
  fi
}


# Unraid: /mnt/user is FUSE; inotify can be flaky. Watch real paths too.
#
# IMPORTANT: These watch roots must be valid existing directories when inotifywait starts.
# On Unraid, override roots on the cache pool may not exist until the first write happens;
# if we don't watch them, "details.json" and other override edits can be missed until the
# next periodic scan or a restart.
#
# We therefore (best-effort) ensure the cache-pool override root exists (so new files land
# there), and we rebuild the watch list each time the inotify watcher starts so newly
# created /mnt/diskX paths (from mover) are picked up automatically.

WATCH_ROOTS=()
CACHE_EQUIV=""
DISK_EQUIVS=()
OVERRIDE_CACHE_EQUIV=""
OVERRIDE_DISK_EQUIVS=()

init_watch_roots() {
  WATCH_ROOTS=("$DOWNLOAD_ROOT")
  CACHE_EQUIV=""
  DISK_EQUIVS=()
  OVERRIDE_CACHE_EQUIV=""
  OVERRIDE_DISK_EQUIVS=()

  # === DOWNLOAD_ROOT equivalents ===
  if [[ "$DOWNLOAD_ROOT" == /mnt/user/* && -n "${UNRAID_CACHE_POOL:-}" && -d "/mnt/${UNRAID_CACHE_POOL}" ]]; then
    CACHE_EQUIV="/mnt/${UNRAID_CACHE_POOL}${DOWNLOAD_ROOT#/mnt/user}"
    [[ -d "$CACHE_EQUIV" ]] && WATCH_ROOTS+=("$CACHE_EQUIV")
  fi

  if [[ "$DOWNLOAD_ROOT" == /mnt/user/* ]]; then
    local _rel="${DOWNLOAD_ROOT#/mnt/user}"
    local _d _cand
    for _d in /mnt/disk*; do
      [[ -d "$_d" ]] || continue
      _cand="$_d$_rel"
      [[ -d "$_cand" ]] && { DISK_EQUIVS+=("$_cand"); WATCH_ROOTS+=("$_cand"); }
    done
    unset _rel _d _cand
  fi

  # === OVERRIDE_ROOT equivalents ===
  if [[ "$OVERRIDE_ROOT" == /mnt/user/* ]]; then
    # Always watch the user-share path too (best-effort). Even if inotify on /mnt/user
    # is flaky, it can still catch events that don't appear on specific disk/pool paths.
    [[ -d "$OVERRIDE_ROOT" ]] && WATCH_ROOTS+=("$OVERRIDE_ROOT")

    if [[ -n "${UNRAID_CACHE_POOL:-}" && -d "/mnt/${UNRAID_CACHE_POOL}" ]]; then
      OVERRIDE_CACHE_EQUIV="/mnt/${UNRAID_CACHE_POOL}${OVERRIDE_ROOT#/mnt/user}"
      # Ensure it exists so inotify can watch it, and so new files created via /mnt/user
      # land on the pool (per share settings) instead of creating per-disk copies.
      safe_mkdir_p "$OVERRIDE_CACHE_EQUIV" || true
      [[ -d "$OVERRIDE_CACHE_EQUIV" ]] && WATCH_ROOTS+=("$OVERRIDE_CACHE_EQUIV")
    fi

    local _orel="${OVERRIDE_ROOT#/mnt/user}"
    local _d _cand
    for _d in /mnt/disk*; do
      [[ -d "$_d" ]] || continue
      _cand="$_d$_orel"
      [[ -d "$_cand" ]] && { OVERRIDE_DISK_EQUIVS+=("$_cand"); WATCH_ROOTS+=("$_cand"); }
    done
    unset _orel _d _cand
  else
    [[ -d "$OVERRIDE_ROOT" ]] && WATCH_ROOTS+=("$OVERRIDE_ROOT")
  fi

  # Deduplicate WATCH_ROOTS (just in case)
  if (( ${#WATCH_ROOTS[@]} > 1 )); then
    declare -A _seen_wr=()
    local -a _dedup=()
    local _w
    for _w in "${WATCH_ROOTS[@]}"; do
      if [[ -z "${_seen_wr[$_w]+x}" ]]; then
        _seen_wr["$_w"]=1
        _dedup+=("$_w")
      fi
    done
    WATCH_ROOTS=("${_dedup[@]}")
    unset _seen_wr _dedup _w
  fi
}

################################################################################
# WATCHER TIMINGS
################################################################################

# MergerFS rescan interval (fallback periodic scan)
MERGE_INTERVAL_SECONDS=3600    # 1 hour

# How often mergerfs daemon checks for a trigger from inotify callback
MERGE_TRIGGER_POLL_SECONDS=5
MERGE_MIN_SECONDS_BETWEEN_SCANS=15

# When a scan is requested but another scan is already running, wait this long before retrying
MERGE_LOCK_RETRY_SECONDS=30

# Chapter rename watcher: delay before rename, and quiet period
RENAME_DELAY_SECONDS=300       # 5 minutes
RENAME_QUIET_SECONDS=120       # 2 minutes
RENAME_POLL_SECONDS=20

# Rare safety rescan for rename watcher (missed inotify events). Your preference: "every couple days".
RENAME_RESCAN_SECONDS=172800   # 2 days

################################################################################
# Prefix rewrite controls (case-insensitive bash regex patterns)
################################################################################

PREFIX_BLACKLIST_PATTERNS=(
  '^(ch|ch\.|chapter)$'
  '^(ep|ep\.|episode)$'
  '^(issue)$'
  '^(sp|sp\.|special)$'
  '^(extra|side|omake|oneshot|pilot|interlude|prologue|afterword)$'
  '^(vol|vol\.|volume)$'
  '^(part|pt)\.?[0-9]+$'
  '^s[0-9]+$'
  '^season[0-9]+$'
  '^(en|eng|jp|jpn|kr|kor|cn|chi|fr|es|de|pt|it|ru)$'
  '^\(s[0-9]+\)$'
  '^\(season[0-9]+\)$'
)

PREFIX_WHITELIST_PATTERNS=(
  '^team[[:alnum:]]*$'
  '.*scan.*'
  '.*subs?$'
  '^(tl|tls|trans|translate|translator)[[:alnum:]]*$'
  '^(anon|anonymous)[[:alnum:]]*$'
  '.*group.*'
  '.*studio.*'
)

################################################################################
# RUNTIME / STATE
################################################################################

STATE_DIR="/mnt/suwayomi_manga_daemon"
PID_FILE="$STATE_DIR/daemon.pid"
SUPERVISOR_LOCK_FILE="$STATE_DIR/supervisor.lock"

# Child daemon PID files (best-effort; used for cleanup after crashes)
MERGE_PID_FILE="$STATE_DIR/mergerfs.pid"
RENAME_PID_FILE="$STATE_DIR/rename.pid"

# Separate internal states for each subsystem
MERGE_STATE_DIR="$STATE_DIR/mergerfs"
RENAME_STATE_DIR="$STATE_DIR/rename"

MERGE_LOCK_FILE="$MERGE_STATE_DIR/lock"
RENAME_LOCK_FILE="$RENAME_STATE_DIR/lock"
RENAME_QUEUE_FILE="$RENAME_STATE_DIR/queue.tsv"

# Trigger file (written by the existing inotify callback; read by mergerfs daemon)
MERGE_TRIGGER_FILE="$MERGE_STATE_DIR/trigger"

# Logging (keep on /boot so it""'s still writable during array stop)
LOG_FILE="$LOG_DIR_REAL/daemon.log"

################################################################################
# FLAGS
################################################################################
DRY_RUN=0
DEBUG_COMICINFO="${DEBUG_COMICINFO:-0}"  # set to 1 for verbose ComicInfo.xml detection logs
#DETAILS_DESC_MODE="${DETAILS_DESC_MODE:-br}"  # details.json description formatting: br (HTML <br>) or text (\n)
DETAILS_DESC_MODE="${DETAILS_DESC_MODE:-text}"  # details.json description formatting: br (HTML <br>) or text (\n)
UNMOUNT_ON_EXIT=1          # default: yes (use --no-unmount if you want)
LOW_PRIO=1                 # default: yes, best-effort ionice+renice

# Shutdown/stop tuning
STOP_TIMEOUT_SECONDS="${STOP_TIMEOUT_SECONDS:-120}"          # seconds to wait for supervisor to exit before force-kill in --stop
CHILD_EXIT_GRACE_SECONDS="${CHILD_EXIT_GRACE_SECONDS:-5}"    # seconds supervisor waits for worker daemons to exit cleanly
UNMOUNT_CMD_TIMEOUT_SECONDS="${UNMOUNT_CMD_TIMEOUT_SECONDS:-8}"      # per fusermount/umount command timeout
UNMOUNT_DETACH_WAIT_SECONDS="${UNMOUNT_DETACH_WAIT_SECONDS:-5}"      # seconds to wait after lazy detach for mount to disappear
CLEANUP_HIGH_PRIO="${CLEANUP_HIGH_PRIO:-1}"                 # boost ionice/nice for cleanup/unmount (recommended)

# Normalize tunables to avoid arithmetic errors under set -e
[[ "$STOP_TIMEOUT_SECONDS" =~ ^[0-9]+$ ]] || STOP_TIMEOUT_SECONDS=120
[[ "$CHILD_EXIT_GRACE_SECONDS" =~ ^[0-9]+$ ]] || CHILD_EXIT_GRACE_SECONDS=5
[[ "$UNMOUNT_CMD_TIMEOUT_SECONDS" =~ ^[0-9]+$ ]] || UNMOUNT_CMD_TIMEOUT_SECONDS=8
[[ "$UNMOUNT_DETACH_WAIT_SECONDS" =~ ^[0-9]+$ ]] || UNMOUNT_DETACH_WAIT_SECONDS=5
[[ "$CLEANUP_HIGH_PRIO" =~ ^[01]$ ]] || CLEANUP_HIGH_PRIO=1

RESCAN_NOW=1               # default: yes (startup rename rescan). Use --no-rescan-now to disable.
STARTUP_CLEANUP=1      # default: yes; best-effort cleanup if previous run died
TIMEOUT_HAS_PRESERVE=-1       # cached check for timeout --preserve-status support
INOTIFY_HAS_EXCLUDEI=-1      # cached check for inotifywait --excludei support
FINDMNT_PAIRS_MODE=""     # cached findmnt pairs-mode compatibility: modern (-n -P) or legacy (-rn -P)
INHERIT_FROM_PARENT="${INHERIT_FROM_PARENT:-1}"
# If 1, enforce cached owner/mode on existing paths too (slower). Default 0.
PERMS_ENFORCE_EXISTING="${PERMS_ENFORCE_EXISTING:-0}"
case "${PERMS_ENFORCE_EXISTING,,}" in
  1|true|yes|y|on) PERMS_ENFORCE_EXISTING=1 ;;
  *) PERMS_ENFORCE_EXISTING=0 ;;
esac

# Normalize to 0/1 to avoid arithmetic errors under set -e
if [[ ! "$INHERIT_FROM_PARENT" =~ ^[01]$ ]]; then
  INHERIT_FROM_PARENT=1
fi

# Cached permissions derived from DOWNLOAD_ROOT (captured once at startup).
# We apply these permissions to paths created/managed by this daemon to keep
# ownership/mode consistent and avoid thousands of per-path stat calls.
PERMS_REF_PATH="${PERMS_REF_PATH:-$DOWNLOAD_ROOT}"
PERMS_REF_PATH_REAL=""
PERMS_CACHE_UID=""
PERMS_CACHE_GID=""
PERMS_CACHE_DMODE=""
PERMS_CACHE_FMODE=""
PERMS_CACHE_UMASK=""
PERMS_CACHE_OK=0
PERMS_CACHE_CAN_CHOWN=0
declare -A PERMS_APPLIED_CACHE=()   # path -> 1 (avoid repeated chmod/chown within a single daemon process)

# Last branchlink stats (for debug)
BRANCHLINKS_LAST_TOTAL=0
BRANCHLINKS_LAST_CHANGED=0
BRANCHLINKS_LAST_SKIPPED=0


################################################################################
# Helpers
################################################################################

# Always have a valid log FD (3). Reopened to real file during run() if possible.
exec 3>/dev/null

normalize_event_path_to_user() {
  local p="${1%/}"

  # If event came from cache-equivalent path, map it back to /mnt/user path.
  if [[ -n "${CACHE_EQUIV:-}" && "$p" == "$CACHE_EQUIV"* ]]; then
    printf '%s' "$DOWNLOAD_ROOT${p#"$CACHE_EQUIV"}"
    return 0
  fi

  # If event came from an array-disk equivalent, map it back to /mnt/user path.
  if (( ${#DISK_EQUIVS[@]} )); then
    local _r
    for _r in "${DISK_EQUIVS[@]}"; do
      if [[ "$p" == "$_r"* ]]; then
        printf '%s' "$DOWNLOAD_ROOT${p#"$_r"}"
        return 0
      fi
    done
  fi

# If event came from override cache-equivalent path, map it back to OVERRIDE_ROOT.
if [[ -n "${OVERRIDE_CACHE_EQUIV:-}" && "$p" == "$OVERRIDE_CACHE_EQUIV"* ]]; then
  printf '%s' "$OVERRIDE_ROOT${p#"$OVERRIDE_CACHE_EQUIV"}"
  return 0
fi

# If event came from an override array-disk equivalent, map it back to OVERRIDE_ROOT.
if (( ${#OVERRIDE_DISK_EQUIVS[@]} )); then
  local _or
  for _or in "${OVERRIDE_DISK_EQUIVS[@]}"; do
    if [[ "$p" == "$_or"* ]]; then
      printf '%s' "$OVERRIDE_ROOT${p#"$_or"}"
      return 0
    fi
  done
fi

  printf '%s' "$p"
  return 0
}

open_log_fd() {
  # Ensure FD 3 points somewhere to avoid recursion if open_log_fd is called because FD 3 was closed.
  exec 3>/dev/null 2>/dev/null || true
  # Best effort: don't die if the log path becomes unwritable.
  # Prefer writing to a real (non-/mnt/user) path on Unraid when available.
  safe_mkdir_p "$LOG_DIR_REAL" || true
  if ! exec 3>>"$LOG_FILE" 2>/dev/null; then
    exec 3>/dev/null
    return 0
  fi
  # Now that FD 3 is valid, optionally fix ownership/mode to match the parent dir.
  inherit_from_parent "$LOG_DIR_REAL" || true
  inherit_from_parent "$LOG_FILE" "$LOG_DIR_REAL" || true
}

open_safe_log_fd() {
  # Switch FD 3 to a RAM-backed log so shutdown/stop can't wedge if /mnt/user goes away
  # or the array is stopping. This intentionally does NOT call log().
  local d="$STATE_DIR"
  mkdir -p "$d" 2>/dev/null || true
  local f="$d/daemon.safe.log"
  # Always replace FD 3 (it may point to a hung filesystem)
  exec 3>>"$f" 2>/dev/null || exec 3>/dev/null || true
}



log() {
  local ts
  ts="$(date '+%F %T' 2>/dev/null || echo 'unknown-time')"

  # Ensure FD 3 is open. In some subshell/exec contexts (including command substitutions),
  # FD 3 may not be inherited as expected on Unraid. Re-open best-effort if needed.
  if ! { : >&3; } 2>/dev/null; then
    open_log_fd || true
  fi

  printf '[%s] %s\n' "$ts" "$*" >&3 2>/dev/null || true
}

die(){ printf 'ERROR: %s\n' "$*" >&2; exit 1; }

################################################################################
# Timing helpers (profiling)
################################################################################

# Returns monotonic-ish nanoseconds (epoch ns). Good enough for elapsed deltas.
prof_ns() {
  local ns=""
  ns="$(date +%s%N 2>/dev/null || true)"
  if [[ "$ns" =~ ^[0-9]+$ && ${#ns} -ge 16 ]]; then
    printf '%s' "$ns"
    return 0
  fi
  local s=""
  s="$(date +%s 2>/dev/null || echo 0)"
  printf '%s000000000' "$s"
}

# Global accumulators (cleared each scan pass)
declare -A PROF_TOTAL_NS=()
declare -A PROF_COUNT=()
declare -A PROF_TIMEOUTS=()

declare -a TOP_DETAILS_NS=() TOP_DETAILS_LABEL=()
declare -a TOP_MOUNT_NS=() TOP_MOUNT_LABEL=()
declare -a TOP_UNMOUNT_NS=() TOP_UNMOUNT_LABEL=()
declare -a TOP_BRANCHLINKS_NS=() TOP_BRANCHLINKS_LABEL=()
declare -a TOP_FINDSOURCES_NS=() TOP_FINDSOURCES_LABEL=()
declare -a TOP_FINDMANGAS_NS=() TOP_FINDMANGAS_LABEL=()
declare -a TOP_MANGADIR_NS=() TOP_MANGADIR_LABEL=()
declare -a TOP_OVERRIDES_NS=() TOP_OVERRIDES_LABEL=()
declare -a TOP_TITLE_NS=() TOP_TITLE_LABEL=()

prof_reset() {
  PROF_TOTAL_NS=()
  PROF_COUNT=()
  PROF_TIMEOUTS=()
  TOP_DETAILS_NS=(); TOP_DETAILS_LABEL=()
  TOP_MOUNT_NS=(); TOP_MOUNT_LABEL=()
  TOP_UNMOUNT_NS=(); TOP_UNMOUNT_LABEL=()
  TOP_BRANCHLINKS_NS=(); TOP_BRANCHLINKS_LABEL=()
  TOP_FINDSOURCES_NS=(); TOP_FINDSOURCES_LABEL=()
  TOP_FINDMANGAS_NS=(); TOP_FINDMANGAS_LABEL=()
  TOP_MANGADIR_NS=(); TOP_MANGADIR_LABEL=()
  TOP_OVERRIDES_NS=(); TOP_OVERRIDES_LABEL=()
  TOP_TITLE_NS=(); TOP_TITLE_LABEL=()
}

prof_add() {
  local label="$1"
  local start_ns="$2"
  local end_ns
  end_ns="$(prof_ns)"
  local dt=$(( end_ns - start_ns ))
  PROF_TOTAL_NS["$label"]=$(( ${PROF_TOTAL_NS[$label]-0} + dt ))
  PROF_COUNT["$label"]=$(( ${PROF_COUNT[$label]-0} + 1 ))
}

prof_add_ns() {
  local label="$1"
  local dt="$2"
  PROF_TOTAL_NS["$label"]=$(( ${PROF_TOTAL_NS[$label]-0} + dt ))
  PROF_COUNT["$label"]=$(( ${PROF_COUNT[$label]-0} + 1 ))
}

prof_timeout() {
  local label="$1"
  PROF_TIMEOUTS["$label"]=$(( ${PROF_TIMEOUTS[$label]-0} + 1 ))
}

fmt_ms() { printf '%d' $(( ${1:-0} / 1000000 )); }
fmt_s() { awk -v ns="${1:-0}" 'BEGIN{printf "%.2f", (ns/1000000000.0)}'; }

top_insert() {
  local base="$1" ns="$2" label="$3" limit="${4:-$DEBUG_TIMING_TOP_N}"
  local min_ms="${DEBUG_TIMING_MIN_ITEM_MS:-0}"
  local ms=$(( ns / 1000000 ))
  (( ms < min_ms )) && return 0

  local ns_var="TOP_${base}_NS"
  local lab_var="TOP_${base}_LABEL"

  # Pull current arrays into locals
  eval "local -a _ns=(\"\${${ns_var}[@]-}\")"
  eval "local -a _lab=(\"\${${lab_var}[@]-}\")"

  _ns+=("$ns")
  _lab+=("$label")

  # Bubble last element into place (descending by ns)
  local i=$(( ${#_ns[@]} - 1 ))
  while (( i > 0 )); do
    if (( _ns[i] > _ns[i-1] )); then
      local tmp="${_ns[i-1]}"; _ns[i-1]="${_ns[i]}"; _ns[i]="$tmp"
      local tmpl="${_lab[i-1]}"; _lab[i-1]="${_lab[i]}"; _lab[i]="$tmpl"
      ((i-=1))
    else
      break
    fi
  done

  # Trim to limit
  if (( ${#_ns[@]} > limit )); then
    _ns=("${_ns[@]:0:limit}")
    _lab=("${_lab[@]:0:limit}")
  fi

  # Write back
  eval "${ns_var}=(\"\${_ns[@]}\")"
  eval "${lab_var}=(\"\${_lab[@]}\")"
}

prof_report_merge_pass() {
  local pass_start_ns="$1"
  local reason="$2"
  local srcs="$3" mangas="$4" titles="$5"
  local details_calls="$6" need_actions="$7"
  local mount_ops="$8" unmount_ops="$9"
  shift 9
  local mount_fail="${1:-0}" unmount_fail="${2:-0}"

  local pass_end_ns; pass_end_ns="$(prof_ns)"
  local pass_dt=$(( pass_end_ns - pass_start_ns ))

  log "MergerFS: timing: total=$(fmt_s "$pass_dt")s (sources=$srcs mangas=$mangas titles=$titles actions=$need_actions details_calls=$details_calls mounts=$mount_ops unmounts=$unmount_ops mount_fail=$mount_fail unmount_fail=$unmount_fail)${reason:+ reason='$reason'}"

  # Stage totals (some may be zero if skipped)
  local stage
  for stage in load_mappings build_override_index build_findmnt_snapshot find_sources find_mangas process_manga_dir choose_override_dirs mkdir_override mkdir_mountpoint order_branches ensure_details_json prepare_branchlinks title_total unmount mount cleanup_findmnt cleanup_branchlinks; do
    local t="${PROF_TOTAL_NS[$stage]-0}"
    local c="${PROF_COUNT[$stage]-0}"
    (( t > 0 || c > 0 )) || continue
    local avg=0
    (( c > 0 )) && avg=$(( t / c ))
    log "MergerFS: timing:  stage=$stage total=$(fmt_s "$t")s count=$c avg=$(fmt_ms "$avg")ms timeouts=${PROF_TIMEOUTS[$stage]-0}"
  done

  # Slowest lists

  # Slowest lists (live offenders)
  local i n ms lbl
  n="${#TOP_FINDSOURCES_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest find_sources (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_FINDSOURCES_NS[$i]}")"
      lbl="${TOP_FINDSOURCES_LABEL[$i]}"
      log "  find_sources ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_FINDMANGAS_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest find_mangas (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_FINDMANGAS_NS[$i]}")"
      lbl="${TOP_FINDMANGAS_LABEL[$i]}"
      log "  find_mangas ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_MANGADIR_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest per-manga-dir processing (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_MANGADIR_NS[$i]}")"
      lbl="${TOP_MANGADIR_LABEL[$i]}"
      log "  manga_dir ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_OVERRIDES_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest override-dir resolution (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_OVERRIDES_NS[$i]}")"
      lbl="${TOP_OVERRIDES_LABEL[$i]}"
      log "  overrides ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_TITLE_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest full title processing (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_TITLE_NS[$i]}")"
      lbl="${TOP_TITLE_LABEL[$i]}"
      log "  title ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_DETAILS_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest details.json (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_DETAILS_NS[$i]}")"
      lbl="${TOP_DETAILS_LABEL[$i]}"
      log "  details ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_BRANCHLINKS_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest branchlink prep (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_BRANCHLINKS_NS[$i]}")"
      lbl="${TOP_BRANCHLINKS_LABEL[$i]}"
      log "  branchlinks ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_UNMOUNT_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest unmounts (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_UNMOUNT_NS[$i]}")"
      lbl="${TOP_UNMOUNT_LABEL[$i]}"
      log "  unmount ${ms}ms: $lbl"
    done
  fi

  n="${#TOP_MOUNT_NS[@]}"
  if (( n > 0 )); then
    log "MergerFS: timing: slowest mounts (top $n, min ${DEBUG_TIMING_MIN_ITEM_MS}ms):"
    for (( i=0; i<n; i++ )); do
      ms="$(fmt_ms "${TOP_MOUNT_NS[$i]}")"
      lbl="${TOP_MOUNT_LABEL[$i]}"
      log "  mount ${ms}ms: $lbl"
    done
  fi
}


trim_ws(){
  local s="${1-}"
  s="${s//$'\r'/}"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

is_excluded_source(){
  local s x xl
  s="$(trim_ws "${1-}")"
  s="${s,,}"
  for x in "${EXCLUDED_SOURCES[@]-}"; do
    xl="$(trim_ws "$x")"
    xl="${xl,,}"
    [[ -n "$xl" ]] || continue
    [[ "$s" == "$xl" ]] && return 0
  done
  return 1
}

# Escape text for use in an ERE (extended regex) (for inotifywait --exclude/--excludei).
regex_escape_ere() {
  local s="${1-}"
  # Escape ERE metacharacters: ] [ \ . ^ $ | ? * + ( ) { }
  printf '%s' "$s" | sed -e 's/[][\\.^$|?*+(){}]/\\&/g'
}

build_inotify_exclude_regex() {
  local parts=() x xl
  for x in "${EXCLUDED_SOURCES[@]-}"; do
    xl="$(trim_ws "$x")"
    [[ -n "$xl" ]] || continue
    xl="$(regex_escape_ere "$xl")"
    parts+=("$xl")
  done
  if (( ${#parts[@]} )); then
    local joined
    joined="$(IFS='|'; echo "${parts[*]}")"
    # Match excluded source names as a path segment anywhere in the watched trees.
    printf '/(%s)(/|$)' "$joined"
  else
    printf ''
  fi
}

inotifywait_supports_excludei() {
  if (( INOTIFY_HAS_EXCLUDEI == -1 )); then
    if inotifywait --help 2>&1 | grep -q -- '--excludei'; then
      INOTIFY_HAS_EXCLUDEI=1
    else
      INOTIFY_HAS_EXCLUDEI=0
    fi
  fi
  (( INOTIFY_HAS_EXCLUDEI == 1 ))
}

findmnt_pairs_mode_name() {
  case "${FINDMNT_PAIRS_MODE:-}" in
    modern) printf '%s' '-n -P' ;;
    legacy) printf '%s' '-rn -P' ;;
    *) printf '%s' 'unknown' ;;
  esac
}

probe_findmnt_pairs_mode() {
  # Cache which findmnt pairs-mode flag set is supported by the host util-linux.
  if [[ -n "${FINDMNT_PAIRS_MODE:-}" ]]; then
    return 0
  fi

  local tmp
  tmp="$(tmpfile findmnt_probe)"

  local modern_rc=0
  ro_timeout 2 findmnt -n -P -o TARGET >"$tmp" 2>/dev/null || modern_rc=$?
  if (( modern_rc == 0 )); then
    FINDMNT_PAIRS_MODE="modern"
    rm -f -- "$tmp" 2>/dev/null || true
    return 0
  fi

  local legacy_rc=0
  ro_timeout 2 findmnt -rn -P -o TARGET >"$tmp" 2>/dev/null || legacy_rc=$?
  if (( legacy_rc == 0 )); then
    FINDMNT_PAIRS_MODE="legacy"
    rm -f -- "$tmp" 2>/dev/null || true
    return 0
  fi

  FINDMNT_PAIRS_MODE="modern"
  rm -f -- "$tmp" 2>/dev/null || true
  log "WARN: findmnt pairs-mode probe failed (rc modern=$modern_rc legacy=$legacy_rc); defaulting to -n -P"
  return 1
}

findmnt_pairs() {
  # Canonical wrapper for `findmnt -P` calls with util-linux compatibility fallback.
  probe_findmnt_pairs_mode >/dev/null 2>&1 || true
  if [[ "$FINDMNT_PAIRS_MODE" == "legacy" ]]; then
    findmnt -rn -P "$@"
  else
    findmnt -n -P "$@"
  fi
}

ro_timeout_findmnt_pairs() {
  # Timed findmnt pairs-mode wrapper that always invokes the external findmnt binary.
  local seconds="$1"
  shift

  probe_findmnt_pairs_mode >/dev/null 2>&1 || true
  if [[ "$FINDMNT_PAIRS_MODE" == "legacy" ]]; then
    ro_timeout "$seconds" findmnt -rn -P "$@"
  else
    ro_timeout "$seconds" findmnt -n -P "$@"
  fi
}

need_cmd(){ command -v "$1" >/dev/null 2>&1 || die "Missing command: $1"; }

run_cmd() {
  if (( DRY_RUN )); then
    {
      printf 'DRY-RUN:'
      printf ' %q' "$@"
      printf '\n'
    } >&3 || true
    return 0
  fi
  "$@"
}


timeout_supports_preserve() {
  # Cache whether `timeout` supports --preserve-status.
  # IMPORTANT: Do NOT probe by running the target command twice.
  if (( TIMEOUT_HAS_PRESERVE == -1 )); then
    if command -v timeout >/dev/null 2>&1 && timeout --help 2>&1 | grep -q -- '--preserve-status'; then
      TIMEOUT_HAS_PRESERVE=1
    else
      TIMEOUT_HAS_PRESERVE=0
    fi
  fi
  (( TIMEOUT_HAS_PRESERVE == 1 ))
}

sleep_ms() {
  # Portable-ish millisecond sleep using `sleep` with fractional seconds.
  # Accepts integer milliseconds.
  local ms="${1:-0}"
  [[ "$ms" =~ ^[0-9]+$ ]] || ms=0
  if (( ms <= 0 )); then
    return 0
  fi
  if (( ms >= 1000 )); then
    local s=$(( ms / 1000 ))
    local r=$(( ms % 1000 ))
    sleep "$s"
    ms="$r"
  fi
  if (( ms > 0 )); then
    local frac
    printf -v frac '0.%03d' "$ms"
    sleep "$frac"
  fi
}


_run_cmd_timeout_with_poll() {
  local seconds="$1"; shift
  local poll_ms="$1"; shift

  if (( DRY_RUN )); then
    { printf 'DRY-RUN:'; printf ' %q' "$@"; printf '\n'; } >&2
    return 0
  fi

  # Prefer GNU coreutils `timeout` if present, but supervise it with polling so we can
  # still return even if the wrapped command wedges in D state (common with stale FUSE I/O).
  if command -v timeout >/dev/null 2>&1; then
    local rc=0
    local wrapper_pid=0

    if timeout_supports_preserve; then
      timeout --preserve-status "$seconds" "$@" 9>&- &
    else
      timeout "$seconds" "$@" 9>&- &
    fi
    wrapper_pid=$!

    # Poll in sub-second intervals to avoid a 1s minimum overhead for short commands.
    local elapsed_ms=0
    local limit_ms=$(( (seconds + 3) * 1000 ))
    while (( elapsed_ms < limit_ms )); do
      if ! kill -0 "$wrapper_pid" 2>/dev/null; then
        wait "$wrapper_pid" 2>/dev/null || rc=$?
        return "$rc"
      fi
      sleep_ms "$poll_ms"
      elapsed_ms=$(( elapsed_ms + poll_ms ))
    done

    # If we're still here, either timeout is wedged waiting on an unkillable child,
    # or something else went badly. Kill the wrapper (best effort) and return 124.
    kill_tree "$wrapper_pid" TERM || true
    sleep 1
    kill_tree "$wrapper_pid" KILL || true
    ( wait "$wrapper_pid" 2>/dev/null || true ) &
    return 124
  fi

  # Portable bash fallback:
  # - Returns after `seconds` even if the target command becomes unkillable (D state),
  #   so the supervisor doesn't hang silently on stale FUSE I/O.
  ("$@" 9>&-) &
  local cmd_pid=$!

  local rc=0
  local elapsed_ms=0
  local limit_ms=$(( seconds * 1000 ))

  # Poll in sub-second intervals (see above).
  while (( elapsed_ms < limit_ms )); do
    if ! kill -0 "$cmd_pid" 2>/dev/null; then
      wait "$cmd_pid" 2>/dev/null || rc=$?
      return "$rc"
    fi
    sleep_ms "$poll_ms"
    elapsed_ms=$(( elapsed_ms + poll_ms ))
  done

  # Timed out. Try to terminate; do not block on wait (it may be unkillable).
  kill -TERM "$cmd_pid" 2>/dev/null || true
  sleep 1
  kill -KILL "$cmd_pid" 2>/dev/null || true

  # Reap if it eventually exits, without blocking this function.
  ( wait "$cmd_pid" 2>/dev/null || true ) &

  return 124
}

run_cmd_timeout() {
  local seconds="$1"; shift
  _run_cmd_timeout_with_poll "$seconds" "${TIMEOUT_POLL_MS:-100}" "$@"
}

run_cmd_timeout_fast() {
  local seconds="$1"; shift
  _run_cmd_timeout_with_poll "$seconds" "${TIMEOUT_POLL_MS_FAST:-10}" "$@"
}


ro_timeout() {
  # Like run_cmd_timeout, but ALWAYS executes the command even in DRY_RUN mode.
  # Use only for read-only operations (find/stat/findmnt etc).
  local seconds="$1"; shift
  local _dry="${DRY_RUN:-0}"
  DRY_RUN=0
  run_cmd_timeout "$seconds" "$@"
  local rc=$?
  DRY_RUN="$_dry"
  return $rc
}


# Capture the first line of a command's stdout with a timeout, without using pipes.
# This avoids rare hangs when the underlying command gets stuck in uninterruptible I/O
# while still holding the stdout pipe open (common on FUSE/shfs stalls).
capture_first_line_timeout() {
  local seconds="$1"
  local __outvar="$2"
  shift 2

  local tmp
  tmp="$(mktemp "${TMPDIR:-/tmp}/suwayomi_manga_daemon.capture.XXXXXX" 2>/dev/null || echo "${TMPDIR:-/tmp}/suwayomi_manga_daemon.capture.$$.$RANDOM")"
  : > "$tmp" 2>/dev/null || true
  ro_timeout "$seconds" "$@" >"$tmp" 2>/dev/null || true

  local line=""
  IFS= read -r line <"$tmp" 2>/dev/null || true
  rm -f -- "$tmp" 2>/dev/null || true

  printf -v "$__outvar" '%s' "$line"
}

tmpfile() {
  # Create a unique temp file path in TMPDIR (default: /tmp), not under /mnt.
  # Unraid can stall on /mnt/user or dead FUSE mounts; keeping temps in tmpfs avoids that.
  local label="${1:-tmp}"
  label="${label//[^A-Za-z0-9_]/_}"
  local f=""
  f="$(mktemp "${TMPDIR:-/tmp}/suwayomi_manga_daemon.${label}.XXXXXX" 2>/dev/null || true)"
  if [[ -z "$f" ]]; then
    f="${TMPDIR:-/tmp}/suwayomi_manga_daemon.${label}.$$.$RANDOM"
    : > "$f" 2>/dev/null || true
  fi
  printf '%s' "$f"
}



# When running as root, make paths created by this script look like they were created
# by the parent directory's owner/group. This avoids root-owned artifacts breaking
# other tooling (common on Unraid with containers running as nobody/users).
#
# Note: Linux doesn't "inherit" *user* ownership automatically; root must chown.
cache_reference_perms() {
  # Cache uid:gid:mode from PERMS_REF_PATH (default: DOWNLOAD_ROOT) once.
  if [[ "${EUID:-$(id -u)}" -eq 0 ]]; then
    PERMS_CACHE_CAN_CHOWN=1
  else
    PERMS_CACHE_CAN_CHOWN=0
  fi
  (( PERMS_CACHE_OK )) && return 0

  local ref="${PERMS_REF_PATH:-$DOWNLOAD_ROOT}"
  local real="$ref"
  real="$(resolve_user_path_to_real "$ref" 2>/dev/null || printf '%s' "$ref")"
  PERMS_REF_PATH_REAL="$real"

  local meta=""
  capture_first_line_timeout 3 meta stat -c '%u:%g:%a' "$real"
  if [[ -z "$meta" && "$real" != "$ref" ]]; then
    capture_first_line_timeout 3 meta stat -c '%u:%g:%a' "$ref"
  fi

  if [[ -z "$meta" ]]; then
    PERMS_CACHE_OK=0
    log "WARN: Perms: could not stat reference dir for uid/gid/mode: $ref"
    return 0
  fi

  IFS=':' read -r PERMS_CACHE_UID PERMS_CACHE_GID PERMS_CACHE_DMODE <<<"$meta"
  if [[ -z "${PERMS_CACHE_UID:-}" || -z "${PERMS_CACHE_GID:-}" || -z "${PERMS_CACHE_DMODE:-}" ]]; then
    PERMS_CACHE_OK=0
    log "WARN: Perms: invalid stat meta from $real: $meta"
    return 0
  fi

  local pm=0 fm=0
  pm=$(( 8#${PERMS_CACHE_DMODE} ))
  fm=$(( pm & 8#666 ))
  PERMS_CACHE_FMODE="$(printf '%o' "$fm")"
  PERMS_CACHE_OK=1
  log "Perms: cached from $real (ref=$ref) uid=$PERMS_CACHE_UID gid=$PERMS_CACHE_GID dmode=$PERMS_CACHE_DMODE fmode=$PERMS_CACHE_FMODE"
  return 0
}

_apply_cached_perms_once() {
  local path="$1"
  local is_root=0
  [[ "${EUID:-$(id -u)}" == "0" ]] && is_root=1
  [[ -n "${path:-}" ]] || return 0

  # Only attempt when we have a cached reference.
  cache_reference_perms || return 0
  (( PERMS_CACHE_OK )) || return 0

  # Skip repeat work for the same path during this daemon run.
  if [[ -n "${PERMS_APPLIED_CACHE[$path]+x}" ]]; then
    return 0
  fi
  PERMS_APPLIED_CACHE["$path"]=1

  # If not root, ownership fixes are skipped (gated below) but chmod may still work.

  # Read current owner/mode in one shot (do NOT follow symlinks).
  local meta u g a ftype
  meta=""
  if ! capture_first_line_timeout 3 meta stat -c '%u:%g:%a:%F' -- "$path"; then
    return 0
  fi
  IFS=':' read -r u g a ftype <<<"$meta"

  local need_chown=0 need_chmod=0 desired_mode=""

  if [[ "$u" != "$PERMS_CACHE_UID" || "$g" != "$PERMS_CACHE_GID" ]]; then
    need_chown=1
  fi

  case "$ftype" in
    directory)
      desired_mode="$PERMS_CACHE_DMODE"
      [[ "$a" != "$desired_mode" ]] && need_chmod=1
      ;;
    "regular file")
      desired_mode="$PERMS_CACHE_FMODE"
      [[ "$a" != "$desired_mode" ]] && need_chmod=1
      ;;
    # Symlinks/devices/sockets/etc: ownership only; chmod is meaningless or undesirable.
    *)
      need_chmod=0
      ;;
  esac

  if (( need_chown )); then
    if [[ -L "$path" ]]; then
      if (( is_root == 1 )); then

        run_cmd_timeout 5 chown -h "$PERMS_CACHE_UID:$PERMS_CACHE_GID" -- "$path" >/dev/null 2>&1 || true

      fi
    else
      if (( is_root == 1 )); then

        run_cmd_timeout 5 chown "$PERMS_CACHE_UID:$PERMS_CACHE_GID" -- "$path" >/dev/null 2>&1 || true

      fi
    fi
  fi

  if (( need_chmod )); then
    run_cmd_timeout 5 chmod "$desired_mode" -- "$path" >/dev/null 2>&1 || true
  fi
}

# Backwards-compatible name used throughout the script.
inherit_from_parent() {
  local path="$1"
  _apply_cached_perms_once "$path"
}



# Some Unraid setups (especially when mapping /mnt/user -> /mnt/<pool>) can end up
# with a directory existing on the array with the right ownership, while the pool
# copy is auto-created by this daemon as root (masking the array copy under /mnt/user).
# If that happens, we only want to "fix" the dir when it is actually root-owned.
fix_root_owned_dir_if_needed() {
  # Historical helper name. We no longer stat/compare; we simply apply cached perms once.
  local path="$1"
  _apply_cached_perms_once "$path"
  return 0
}

# Test whether a path is an actual mountpoint (fast, exact).
# Uses findmnt -M so it only returns true when the mountpoint itself is "$path".
is_mountpoint() {
  local path="$1"
  local tgt=""
  # -M matches the mountpoint itself (not just a parent mount containing the path).
  capture_first_line_timeout 2 tgt findmnt -rn -M "$path" -o TARGET 2>/dev/null
  tgt="$(unescape_findmnt_string "$tgt")"
  [[ -n "$tgt" && "$tgt" == "$path" ]]
}
# mkdir -p that won't hang the whole daemon on odd Unraid/FUSE stalls.


safe_mkdir_p() {
  local path="$1"
  local chmod_mode="${2:-}"
  local chown_user="${3:-}"
  local chgrp_group="${4:-}"

  # Fast path: if it already exists and is accessible, we're done.
  if dir_accessible_fast "$path"; then
    return 0
  fi

  # If the kernel thinks something is mounted here but it's not accessible (common with stale FUSE),
  # detach it BEFORE attempting mkdir/stat (which can throw TEIN).
  if is_mounted_any "$path" && ! dir_accessible_fast "$path"; then
    log "FS: mountpoint not accessible; detaching: $path"
    FORCE_REMOUNT_MP["$path"]=1
    unmount_path "$path" || true
    sleep 0.1
  fi

  # Try to create it (capture stderr so we can detect TEIN reliably).
  local tmp_err; tmp_err="$(tmpfile mkdirerr)"
  local rc=0
  run_cmd_timeout_fast 5 mkdir -p -- "$path" 2>"$tmp_err" || rc=$?
  local last_err="$(<"$tmp_err")"
  rm -f "$tmp_err" 2>/dev/null || true

  if (( rc == 0 )); then
    if [[ -n "$chmod_mode" || -n "$chown_user" || -n "$chgrp_group" ]]; then
      apply_perms "$path" "$chmod_mode" "$chown_user" "$chgrp_group" || true
    fi
    return 0
  fi

  # If the path is a broken FUSE mountpoint, mkdir/stat will often fail with:
  #   "Transport endpoint is not connected"
  if [[ "$last_err" == *"Transport endpoint is not connected"* ]]; then
    log "FS: mkdir failed (rc=$rc) path=$path err=${last_err//$'\n'/ } (transport endpoint); forcing detach+retry"
    FORCE_REMOUNT_MP["$path"]=1

    # One-time sweep: if a previous crash left lots of stale mounts under LOCAL_ROOT,
    # detach them all once so we don't hit TEIN on every title during startup.
    if [[ "${TEIN_SWEEP_DONE:-0}" != "1" ]] && [[ "$path" == "$LOCAL_ROOT_REAL/"* || "$path" == "$LOCAL_ROOT_REAL" ]]; then
      TEIN_SWEEP_DONE=1
      log "FS: TEIN detected; sweeping FUSE mounts under $LOCAL_ROOT_REAL"
      detach_fuse_mounts_under "$LOCAL_ROOT_REAL" || true
    fi


    # Best-effort detach for the mountpoint itself.
    unmount_path "$path" || true
    sleep 0.1

    # If we still can't access the path, the TEIN may be coming from a mounted ancestor (rare but nasty).
    if ! dir_accessible_fast "$path"; then
      local anc=""
      if anc="$(longest_mounted_ancestor "$path" 2>/dev/null)" && [[ -n "$anc" ]]; then
        local fstype=""
        fstype="$(mountinfo_fstype_for_mp "$anc")"
        if [[ "$fstype" == fuse.* || "$fstype" == *mergerfs* ]]; then
          log "FS: TEIN persists; detaching mounted ancestor $anc (fstype=$fstype)"
          unmount_path "$anc" || true
          sleep 0.1
        fi
      fi
    fi

    # Retry create after detach attempts.
    local tmp_err2; tmp_err2="$(tmpfile mkdirerr)"
    local rc2=0
    run_cmd_timeout_fast 5 mkdir -p -- "$path" 2>"$tmp_err2" || rc2=$?
    local last_err2="$(<"$tmp_err2")"
    rm -f "$tmp_err2" 2>/dev/null || true

    if (( rc2 == 0 )); then
      if [[ -n "$chmod_mode" || -n "$chown_user" || -n "$chgrp_group" ]]; then
        apply_perms "$path" "$chmod_mode" "$chown_user" "$chgrp_group" || true
      fi
      return 0
    fi

    log "FS: mkdir still failing after detach (rc=$rc2) path=$path err=${last_err2//$'\n'/ } ; skipping for now"
    return "$rc2"
  fi

  log "FS: mkdir -p failed rc=$rc path=$path err=${last_err//$'\n'/ }"
  return "$rc"
}



with_lock() {
  local lockfile="$1"; shift
  mkdir -p "$(dirname "$lockfile")"
  (
    flock -x 9
    "$@"
  ) 9>"$lockfile"
}

with_lock_try() {
  # Non-blocking lock helper: returns 0 if the lock was acquired and the command ran.
  # Returns 111 if the lock is currently held by someone else.
  local lockfile="$1"; shift
  mkdir -p "$(dirname "$lockfile")"
  (
    if ! flock -xn 9; then
      exit 111
    fi
    "$@"
  ) 9>"$lockfile"
}

# Best-effort: find PIDs holding an open FD to a given lock file (inode match).
find_lock_holder_pids() {
  local lockfile="$1"
  [[ -e "$lockfile" ]] || return 0

  local inode="" maj="" min=""
  inode="$(stat -c '%i' "$lockfile" 2>/dev/null || true)"
  maj="$(stat -c '%t' "$lockfile" 2>/dev/null || true)"
  min="$(stat -c '%T' "$lockfile" 2>/dev/null || true)"
  [[ -n "$inode" ]] || return 0

  # Fast path: /proc/locks already knows which PID(s) hold flock/POSIX locks.
  # This avoids scanning /proc/*/fd which can be very slow on busy Unraid hosts.
  if [[ -r /proc/locks ]]; then
    awk -v ino="$inode" -v maj="$maj" -v min="$min" '
      function normhex(x){ x=tolower(x); sub(/^0+/, "", x); return (x=="" ? "0" : x) }
      BEGIN { majn=normhex(maj); minn=normhex(min) }
      {
        # Typical format includes PID in field 5 and "maj:min:inode" in field 6.
        split($6, a, ":")
        if (length(a) >= 3) {
          if (a[3] == ino && normhex(a[1]) == majn && normhex(a[2]) == minn) print $5
        }
      }
    ' /proc/locks 2>/dev/null | sort -u
    return 0
  fi

  # Fallback: bounded scan of /proc/*/fd (best effort).
  local p pid fd ino cnt=0 max=8000
  for p in /proc/[0-9]*; do
    pid="${p#/proc/}"
    for fd in "$p"/fd/*; do
      ino="$(stat -Lc '%i' "$fd" 2>/dev/null || true)"
      [[ -n "$ino" ]] || continue
      if [[ "$ino" == "$inode" ]]; then
        printf '%s\n' "$pid"
        break
      fi
      ((cnt+=1))
      ((cnt >= max)) && break 2
    done
  done | sort -u
}


kill_lock_holders() {
  local lockfile="$1"
  local label="${2:-lock}"
  local pids=()
  mapfile -t pids < <(find_lock_holder_pids "$lockfile")
  (( ${#pids[@]} )) || return 1

  log "Supervisor: ${label} appears held; killing lock holder(s): ${pids[*]}"
  local p
  for p in "${pids[@]}"; do
    [[ "$p" =~ ^[0-9]+$ ]] || continue
    kill_tree "$p" TERM || true
  done
  sleep 1
  for p in "${pids[@]}"; do
    [[ "$p" =~ ^[0-9]+$ ]] || continue
    kill -0 "$p" 2>/dev/null && kill_tree "$p" KILL || true
  done
  return 0
}

kill_pid_from_file() {
  local f="$1"
  [[ -f "$f" ]] || return 0
  local pid=""
  pid="$(cat "$f" 2>/dev/null || true)"
  if [[ -n "${pid:-}" && "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null && ! is_zombie_pid "$pid"; then
    log "Supervisor: killing stale pid from $f: $pid"
    kill_tree "$pid" TERM || true
    sleep 1
    kill -0 "$pid" 2>/dev/null && kill_tree "$pid" KILL || true
  fi
  rm -f -- "$f" 2>/dev/null || true
}


lock_is_free() {
  local lockfile="$1"
  mkdir -p "$(dirname "$lockfile")" 2>/dev/null || true
  local __lfd
  exec {__lfd}>"$lockfile"
  if flock -n "$__lfd"; then
    exec {__lfd}>&- 2>/dev/null || true
    return 0
  fi
  exec {__lfd}>&- 2>/dev/null || true
  return 1
}

try_break_stale_worker_locks() {
  # If a previous run left orphaned workers holding locks, break them.
  mkdir -p "$MERGE_STATE_DIR" "$RENAME_STATE_DIR" 2>/dev/null || true

  # Ensure lockfiles exist so inode checks work
  : > "$MERGE_LOCK_FILE" 2>/dev/null || true
  : > "$RENAME_LOCK_FILE" 2>/dev/null || true

  # If locks are held, kill their holders.
  lock_is_free "$MERGE_LOCK_FILE" || kill_lock_holders "$MERGE_LOCK_FILE" "mergerfs lock" || true
  lock_is_free "$RENAME_LOCK_FILE" || kill_lock_holders "$RENAME_LOCK_FILE" "rename lock" || true

  # Also kill any stale child PIDs we previously recorded.
  kill_pid_from_file "$MERGE_PID_FILE" || true
  kill_pid_from_file "$RENAME_PID_FILE" || true
}


# Single-instance guard: use a flock-held lock file rather than scanning `ps` (which can hang on some Unraid setups).
SUPERVISOR_LOCK_FD=""

acquire_supervisor_lock() {
  mkdir -p "$STATE_DIR" 2>/dev/null || true
  # Bash 4+ supports allocating a free FD into a variable.
  exec {SUPERVISOR_LOCK_FD}>"$SUPERVISOR_LOCK_FILE"
  if ! flock -n "$SUPERVISOR_LOCK_FD"; then
    die "Another instance is running (lock held: $SUPERVISOR_LOCK_FILE)"
  fi
}

release_supervisor_lock() {
  if [[ -n "${SUPERVISOR_LOCK_FD:-}" ]]; then
    exec {SUPERVISOR_LOCK_FD}>&- 2>/dev/null || true
    SUPERVISOR_LOCK_FD=""
  fi
}

best_effort_low_prio_self() {
  (( LOW_PRIO )) || return 0
  command -v ionice >/dev/null 2>&1 && ionice -c2 -n5 -p $$ >/dev/null 2>&1 || true
  command -v renice >/dev/null 2>&1 && renice 10 $$ >/dev/null 2>&1 || true
}


best_effort_high_prio_self() {
  (( CLEANUP_HIGH_PRIO )) || return 0
  # Undo LOW_PRIO effects for time-sensitive shutdown work.
  command -v ionice >/dev/null 2>&1 && ionice -c2 -n0 -p $$ >/dev/null 2>&1 || true
  command -v renice >/dev/null 2>&1 && renice 0 $$ >/dev/null 2>&1 || true
}

best_effort_high_prio_pid() {
  (( CLEANUP_HIGH_PRIO )) || return 0
  local pid="${1:-}"
  [[ -n "${pid:-}" && "$pid" =~ ^[0-9]+$ ]] || return 0
  command -v ionice >/dev/null 2>&1 && ionice -c2 -n0 -p "$pid" >/dev/null 2>&1 || true
  command -v renice >/dev/null 2>&1 && renice 0 -p "$pid" >/dev/null 2>&1 || true
}



child_pids() {
  local pid="$1"
  if [[ -r "/proc/$pid/task/$pid/children" ]]; then
    tr ' ' '\n' <"/proc/$pid/task/$pid/children" 2>/dev/null || true
    return 0
  fi
  if command -v pgrep >/dev/null 2>&1; then
    pgrep -P "$pid" 2>/dev/null || true
    return 0
  fi
  if command -v ps >/dev/null 2>&1; then
    ps -o pid= --ppid "$pid" 2>/dev/null | awk '{print $1}' || true
    return 0
  fi
  return 0
}

# Kill a pid and its descendants (best effort)
kill_tree() {
  local pid="$1"
  local sig="${2:-TERM}"

  [[ -n "${pid:-}" ]] || return 0
  [[ "$pid" =~ ^[0-9]+$ ]] || return 0

  local kids=()
  mapfile -t kids < <(child_pids "$pid")
  local k
  for k in "${kids[@]}"; do
    kill_tree "$k" "$sig"
  done
  kill "-$sig" "$pid" 2>/dev/null || true
}

is_zombie_pid() {
  local pid="$1"
  [[ -n "${pid:-}" && "$pid" =~ ^[0-9]+$ ]] || return 1
  local st=""
  if [[ -r "/proc/$pid/stat" ]]; then
    # /proc/<pid>/stat: third field is process state (Z = zombie)
    st="$(awk '{print $3}' "/proc/$pid/stat" 2>/dev/null || true)"
  fi
  [[ "${st:-}" == "Z" ]]
}

# findmnt commonly escapes fstab-style sequences like \040 (space)
unescape_findmnt_string() {
  local s="$1"
  # findmnt / mountinfo style escaping uses backslash sequences like:
  #   space = \040 or \x20, tab = \011, newline = \012, backslash = \134
  # printf %b decodes both octal and \xHH forms.
  printf '%b' "$s"
}


# Extract KEY="value" fields from `findmnt -P` output lines without using eval.
findmnt_p_get() {
  local key="$1"
  local line="$2"
  local val=""
  if [[ "$line" =~ ${key}=\"([^\"]*)\" ]]; then
    val="${BASH_REMATCH[1]}"
  fi
  printf '%s' "$val"
}

mountopt_get_value() {
  # Extract a mount option value from a comma-separated option list, e.g. "fsname=foo,allow_other".
  # Usage: mountopt_get_value "<opts>" "<key>"  => prints value and returns 0 if found.
  local opts="$1" key="$2" part
  [[ -z "$opts" ]] && return 1
  local IFS=',' parts=()
  read -r -a parts <<<"$opts"
  for part in "${parts[@]}"; do
    if [[ "$part" == "$key="* ]]; then
      printf '%s\n' "${part#*=}"
      return 0
    fi
  done
  return 1
}



build_findmnt_snapshot() {
  # Build a snapshot of current mounts to avoid hundreds of per-title findmnt calls.
  # Args: <fstype_assoc_name> <source_assoc_name> <options_assoc_name>
  local __fst="$1" __src="$2" __opts="$3"
  declare -n fstype_assoc="$__fst"
  declare -n source_assoc="$__src"
  declare -n opts_assoc="$__opts"
  fstype_assoc=()
  source_assoc=()
  opts_assoc=()

  local line target fstype source opts
  while IFS= read -r line; do
    [[ -z "$line" ]] && continue
    target="$(unescape_findmnt_string "$(findmnt_p_get TARGET "$line")")"
    fstype="$(findmnt_p_get FSTYPE "$line")"
    source="$(unescape_findmnt_string "$(findmnt_p_get SOURCE "$line")")"
    opts="$(unescape_findmnt_string "$(findmnt_p_get OPTIONS "$line")")"
    [[ -z "$target" ]] && continue
    fstype_assoc["$target"]="$fstype"
    source_assoc["$target"]="$source"
    opts_assoc["$target"]="$opts"
  done < <(findmnt_pairs -o TARGET,FSTYPE,SOURCE,OPTIONS 2>/dev/null || true)
}


require_arg() {
  local opt="$1"
  local val="${2:-}"
  [[ -n "$val" ]] || die "Missing value for $opt"
}

require_int() {
  local opt="$1"
  local val="${2:-}"
  [[ -n "$val" ]] || die "Missing value for $opt"
  [[ "$val" =~ ^[0-9]+$ ]] || die "Invalid integer for $opt: $val"
}

# Read trigger file fields safely: prints first line (or empty)
read_merge_trigger_line() {
  local f="$1"
  [[ -f "$f" ]] || return 0
  local line=""
  IFS= read -r line < "$f" 2>/dev/null || true
  printf '%s' "$line"
}

# Generate a trigger id that won't collide even if multiple triggers happen within the same second.
make_trigger_id() {
  local now r
  now="$(date +%s 2>/dev/null || echo 0)"
  r="$(printf '%06d' "$RANDOM")"
  printf '%s-%s-%s' "$now" "$$" "$r"
}

trigger_mergerfs_scan() {
  local reason="${1:-event}"
  mkdir -p "$MERGE_STATE_DIR"
  if (( DRY_RUN )); then
    log "DRY-RUN: would trigger mergerfs scan ($reason)"
    return 0
  fi

  local id tmp
  id="$(make_trigger_id)"
  tmp="$MERGE_TRIGGER_FILE.tmp"
  printf "%s\t%s\n" "$id" "$reason" > "$tmp" 2>/dev/null || true
  mv -f -- "$tmp" "$MERGE_TRIGGER_FILE" 2>/dev/null || true
}

################################################################################
# Part A: Chapter rename watcher (delayed + quiet + whitelist/blacklist + rare rescan)
################################################################################

# Track known sources/manga so a lone chapter event can still trigger a mergerfs scan once.
declare -A SEEN_SOURCES=()
declare -A SEEN_MANGAS=()

init_seen_sets() {
  SEEN_SOURCES=()
  SEEN_MANGAS=()

  local roots=("$DOWNLOAD_ROOT")
  [[ -n "${CACHE_EQUIV:-}" && -d "$CACHE_EQUIV" ]] && roots+=("$CACHE_EQUIV")
  (( ${#DISK_EQUIVS[@]} )) && roots+=("${DISK_EQUIVS[@]}")

  local root src manga src_name manga_name
  local tmp_src tmp_manga

  for root in "${roots[@]}"; do
    tmp_src="$(tmpfile seen_src)"
    : > "$tmp_src" 2>/dev/null || true
    ro_timeout 20 find "$root" -mindepth 1 -maxdepth 1 -type d -print0 >"$tmp_src" 2>/dev/null || true

    while IFS= read -r -d '' src; do
      src_name="${src##*/}"
      [[ -n "$src_name" ]] || continue
      if is_excluded_source "$src_name"; then
        continue
      fi
      SEEN_SOURCES["$src_name"]=1

      tmp_manga="$(tmpfile seen_manga)"
      : > "$tmp_manga" 2>/dev/null || true
      ro_timeout 20 find "$src" -mindepth 1 -maxdepth 1 -type d -print0 >"$tmp_manga" 2>/dev/null || true

      while IFS= read -r -d '' manga; do
        manga_name="${manga##*/}"
        [[ -n "$manga_name" ]] || continue
        SEEN_MANGAS["$src_name/$manga_name"]=1
      done <"$tmp_manga"

      rm -f -- "$tmp_manga" 2>/dev/null || true
    done <"$tmp_src"

    rm -f -- "$tmp_src" 2>/dev/null || true
  done

  log "Watcher: init seen sets sources=${#SEEN_SOURCES[@]} mangas=${#SEEN_MANGAS[@]}"
}


matches_any_pattern_ci() {
  local text_lc="${1,,}"; shift
  local pat
  for pat in "$@"; do
    if [[ "$text_lc" =~ $pat ]]; then return 0; fi
  done
  return 1
}

is_blacklisted_prefix() { matches_any_pattern_ci "$1" "${PREFIX_BLACKLIST_PATTERNS[@]}"; }
is_whitelisted_prefix() { matches_any_pattern_ci "$1" "${PREFIX_WHITELIST_PATTERNS[@]}"; }

is_chapterish() {
  local s="${1,,}"
  [[ "$s" =~ (^|[^a-z])(ch\.|chapter|ep\.|episode|issue|special|extra|side|volume|vol\.)([^a-z]|$) ]]
}

looks_like_group_prefix() {
  local p="$1"
  is_blacklisted_prefix "$p" && return 1
  [[ "$p" =~ [A-Za-z] ]] && [[ "$p" =~ [0-9] ]]
}

sanitize_chapter_dirname() {
  local name="$1"

  # Case 1: Group_Chapter...
  if [[ "$name" == *"_"* ]]; then
    local prefix="${name%%_*}"
    local rest="${name#*_}"

    # If prefix has spaces, treat only the first token as "group" candidate.
    # This prevents stripping numbers from things like "(S2) Ep. 68 ..." where the number is meaningful.
    local prefix_tok="$prefix"
    local prefix_tail=""
    if [[ "$prefix" == *[[:space:]]* ]]; then
      prefix_tok="${prefix%%[[:space:]]*}"
      prefix_tail="${prefix#"$prefix_tok"}"   # includes leading space(s)
    fi

    if is_blacklisted_prefix "$prefix_tok"; then
      printf '%s' "$name"; return
    fi

    if ( is_whitelisted_prefix "$prefix_tok" || looks_like_group_prefix "$prefix_tok" ) && is_chapterish "$rest"; then
      local clean_tok="${prefix_tok//[0-9]/}"
      [[ -n "$clean_tok" ]] || { printf '%s' "$name"; return; }

      # Only replace/clean the token; keep the rest of the prefix phrase intact.
      printf '%s%s_%s' "$clean_tok" "$prefix_tail" "$rest"
      return
    fi

    printf '%s' "$name"; return
  fi


  # Case 2: Group123 Chapter 245 (no underscore)
  if [[ "$name" =~ ^([A-Za-z][A-Za-z0-9]*[0-9][A-Za-z0-9]*)[[:space:]]+(.+)$ ]]; then
    local prefix="${BASH_REMATCH[1]}"
    local rest="${BASH_REMATCH[2]}"
    local rest_lc="${rest,,}"

    is_blacklisted_prefix "$prefix" && { printf '%s' "$name"; return; }

    if ( is_whitelisted_prefix "$prefix" || looks_like_group_prefix "$prefix" ) \
       && [[ "$rest_lc" =~ ^(ch\.|chapter|ep\.|episode|issue|special|extra|side|season|volume|vol\.) ]]; then
      local clean="${prefix//[0-9]/}"
      [[ -n "$clean" ]] || { printf '%s' "$name"; return; }
      printf '%s %s' "$clean" "$rest"
      return
    fi
  fi

  printf '%s' "$name"
}

latest_mtime() {
  local path="$1"

  # IMPORTANT: Do not capture `find` output via $(...) (pipes can hang if find wedges in D state).
  # Instead, write to a temp file and read that file.
  local tmp="$(tmpfile mtime)"
  : > "$tmp" 2>/dev/null || true

  # Fast path: use find's %T@ (float seconds) when supported.
  ro_timeout 8 find "$path" -mindepth 1 -printf '%T@\n' >"$tmp" 2>/dev/null || true
  if [[ -s "$tmp" ]]; then
    awk '{ if ($1 > max) max=$1 } END { if (max=="") max=0; printf "%d\n", max }' "$tmp"
    rm -f -- "$tmp" 2>/dev/null || true
    return 0
  fi

  # Fallback: stat each entry (slower but more portable).
  : > "$tmp" 2>/dev/null || true
  ro_timeout 8 find "$path" -mindepth 1 -exec stat -c '%Y' {} + >"$tmp" 2>/dev/null || true
  if [[ -s "$tmp" ]]; then
    awk '{ if ($1 > max) max=$1 } END { if (max=="") max=0; printf "%d\n", max }' "$tmp"
    rm -f -- "$tmp" 2>/dev/null || true
    return 0
  fi

  rm -f -- "$tmp" 2>/dev/null || true
  printf '0\n'
}


is_quiet_enough() {
  local path="$1"
  local now last
  now="$(date +%s)"
  last="$(latest_mtime "$path")"
  if [[ "$last" -eq 0 ]]; then
    capture_first_line_timeout 3 last stat -c '%Y' "$path"
    [[ -n "${last:-}" ]] || last=0
  fi
  [[ "$last" -ne 0 ]] || return 1
  (( now - last >= RENAME_QUIET_SECONDS ))
}

rename_chapter_dir() {
  local full="$1"
  [[ -d "$full" ]] || return 0

  local base parent new cand
  base="${full##*/}"
  parent="$(dirname "$full")"
  new="$(sanitize_chapter_dirname "$base")"

  [[ "$new" != "$base" ]] || return 0

  cand="$new"
  if [[ -e "$parent/$cand" ]]; then
    for s in a b c d e f g h i j k l m n o p q r s t u v w x y z; do
      cand="${new}_alt-${s}"
      [[ -e "$parent/$cand" ]] || break
    done
    [[ -e "$parent/$cand" ]] && { log "RENAME: collision too many, skip: $full"; return 0; }
  fi

  log "RENAME: '$base' -> '$cand'"
  run_cmd mv -n -- "$full" "$parent/$cand" || { log "RENAME: mv failed, leaving as-is: $full"; return 0; }
}

_queue_contains_path_locked() {
  local path="$1"
  [[ -f "$RENAME_QUEUE_FILE" ]] || return 1
  awk -F $'\t' -v p="$path" '$2 == p { found=1; exit } END { exit (found?0:1) }' "$RENAME_QUEUE_FILE"
}

_enqueue_locked() {
  local allow_at="$1"
  local path="$2"
  mkdir -p "$RENAME_STATE_DIR"
  touch "$RENAME_QUEUE_FILE"

  _queue_contains_path_locked "$path" && return 0
  printf "%s\t%s\n" "$allow_at" "$path" >> "$RENAME_QUEUE_FILE"
  return 0
}

enqueue_chapter_path() {
  local path="${1%/}"

  # Only depth=3: <source>/<manga>/<chapter>
  local rel="${path#"$DOWNLOAD_ROOT/"}"
  [[ "$rel" != "$path" ]] || return 0
  local a="" b="" c="" rest=""
  local IFS='/'
  read -r a b c rest <<<"$rel"
  [[ -n "${a:-}" && -n "${b:-}" && -n "${c:-}" && -z "${rest:-}" ]] || return 0

  if is_excluded_source "$a"; then
    return 0
  fi

  local now allow_at
  now="$(date +%s)"
  allow_at=$(( now + RENAME_DELAY_SECONDS ))

  with_lock "$RENAME_LOCK_FILE" _enqueue_locked "$allow_at" "$path"
}

enqueue_chapters_under_source_dir() {
  local source_path_user="${1%/}"

  local _src_name
  _src_name="${source_path_user##*/}"
  if is_excluded_source "$_src_name"; then
    return 0
  fi

  # Build equivalent real roots (cache + disks) to avoid relying only on /mnt/user.
  local roots=()
  [[ -d "$source_path_user" ]] && roots+=("$source_path_user")

  if [[ "$source_path_user" == "$DOWNLOAD_ROOT/"* ]]; then
    local rel="${source_path_user#"$DOWNLOAD_ROOT"}"

    if [[ -n "${CACHE_EQUIV:-}" ]]; then
      local source_cache="$CACHE_EQUIV$rel"
      [[ -d "$source_cache" ]] && roots+=("$source_cache")
    fi

    if (( ${#DISK_EQUIVS[@]} )); then
      local _dr _cand
      for _dr in "${DISK_EQUIVS[@]}"; do
        _cand="$_dr$rel"
        [[ -d "$_cand" ]] && roots+=("$_cand")
      done
      unset _dr _cand
    fi
  fi

  (( ${#roots[@]} )) || return 0

  local queued=0 root ch
  for root in "${roots[@]}"; do
    local _tmp_ch="$(tmpfile chapters)"
    : > "$_tmp_ch" 2>/dev/null || true
    # Chapters are depth 2 under source: <source>/<manga>/<chapter>
    ro_timeout 30 find "$root" -mindepth 2 -maxdepth 2 -type d -print0 >"$_tmp_ch" 2>/dev/null || true
    while IFS= read -r -d '' ch; do
      ch="$(normalize_event_path_to_user "$ch")"
      enqueue_chapter_path "$ch" || true
      ((queued+=1)) || true
    done <"$_tmp_ch"
    rm -f -- "$_tmp_ch" 2>/dev/null || true
  done
  log "Watcher: enqueue chapters under new source: queued=${queued} path=$source_path_user"
}



enqueue_chapters_under_manga_dir() {
  local manga_path_user="${1%/}"

  local _rel _src
  _rel="${manga_path_user#"$DOWNLOAD_ROOT/"}"
  _src="${_rel%%/*}"
  if [[ "$_src" != "$_rel" ]] && is_excluded_source "$_src"; then
    return 0
  fi

  # Build equivalent real roots (cache + disks) to avoid relying only on /mnt/user.
  local roots=()
  [[ -d "$manga_path_user" ]] && roots+=("$manga_path_user")

  if [[ "$manga_path_user" == "$DOWNLOAD_ROOT/"* ]]; then
    local rel2="${manga_path_user#"$DOWNLOAD_ROOT"}"

    if [[ -n "${CACHE_EQUIV:-}" ]]; then
      local manga_cache="$CACHE_EQUIV$rel2"
      [[ -d "$manga_cache" ]] && roots+=("$manga_cache")
    fi

    if (( ${#DISK_EQUIVS[@]} )); then
      local _dr _cand
      for _dr in "${DISK_EQUIVS[@]}"; do
        _cand="$_dr$rel2"
        [[ -d "$_cand" ]] && roots+=("$_cand")
      done
      unset _dr _cand
    fi
  fi

  (( ${#roots[@]} )) || return 0

  local queued=0 root ch
  for root in "${roots[@]}"; do
    local _tmp_ch="$(tmpfile chapters)"
    : > "$_tmp_ch" 2>/dev/null || true
    # Chapters are depth 1 under manga: <manga>/<chapter>
    ro_timeout 30 find "$root" -mindepth 1 -maxdepth 1 -type d -print0 >"$_tmp_ch" 2>/dev/null || true
    while IFS= read -r -d '' ch; do
      ch="$(normalize_event_path_to_user "$ch")"
      enqueue_chapter_path "$ch" || true
      ((queued+=1)) || true
    done <"$_tmp_ch"
    rm -f -- "$_tmp_ch" 2>/dev/null || true
  done
  log "Watcher: enqueue chapters under new manga: queued=${queued} path=$manga_path_user"
}



handle_download_dir_event() {
  local path="${1%/}"
  local events="${2:-}"

  # Override-root events (mover / cache settings can create duplicate per-title override dirs).
  # These don't affect chapter renaming, but they *can* affect mergerfs branch selection and therefore
  # whether new/edited override files are visible through the union mount.
  if [[ "$path" == "$OVERRIDE_ROOT/"* ]]; then
    local orel="${path#"$OVERRIDE_ROOT/"}"
    local ot="${orel%%/*}"

    # If this is a file-level change (create/move-in/close-write/attrib), we prefer a fast scan so the
    # daemon can switch the primary override dir to the most-recently-updated real location if needed.
    if [[ "$events" == *"CLOSE_WRITE"* || "$events" == *"ATTRIB"* || "$events" == *"CREATE"* || "$events" == *"MOVED_TO"* ]]; then
      log "Watcher: override file change -> trigger mergerfs (force): $ot"
      trigger_mergerfs_scan "override-force:$ot"
    else
      log "Watcher: override dir change -> trigger mergerfs: $ot"
      trigger_mergerfs_scan "override:$ot"
    fi
    return 0
  fi

  local rel="${path#"$DOWNLOAD_ROOT/"}"
  [[ "$rel" != "$path" ]] || return 0

  local a="" b="" c="" rest=""
  local IFS='/'
  read -r a b c rest <<<"$rel"

  if [[ -n "${a:-}" ]] && is_excluded_source "$a"; then
    return 0
  fi

  # depth=1 (new source)
  if [[ -n "${a:-}" && -z "${b:-}" ]]; then
    SEEN_SOURCES["$a"]=1
    log "Watcher: new source dir -> trigger mergerfs: $a"
    trigger_mergerfs_scan "new-source:$a"
    enqueue_chapters_under_source_dir "$path" || true
    return 0
  fi

  # depth=2 (new manga)
  if [[ -n "${a:-}" && -n "${b:-}" && -z "${c:-}" ]]; then
    SEEN_SOURCES["$a"]=1
    SEEN_MANGAS["$a/$b"]=1
    log "Watcher: new manga dir -> trigger mergerfs: $a/$b"
    trigger_mergerfs_scan "new-manga:$a/$b"
    enqueue_chapters_under_manga_dir "$path" || true
    return 0
  fi

  # depth=3 (chapter)
  if [[ -n "${a:-}" && -n "${b:-}" && -n "${c:-}" && -z "${rest:-}" ]]; then
# If we haven't seen this source/manga yet, a lone chapter directory event might be the first signal.
if [[ -z "${SEEN_MANGAS[$a/$b]+x}" || -z "${SEEN_SOURCES[$a]+x}" ]]; then
  SEEN_SOURCES["$a"]=1
  SEEN_MANGAS["$a/$b"]=1
  log "Watcher: chapter dir implies new source/manga -> trigger mergerfs: $a/$b"
  trigger_mergerfs_scan "chapter-implied-new:$a/$b"
  enqueue_chapters_under_manga_dir "$DOWNLOAD_ROOT/$a/$b" || true
else
  # Even for an existing manga, a new chapter directory can appear on a *new underlying storage root*
  # (e.g. cache vs array). We normalize paths to /mnt/user for watcher logic, so trigger a scan here
  # to refresh branch discovery quickly.
  if [[ "$events" == *"CREATE"* || "$events" == *"MOVED_TO"* ]]; then
    log "Watcher: chapter dir created/moved -> trigger mergerfs refresh: $a/$b"
    trigger_mergerfs_scan "chapter-newdir:$a/$b"
  fi
fi
enqueue_chapter_path "$path" || true

  fi
}

_process_queue_locked() {
  mkdir -p "$RENAME_STATE_DIR"
  touch "$RENAME_QUEUE_FILE"

  local tmp="$RENAME_QUEUE_FILE.tmp"
  : > "$tmp"

  local now
  now="$(date +%s)"

  local allow_at="" path=""
  while IFS=$'\t' read -r allow_at path; do
    [[ -n "${allow_at:-}" && -n "${path:-}" ]] || continue

    # FIX: if path doesn't exist yet (e.g., cache event normalized to /mnt/user, but FUSE is lagging),
    # keep it queued for a grace window; drop only after it's "too old".
    if [[ ! -d "$path" ]]; then
      # allow_at is "earliest attempt"; use it as our age anchor
      if (( now - allow_at > RENAME_RESCAN_SECONDS )); then
        log "RENAME: dropping missing path after grace: $path"
      else
        printf "%s\t%s\n" "$allow_at" "$path" >> "$tmp"
      fi
      continue
    fi

    if (( now < allow_at )); then
      printf "%s\t%s\n" "$allow_at" "$path" >> "$tmp"
      continue
    fi

    if ! is_quiet_enough "$path"; then
      printf "%s\t%s\n" "$allow_at" "$path" >> "$tmp"
      continue
    fi

    rename_chapter_dir "$path"
  done < "$RENAME_QUEUE_FILE"

  mv -f -- "$tmp" "$RENAME_QUEUE_FILE"
}

process_rename_queue_once() {
  with_lock "$RENAME_LOCK_FILE" _process_queue_locked
}

# rescan BOTH /mnt/user and cache-equivalent roots (if present)
_build_rename_rescan_candidates() {
  local tmp="$1"
  : > "$tmp"

  local now
  now="$(date +%s)"

  local roots=("$DOWNLOAD_ROOT")
  [[ -n "${CACHE_EQUIV:-}" && -d "$CACHE_EQUIV" ]] && roots+=("$CACHE_EQUIV")
  (( ${#DISK_EQUIVS[@]} )) && roots+=("${DISK_EQUIVS[@]}")

  local root chapter_dir
  for root in "${roots[@]}"; do
    local _tmp_ch="$(tmpfile rescan_chapters)"
    : > "$_tmp_ch" 2>/dev/null || true
    ro_timeout 120 find "$root" -mindepth 3 -maxdepth 3 -type d -print0 >"$_tmp_ch" 2>/dev/null || true
    while IFS= read -r -d '' chapter_dir; do
      local chapter_user
      chapter_user="$(normalize_event_path_to_user "$chapter_dir")"

      local _rel _src
      _rel="${chapter_user#"$DOWNLOAD_ROOT/"}"
      _src="${_rel%%/*}"
      if [[ "$_src" != "$_rel" ]] && is_excluded_source "$_src"; then
        continue
      fi

      local base sanitized
      base="${chapter_user##*/}"
      sanitized="$(sanitize_chapter_dirname "$base")"
      [[ "$sanitized" != "$base" ]] || continue

      local dir_mtime allow_at
      capture_first_line_timeout 3 dir_mtime stat -c '%Y' "$chapter_dir"
      [[ -n "${dir_mtime:-}" ]] || dir_mtime=0
      if [[ "$dir_mtime" -le 0 ]]; then
        allow_at=$(( now + RENAME_DELAY_SECONDS ))
      else
        allow_at=$(( dir_mtime + RENAME_DELAY_SECONDS ))
        (( allow_at < now )) && allow_at="$now"
      fi

      printf "%s\t%s\n" "$allow_at" "$chapter_user" >> "$tmp"
    done <"$_tmp_ch"
    rm -f -- "$_tmp_ch" 2>/dev/null || true
  done
}

_merge_rename_rescan_candidates_locked() {
  local tmp="$1"
  mkdir -p "$RENAME_STATE_DIR"
  touch "$RENAME_QUEUE_FILE"

  local added=0 total=0
  local allow_at="" path=""
  while IFS=$'\t' read -r allow_at path; do
    [[ -n "${allow_at:-}" && -n "${path:-}" ]] || continue
    ((total+=1))
    # NOTE: don't require -d here; queue processor will hold until it appears (grace window).
    if ! _queue_contains_path_locked "$path"; then
      printf "%s\t%s\n" "$allow_at" "$path" >> "$RENAME_QUEUE_FILE"
      ((added+=1))
    fi
  done < "$tmp"

  log "Rename rescan: candidates=${total}, queued=${added}"
}

rename_rescan_and_enqueue() {
  mkdir -p "$RENAME_STATE_DIR"
  local tmp="$RENAME_STATE_DIR/rescan_candidates.tsv"
  _build_rename_rescan_candidates "$tmp"
  with_lock "$RENAME_LOCK_FILE" _merge_rename_rescan_candidates_locked "$tmp"
  rm -f -- "$tmp"
}

chapter_rename_daemon() {
  # Child must not inherit supervisor lock FD.
  release_supervisor_lock || true


  log "Chapter renamer: starting (delay=${RENAME_DELAY_SECONDS}s quiet=${RENAME_QUIET_SECONDS}s poll=${RENAME_POLL_SECONDS}s rescan=${RENAME_RESCAN_SECONDS}s)"
  init_seen_sets || true

  # Lower priority after initialization so startup work finishes promptly.
  best_effort_low_prio_self

  local queue_pid="" rescan_pid=""
  local ino_pid=""

  ( while true; do
      process_rename_queue_once || true
      sleep "$RENAME_POLL_SECONDS"
    done
  ) & queue_pid=$!

  (
    if (( RESCAN_NOW )); then
      rename_rescan_and_enqueue || true
    fi
    while true; do
      sleep "$RENAME_RESCAN_SECONDS"
      rename_rescan_and_enqueue || true
    done
  ) & rescan_pid=$!

_chapter_rename_cleanup() {
  # Never block here; workers can get stuck in uninterruptible I/O (D-state) if the array goes away.
  # That would prevent the supervisor (and --stop) from exiting promptly.
  local _p="" _st=""

  [[ -n "${queue_pid:-}" ]] && kill_tree "$queue_pid" TERM || true
  [[ -n "${rescan_pid:-}" ]] && kill_tree "$rescan_pid" TERM || true
  [[ -n "${ino_pid:-}" ]] && kill_tree "$ino_pid" TERM || true

  # Brief grace, then SIGKILL anything still alive.
  sleep 1
  for _p in "$queue_pid" "$rescan_pid" "$ino_pid"; do
    [[ -n "${_p:-}" && "$_p" =~ ^[0-9]+$ ]] || continue
    kill -0 "$_p" 2>/dev/null && kill_tree "$_p" KILL || true
  done

  # Reap only if already a zombie (avoid wait hangs).
  for _p in "$queue_pid" "$rescan_pid" "$ino_pid"; do
    [[ -n "${_p:-}" && "$_p" =~ ^[0-9]+$ ]] || continue
    if [[ -r "/proc/$_p/stat" ]]; then
      _st="$(awk '{print $3}' "/proc/$_p/stat" 2>/dev/null || true)"
      [[ "$_st" == "Z" ]] && wait "$_p" 2>/dev/null || true
    fi
  done
}

trap '_chapter_rename_cleanup; exit 0' INT TERM

  trap '_chapter_rename_cleanup' EXIT

  # Tell inotifywait to suppress its own "Watching new directory ..." chatter,
  # and (best-effort) exclude known feedback-loop sources like Suwayomi "Local source".
  local INO_EXCL_REGEX=""
  INO_EXCL_REGEX="$(build_inotify_exclude_regex)" || true
  local -a INO_EXCL_ARGS=()
  if [[ -n "$INO_EXCL_REGEX" ]]; then
    if inotifywait_supports_excludei; then
      INO_EXCL_ARGS=(--excludei "$INO_EXCL_REGEX")
    else
      INO_EXCL_ARGS=(--exclude "$INO_EXCL_REGEX")
    fi
  fi

  while true; do
    # Start watcher; if this fails, don't let set -e kill the whole daemon.

    init_watch_roots || true
    if ! coproc INO {
      inotifywait -qq -m -r -e create -e moved_to -e close_write -e attrib -e delete -e moved_from "${INO_EXCL_ARGS[@]}" --format '%w%f|%e' "${WATCH_ROOTS[@]}" 2>&3
    }; then
      log "Chapter renamer: failed to start inotifywait; retrying in 5s"
      sleep 5
      continue
    fi

    ino_pid="$INO_PID"
    local ino_rfd="${INO[0]}"
    local ino_wfd="${INO[1]}"

    # FIX: close numeric FDs stored in vars correctly (avoid leaks across restarts)
    if [[ "${ino_wfd:-}" =~ ^[0-9]+$ ]]; then
      exec {ino_wfd}>&- 2>/dev/null || true
    fi

    # Read events from the coprocess' stdout FD
    local ev_path="" events=""
    while IFS='|' read -r ev_path events <&"$ino_rfd"; do
      # Normalize first so override events are recognized even when they originate from /mnt/<pool> or /mnt/diskX.
      ev_path="$(normalize_event_path_to_user "$ev_path")"

      # For DOWNLOAD_ROOT we only care about directory-level events (source/manga/chapter).
      # For OVERRIDE_ROOT we also care about file events (e.g. details.json edits) so the mergerfs branch
      # selection can be updated promptly if mover/cache settings cause duplicate override dirs to appear.
      if [[ "$ev_path" != "$OVERRIDE_ROOT/"* ]]; then
        [[ "$events" == *"ISDIR"* ]] || continue

        # Delete noise under DOWNLOAD_ROOT isn't actionable and can cause pointless queue churn.
        if [[ "$events" == *"DELETE"* || "$events" == *"MOVED_FROM"* ]]; then
          continue
        fi
      fi

      handle_download_dir_event "$ev_path" "$events" || true
    done

    if [[ "${ino_rfd:-}" =~ ^[0-9]+$ ]]; then
      exec {ino_rfd}<&- 2>/dev/null || true
    fi

    local rc=0
    wait "$ino_pid" || rc=$?
    ino_pid=""

    log "Chapter renamer: inotify exited rc=$rc; restarting in 5s"
    sleep 5
  done
}

################################################################################
# Part B: MergerFS daemon (periodic rescan/remount + trigger-based scan)
################################################################################

declare -A EQUIV_CANON_BY_KEY=()
declare -A EQUIV_CANON_BY_NORM=()
declare -A SRC_PRIORITY=()
# When no manga_equivalents.txt entry matches, prefer an existing local-override directory name
# (case/punctuation-insensitive) so the mounted title stays stable across sources.
declare -A OVERRIDE_CANON_BY_NORM=()

# String normalization caches (persist for daemon lifetime).
declare -A __CACHE_NORMALIZE_TITLE=()
declare -A __CACHE_KEYIFY=()
declare -A __CACHE_NORMIFY=()
declare -A __CACHE_NORMIFY_NORM=()
declare -A __CACHE_SAFE_LINK_NAME=()

trim_spaces() {
  # Fast whitespace normalization without spawning sed/awk (hot path).
  # - Converts tabs/newlines to spaces
  # - Collapses runs of whitespace to a single space
  # - Trims leading/trailing spaces
  local s="${1-}"
  s="${s//$'\r'/}"
  s="${s//$'\t'/ }"
  s="${s//$'\n'/ }"

  local IFS=$' \t\n'
  local -a words=()
  # read will collapse whitespace for us
  read -r -a words <<<"$s" || true
  IFS=' '
  printf '%s' "${words[*]}"
}

normalize_title() {
  # Canonicalize a title for display/mount naming:
  # - underscores -> spaces
  # - trim/collapse whitespace
  # - strip known tag suffixes like "(Official)", "[Tapas]", "- Colored", etc.
  local in="${1-}"
  if [[ -n "${__CACHE_NORMALIZE_TITLE[$in]+x}" ]]; then
    printf '%s' "${__CACHE_NORMALIZE_TITLE[$in]}"
    return 0
  fi

  local t="$in"
  t="${t//_/ }"
  t="$(trim_spaces "$t")"

  local again=1
  while (( again )); do
    again=0

    if [[ "$t" =~ ^(.*)[[:space:]]*[\[\(]([^\]\)]{1,64})[\]\)][[:space:]]*$ ]]; then
      local base="${BASH_REMATCH[1]}" tag="${BASH_REMATCH[2]}"
      if is_tag_suffix "$tag"; then t="$(trim_spaces "$base")"; again=1; continue; fi
    fi

    if [[ "$t" =~ ^(.*)[[:space:]]*[-:][[:space:]]*([^[:space:]].{0,64})[[:space:]]*$ ]]; then
      local base="${BASH_REMATCH[1]}" tag="${BASH_REMATCH[2]}"
      if is_tag_suffix "$tag"; then t="$(trim_spaces "$base")"; again=1; continue; fi
    fi
  done

  __CACHE_NORMALIZE_TITLE["$in"]="$t"
  printf '%s' "$t"
}

ascii_fold() {
  # Best-effort ASCII folding for comparisons.
  # Avoid spawning iconv for plain ASCII strings (major speed win).
  local s="${1-}"

  local _old_lc="${LC_ALL-}"
  LC_ALL=C
  if [[ "$s" != *[$'?'-$'?']* ]]; then
    if [[ -n "$_old_lc" ]]; then LC_ALL="$_old_lc"; else unset LC_ALL; fi
    printf '%s' "$s"
    return 0
  fi
  if [[ -n "$_old_lc" ]]; then LC_ALL="$_old_lc"; else unset LC_ALL; fi

  if command -v iconv >/dev/null 2>&1; then
    local out=""
    out="$(printf '%s' "$s" | iconv -f UTF-8 -t ASCII//TRANSLIT 2>/dev/null || true)"
    if [[ -n "$out" ]]; then
      printf '%s' "$out"
      return 0
    fi
  fi
  printf '%s' "$s"
}


normify() {
  # "Normalization key" for comparing/merging titles (case/spacing/punctuation-insensitive).
  local in="${1-}"
  if [[ -n "${__CACHE_NORMIFY[$in]+x}" ]]; then
    printf '%s' "${__CACHE_NORMIFY[$in]}"
    return 0
  fi

  local t
  t="$(normalize_title "$in")"
  t="$(trim_spaces "$t")"
  t="${t,,}"
  t="$(ascii_fold "$t")"

  local _old_lc="${LC_ALL-}"
  LC_ALL=C
  t="${t//[^[:alnum:]]/}"
  if [[ -n "${_old_lc+x}" && -n "$_old_lc" ]]; then LC_ALL="$_old_lc"; else unset LC_ALL; fi

  __CACHE_NORMIFY["$in"]="$t"
  printf '%s' "$t"
}

normify_from_normalized() {
  # Like normify(), but assumes input is already title-normalized (underscores removed, tags stripped).
  local in="${1-}"
  if [[ -n "${__CACHE_NORMIFY_FROM_NORM[$in]+x}" ]]; then
    printf '%s' "${__CACHE_NORMIFY_FROM_NORM[$in]}"
    return 0
  fi

  local s="$in"
  s="$(trim_spaces "$s")"
  s="${s,,}"
  s="$(ascii_fold "$s")"

  local _old_lc="${LC_ALL-}"
  LC_ALL=C
  s="${s//[^[:alnum:]]/}"
  if [[ -n "${_old_lc+x}" && -n "$_old_lc" ]]; then LC_ALL="$_old_lc"; else unset LC_ALL; fi

  __CACHE_NORMIFY_FROM_NORM["$in"]="$s"
  printf '%s' "$s"
}

keyify() {
  # "Key" used for stable hashing/IDs: lowercased, alnum+space only.
  # (Unlike normify it preserves spaces, so it's readable-ish.)
  local in="${1-}"
  local k="${in,,}"
  if [[ -n "${__CACHE_KEYIFY[$k]+x}" ]]; then
    printf '%s' "${__CACHE_KEYIFY[$k]}"
    return 0
  fi

  local out="$k"
  local _old_lc="${LC_ALL-}"
  LC_ALL=C
  out="${out//[^[:alnum:]]/ }"
  if [[ -n "${_old_lc+x}" && -n "$_old_lc" ]]; then LC_ALL="$_old_lc"; else unset LC_ALL; fi

  out="$(trim_spaces "$out")"
  __CACHE_KEYIFY["$k"]="$out"
  printf '%s' "$out"
}

safe_link_name() {
  # Create a filesystem-safe link name (for branchlink dirs) without spawning external tools.
  # Behavior matches prior sed version:
  # - Replace runs of non-alnum with underscore
  # - Trim leading/trailing underscores
  local in="${1-}"
  if [[ -n "${__CACHE_SAFE_LINK_NAME[$in]+x}" ]]; then
    printf '%s' "${__CACHE_SAFE_LINK_NAME[$in]}"
    return 0
  fi

  local s="$in"
  local _old_lc="${LC_ALL-}"
  LC_ALL=C

  local was_extglob=0
  shopt -q extglob && was_extglob=1
  shopt -s extglob

  s="${s//+([^[:alnum:]])/_}"
  s="${s##+(_)}"
  s="${s%%+(_)}"

  (( was_extglob == 1 )) || shopt -u extglob

  if [[ -n "${_old_lc+x}" && -n "$_old_lc" ]]; then LC_ALL="$_old_lc"; else unset LC_ALL; fi

  [[ -z "$s" ]] && s="x"
  __CACHE_SAFE_LINK_NAME["$in"]="$s"
  printf '%s' "$s"
}

join_by() { local IFS="$1"; shift; printf '%s' "$*"; }

load_equivs() {
  local f="$1"
  [[ -f "$f" ]] || return 0

  local line canonical canon_disp canon_key canon_norm part alias_disp alias_key alias_norm i
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%$'\r'}"
    line="$(trim_spaces "$line")"
    [[ -z "$line" ]] && continue
    [[ "${line:0:1}" == "#" ]] && continue

    local parts=()
    IFS='|' read -r -a parts <<<"$line"
    (( ${#parts[@]} )) || continue

    canonical="$(trim_spaces "${parts[0]}")"
    [[ -z "$canonical" ]] && continue

    canon_disp="$(normalize_title "$canonical")"
    [[ -z "${canon_disp// }" ]] && canon_disp="$(trim_spaces "$canonical")"

    canon_key="$(keyify "$canon_disp")"
    [[ -z "$canon_key" ]] && canon_key="$(keyify "$canonical")"
    [[ -z "$canon_key" ]] && continue

    EQUIV_CANON_BY_KEY["$canon_key"]="$canon_disp"

    canon_norm="$(normify_from_normalized "$canon_disp")"
    [[ -n "$canon_norm" ]] && EQUIV_CANON_BY_NORM["$canon_norm"]="$canon_disp"

    for (( i=0; i<${#parts[@]}; i++ )); do
      part="$(trim_spaces "${parts[i]}")"
      [[ -z "$part" ]] && continue
      [[ "${part:0:1}" == "#" ]] && continue
      alias_disp="$(normalize_title "$part")"
      [[ -z "${alias_disp// }" ]] && alias_disp="$(trim_spaces "$part")"
      alias_key="$(keyify "$alias_disp")"
      [[ -z "$alias_key" ]] && continue
      EQUIV_CANON_BY_KEY["$alias_key"]="$canon_disp"
      alias_norm="$(normify_from_normalized "$alias_disp")"
      [[ -n "$alias_norm" ]] && EQUIV_CANON_BY_NORM["$alias_norm"]="$canon_disp"
    done
  done < "$f"
}

load_priority() {
  local f="$1"
  [[ -f "$f" ]] || return 0
  local idx=0 line
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="$(trim_spaces "$line")"
    [[ -z "$line" ]] && continue
    [[ "${line:0:1}" == "#" ]] && continue
    SRC_PRIORITY["$line"]="$idx"
    ((idx+=1))
  done < "$f"
}

# Build an index of existing override title directories so we can reuse the exact
# capitalization/punctuation Suwayomi already knows, even if new sources introduce
# slightly different spellings.
#
# Key: normify(title_dir_name)
# Val: exact directory name as it exists in local-override
build_override_canon_index() {
  OVERRIDE_CANON_BY_NORM=()

  local -a roots=()
  if [[ "$OVERRIDE_ROOT" == /mnt/user/* ]]; then
    local rel="${OVERRIDE_ROOT#/mnt/user}"  # includes leading /
    # Pool/cache first so it wins ties.
    if [[ -n "${UNRAID_CACHE_POOL:-}" && -d "/mnt/${UNRAID_CACHE_POOL}${rel}" ]]; then
      roots+=("/mnt/${UNRAID_CACHE_POOL}${rel}")
    fi
    local d cand
    for d in /mnt/disk*; do
      [[ -d "$d" ]] || continue
      cand="$d${rel}"
      [[ -d "$cand" ]] && roots+=("$cand")
    done
  else
    [[ -d "${OVERRIDE_ROOT_REAL:-}" ]] && roots+=("$OVERRIDE_ROOT_REAL")
    [[ -d "$OVERRIDE_ROOT" && "$OVERRIDE_ROOT" != "${OVERRIDE_ROOT_REAL:-}" ]] && roots+=("$OVERRIDE_ROOT")
  fi

  (( ${#roots[@]} )) || return 0

  local root tmp dir base key
  for root in "${roots[@]}"; do
    [[ -d "$root" ]] || continue
    tmp="$(tmpfile overrideidx)"
    ro_timeout 20 find "$root" -mindepth 1 -maxdepth 1 -type d -print0 >"$tmp" 2>/dev/null || true
    while IFS= read -r -d '' dir; do
      base="${dir##*/}"
      key="$(normify "$base")"
      [[ -n "$key" ]] || continue
      # First hit wins (pool/cache roots come first).
      [[ -n "${OVERRIDE_CANON_BY_NORM[$key]+x}" ]] && continue
      OVERRIDE_CANON_BY_NORM["$key"]="$base"
    done <"$tmp"
    rm -f -- "$tmp" 2>/dev/null || true
  done
}

override_canon_for_norm() {
  local key="${1:-}"
  [[ -n "$key" ]] || return 0
  if [[ -n "${OVERRIDE_CANON_BY_NORM[$key]+x}" ]]; then
    printf '%s' "${OVERRIDE_CANON_BY_NORM[$key]}"
  fi
}

priority_of_source() {
  local s="$1"
  if [[ -n "${SRC_PRIORITY[$s]+x}" ]]; then printf '%d' "${SRC_PRIORITY[$s]}"; else printf '%d' 9999; fi
}

FUSERMOUNT_BIN=""
try_pick_fusermount() {
  # Prefer fusermount3 when available (covers fuse3.* mounts).
  if command -v fusermount3 >/dev/null 2>&1; then FUSERMOUNT_BIN="fusermount3"; return 0; fi
  if command -v fusermount >/dev/null 2>&1; then FUSERMOUNT_BIN="fusermount"; return 0; fi
  return 1
}

pick_fusermount() {
  try_pick_fusermount && return 0
  die "Need fusermount or fusermount3"
}

is_mounted_mergerfs() {
  local target="$1"
  local fstype=""
  capture_first_line_timeout 5 fstype findmnt -rn -T "$target" -o FSTYPE
  [[ "${fstype,,}" == *mergerfs* ]]
}

mountinfo_escape_path() {
  # /proc/*/mountinfo escapes spaces and other chars as octal (e.g. ' ' -> '\040').
  local p="$1"
  p="${p//\\/\\\\}"
  p="${p// /\\040}"
  p="${p//$'\t'/\\011}"
  p="${p//$'\n'/\\012}"
  printf '%s\n' "$p"
}

mountinfo_unescape_path() {
  # Convert /proc/*/mountinfo octal escapes back to characters.
  # We only need the common ones used for mountpoint paths.
  local p="$1"
  p="${p//\\040/ }"
  p="${p//\\011/$'\t'}"
  p="${p//\\012/$'\n'}"
  p="${p//\\134/\\}"
  printf '%s\n' "$p"
}

mountinfo_fstype_for_mp() {
  # Get fstype for an exact mountpoint path using /proc/self/mountinfo (no stat).
  local mp="$1"
  local esc; esc="$(mountinfo_escape_path "$mp")"
  awk -v m="$esc" '
    $5==m {
      for (i=1; i<=NF; i++) {
        if ($i=="-") { print $(i+1); exit }
      }
    }
  ' /proc/self/mountinfo 2>/dev/null || true
}

longest_mounted_ancestor() {
  # Return the longest mounted ancestor (mountpoint) for a given path, if any, using mountinfo.
  local path="$1"
  local esc_path; esc_path="$(mountinfo_escape_path "$path")"
  local best="" best_len=0
  local mp
  while IFS= read -r mp; do
    [[ -z "$mp" ]] && continue
    if [[ "$esc_path" == "$mp" || "$esc_path" == "$mp/"* ]]; then
      local l=${#mp}
      if (( l > best_len )); then
        best="$mp"
        best_len=$l
      fi
    fi
  done < <(awk '{print $5}' /proc/self/mountinfo 2>/dev/null || true)
  if [[ -n "$best" ]]; then
    mountinfo_unescape_path "$best"
    return 0
  fi
  return 1
}

list_mountpoints_under_prefix() {
  # List mountpoints at/under a prefix (deepest first), using mountinfo (no stat).
  local prefix="$1"
  local escp; escp="$(mountinfo_escape_path "$prefix")"
  awk -v p="$escp" '
    { mp=$5 }
    (mp==p || index(mp, p"/")==1) { print length(mp) "\t" mp }
  ' /proc/self/mountinfo 2>/dev/null | sort -nr | cut -f2- | while IFS= read -r mp; do
    mountinfo_unescape_path "$mp"
  done
}

dir_accessible_fast() {
  local p="$1"
  run_cmd_timeout_fast 1 test -d -- "$p" >/dev/null 2>&1
}

detach_fuse_mounts_under() {
  # Detach FUSE mounts under a prefix (deepest first) using mountinfo only (no stat).
  # Used when we detect TEIN, which typically means stale FUSE mounts are wedged.
  local prefix="$1"
  local mp fstype
  while IFS= read -r mp; do
    [[ -z "$mp" ]] && continue
    fstype="$(mountinfo_fstype_for_mp "$mp")"
    # Only touch FUSE mounts; never unmount real system mounts by accident.
    if [[ "$fstype" == fuse.* || "$fstype" == *mergerfs* ]]; then
      log "MergerFS: detach FUSE mount -> $mp"
      unmount_path "$mp" || log "MergerFS: WARN: failed to detach mount: $mp"
    fi
  done < <(list_mountpoints_under_prefix "$prefix")
}

# Backwards-compatible name (older versions called this).
cleanup_stale_fuse_mounts_under() {
  detach_fuse_mounts_under "$1"
}


is_mounted_any() {
  # IMPORTANT:
  # This must answer: "is there a filesystem mounted *at this exact path*?"
  #
  # Do NOT use findmnt -T here (it returns the containing filesystem for any path),
  # otherwise we end up trying to "unmount" ordinary directories and we will
  # falsely think mounts are still present after a lazy detach.
  local target="$1"
  [[ -z "$target" ]] && return 1

  # Fast + safe: read /proc/self/mountinfo (no stat; works even when the mount is wedged).
  local fstype
  fstype="$(mountinfo_fstype_for_mp "$target")"
  [[ -n "$fstype" ]] && return 0

  # Fallback: exact mountpoint match via findmnt -M (timeboxed).
  local tgt=""
  capture_first_line_timeout 3 tgt findmnt -rn -M "$target" -o TARGET 2>/dev/null || true
  tgt="$(unescape_findmnt_string "$tgt")"
  [[ -n "$tgt" && "$tgt" == "$target" ]]
}

mountpoint_is_healthy() {
  local mp="$1"
  # Health checks are optional and OFF by default (see ENABLE_MOUNT_HEALTHCHECK).
  if [[ "${ENABLE_MOUNT_HEALTHCHECK}" != "1" ]]; then
    return 0
  fi
  # If the kernel still thinks it's mounted but the FUSE server is wedged, simple stat/tests will hang.
  # Keep this cheap and ALWAYS time-bounded.
  is_mounted_any "$mp" || return 1
  run_cmd_timeout_fast 2 test -d -- "$mp" >/dev/null 2>&1
}

unmount_path() {
  local target="$1"
  [[ -z "$target" ]] && return 1

  local attempt
  for attempt in 1 2 3; do
    # If it's not mounted, we're done.
    if ! is_mounted_any "$target"; then
      return 0
    fi

    # Prefer fusermount for FUSE mounts (works even when running as non-root on Unraid).
    if [[ -n "${FUSERMOUNT_BIN:-}" ]]; then
      run_cmd_timeout_fast 5 "$FUSERMOUNT_BIN" -uz -- "$target" >/dev/null 2>&1 && return 0
      run_cmd_timeout_fast 5 "$FUSERMOUNT_BIN" -u  -- "$target" >/dev/null 2>&1 || true
    fi

    # Lazy unmount: avoids EBUSY from active readers (e.g. docker container browsing the mount).
    run_cmd_timeout_fast 5 umount -l  -- "$target" >/dev/null 2>&1 && return 0

    # Lazy+force (supported by util-linux umount; ignored if unsupported).
    run_cmd_timeout_fast 5 umount -lf -- "$target" >/dev/null 2>&1 || true

    # Force unmount as a last resort.
    run_cmd_timeout_fast 5 umount -f  -- "$target" >/dev/null 2>&1 || true

    if ! is_mounted_any "$target"; then
      return 0
    fi

    sleep 0.1
  done

  return 1
}

mount_union() {
  local mountpoint="$1"
  local branch_string="$2"
  local fsname="${3:-}"

  log "MergerFS: mount -> $mountpoint"
  if (( DRY_RUN )); then
    log "DRY-RUN: would mount mergerfs at $mountpoint"
    return 0
  fi
  if ! command -v mergerfs >/dev/null 2>&1; then
    log "MergerFS: mount failed (mergerfs not found in PATH)"
    return 1
  fi

  # IMPORTANT:
  # If we do not set an explicit fsname, mergerfs may embed the branch list into
  # the fsname/source string. When branch paths contain commas (e.g. titles with
  # commas), that can break FUSE's comma-separated option parsing and cause mount
  # failures like: "fuse: ERROR - unknown option".
  local opts="$MERGERFS_OPTS_BASE"
  if [[ -n "$fsname" ]]; then
    opts+=",fsname=$fsname"
  fi

  local rc=0
  run_cmd_timeout 20 mergerfs -o "$opts" "$branch_string" "$mountpoint" >&3 2>&3 || rc=$?
  if (( rc != 0 )); then
    log "MergerFS: mount failed rc=$rc mp=$mountpoint"
    return 1
  fi
  return 0
}

json_escape() {
  local s="$1"
  s="${s//\\/\\\\}"
  s="${s//\"/\\\"}"
  s="${s//$'\n'/\\n}"
  printf '%s' "$s"
}

# === ComicInfo.xml -> details.json helpers (only used on first creation) ===

xml_unescape_basic() {
  local s="$1"
  s="${s//&lt;/<}"
  s="${s//&gt;/>}"
  s="${s//&quot;/\"}"
  s="${s//&apos;/$'\''}"
  s="${s//&amp;/&}"
  printf '%s' "$s"
}

xml_unescape_numeric_newlines() {
  # Decode common XML numeric character references for newlines/carriage returns.
  # Many ComicInfo.xml generators store line breaks as &#10; / &#xA; etc.
  local s="$1"
  s="${s//&#10;/$'\n'}"
  s="${s//&#13;/$'\r'}"
  s="${s//&#x0A;/$'\n'}"
  s="${s//&#xA;/$'\n'}"
  s="${s//&#x0D;/$'\r'}"
  s="${s//&#xD;/$'\r'}"
  printf '%s' "$s"
}


find_latest_comicinfo_xml() {
  local manga_dir="$1"
  local __outvar="${2:-}"

  # NOTE: This function is often used to "return" a value (the ComicInfo.xml path).
  # Do NOT use command substitution around it in callers; prefer the outvar form to avoid
  # subshell quirks and to keep logs out of captured stdout.
  local _set_out
  _set_out() {
    local v="${1-}"
    if [[ -n "$__outvar" ]]; then
      printf -v "$__outvar" '%s' "$v"
    else
      printf '%s' "$v"
    fi
  }

  [[ -d "$manga_dir" ]] || { _set_out ""; return 0; }

  # Some Unraid setups can temporarily expose new writes more reliably in the cache-pool
  # path than through /mnt/user (FUSE). For metadata seeding, try both views.
  local candidates=("$manga_dir")
  if [[ -n "${UNRAID_CACHE_POOL:-}" ]]; then
    local alt=""
    if [[ "$manga_dir" == /mnt/user/* ]]; then
      alt="/mnt/${UNRAID_CACHE_POOL}${manga_dir#/mnt/user}"
    elif [[ "$manga_dir" == /mnt/${UNRAID_CACHE_POOL}/* ]]; then
      alt="/mnt/user${manga_dir#/mnt/${UNRAID_CACHE_POOL}}"
    fi
    if [[ -n "$alt" && "$alt" != "$manga_dir" && -d "$alt" ]]; then
      candidates+=("$alt")
    fi
  fi

  local dir out=""
  # Use ro_timeout so filesystem stalls won't hang the daemon.

  # Common layout: <manga>/<chapter>/ComicInfo.xml
  for dir in "${candidates[@]}"; do
    if (( DEBUG_COMICINFO )); then
      log "DEBUG ComicInfo: search dir=$dir"
    fi

    capture_first_line_timeout 20 out find "$dir" -mindepth 2 -maxdepth 2 -type f -name 'ComicInfo.xml' -print -quit

    if [[ -n "$out" ]]; then
      if (( DEBUG_COMICINFO )); then
        log "DEBUG ComicInfo: FOUND=$out"
      fi
      _set_out "$out"
      return 0
    fi
  done

  # Fallback: tolerate extra nesting levels (volume/season buckets, etc.).
  for dir in "${candidates[@]}"; do
    capture_first_line_timeout 25 out find "$dir" -mindepth 2 -maxdepth 6 -type f -name 'ComicInfo.xml' -print -quit
    if [[ -n "$out" ]]; then
      if (( DEBUG_COMICINFO )); then
        log "DEBUG ComicInfo: FOUND(deep)=$out"
      fi
      _set_out "$out"
      return 0
    fi
  done

  if (( DEBUG_COMICINFO )); then
    log "DEBUG ComicInfo: not found under $manga_dir"
  fi
  _set_out ""
  return 0
}


status_to_code() {
  local s="${1:-}"
  s="$(trim_spaces "$s")"
  local lc="${s,,}"

  [[ -n "$lc" ]] || { printf '0'; return; }

  if [[ "$lc" == "ongoing" || "$lc" == *"ongoing"* || "$lc" == *"publishing"* || "$lc" == *"serialization"* ]]; then
    printf '1'; return
  fi
  if [[ "$lc" == "completed" || "$lc" == *"completed"* || "$lc" == "complete" || "$lc" == *"finished"* || "$lc" == *"ended"* ]]; then
    printf '2'; return
  fi
  if [[ "$lc" == "licensed" || "$lc" == *"licensed"* ]]; then
    printf '3'; return
  fi
  printf '0'
}

write_details_json_from_comicinfo() {
  local xml_path="$1"
  local fallback_title="$2"
  local out_path="$3"

  [[ -f "$xml_path" ]] || return 1

  local tmp="$out_path.tmp"
  rm -f -- "$tmp" 2>/dev/null || true


  # XML parsing (Unraid-friendly: awk/sed only).

  # Shell fallback: minimal XML extraction (best-effort).
  local US=$'\037'
  local parsed=""

  # Prefer awk (fast, commonly present). Encode Summary embedded newlines as literal "\n"
  # so bash `read` below doesn't truncate at the first newline.
  if command -v awk >/dev/null 2>&1; then
    parsed="$(
      awk -v US="$US" '
        function trim(s){ sub(/^[[:space:]]+/, "", s); sub(/[[:space:]]+$/, "", s); return s }
        BEGIN{ series=""; writer=""; penc=""; genre=""; status=""; summary=""; in_sum=0 }
        {
          line=$0
          gsub(/\r/, "", line)

          if (in_sum) {
            if (line ~ /<\/Summary>/) {
              sub(/<\/Summary>.*/, "", line)
              if (summary != "") summary = summary "\\n" line; else summary = line
              in_sum=0
            } else {
              if (summary != "") summary = summary "\\n" line; else summary = line
            }
            next
          }

          if (line ~ /<Summary>/) {
            sub(/^.*<Summary>/, "", line)
            if (line ~ /<\/Summary>/) {
              sub(/<\/Summary>.*/, "", line)
              summary = line
              in_sum=0
            } else {
              summary = line
              in_sum=1
            }
            next
          }

          if (series=="" && line ~ /<Series>/) { sub(/^.*<Series>/, "", line); sub(/<\/Series>.*/, "", line); series=trim(line); next }
          if (writer=="" && line ~ /<Writer>/) { sub(/^.*<Writer>/, "", line); sub(/<\/Writer>.*/, "", line); writer=trim(line); next }
          if (penc=="" && line ~ /<Penciller>/) { sub(/^.*<Penciller>/, "", line); sub(/<\/Penciller>.*/, "", line); penc=trim(line); next }
          if (genre=="" && line ~ /<Genre>/) { sub(/^.*<Genre>/, "", line); sub(/<\/Genre>.*/, "", line); genre=trim(line); next }
          if (status=="" && line ~ /<Status>/) { sub(/^.*<Status>/, "", line); sub(/<\/Status>.*/, "", line); status=trim(line); next }
          if (status=="" && line ~ /PublishingStatusTachiyomi/) {
            sub(/^.*PublishingStatusTachiyomi[^>]*>/, "", line)
            sub(/<\/.*$/, "", line)
            status=trim(line)
            next
          }
        }
        END{
          printf "%s%s%s%s%s%s%s%s%s%s%s", series, US, writer, US, penc, US, summary, US, genre, US, status
        }
      ' "$xml_path" 2>/dev/null || true
    )"
  fi

  # Fallback to perl (handles some edge cases / stripped environments).
  if [[ -z "$parsed" ]] && command -v perl >/dev/null 2>&1; then
    parsed="$(
      US="$US" perl -0777 -ne '
        my $US = $ENV{"US"} // "\x1f";
        my $s  = $_;

        sub grab {
          my ($tag) = @_;
          if ($s =~ m{<\Q$tag\E\b[^>]*>(.*?)</\Q$tag\E>}is) { return $1; }
          return "";
        }

        my $series  = grab("Series");
        my $writer  = grab("Writer");
        my $penc    = grab("Penciller");
        my $summary = grab("Summary");
        my $genre   = grab("Genre");
        my $status  = grab("Status");

        if (!$status) {
          if ($s =~ m{PublishingStatusTachiyomi\b[^>]*>(.*?)</[^>]*PublishingStatusTachiyomi>}is) { $status = $1; }
        }

        for ($series,$writer,$penc,$genre,$status) { s/^\s+|\s+$//g if defined; }

        $summary =~ s/\r//g;
        $summary =~ s/\n/\\n/g;  # encode embedded newlines

        print $series, $US, $writer, $US, $penc, $US, $summary, $US, $genre, $US, $status;
      ' "$xml_path" 2>/dev/null || true
    )"
  fi

  [[ -n "$parsed" ]] || { rm -f -- "$tmp" 2>/dev/null || true; return 1; }

  local series writer penc summary genre status
  IFS="$US" read -r series writer penc summary genre status <<<"$parsed"

  series="$(xml_unescape_basic "$series")"
  writer="$(xml_unescape_basic "$writer")"
  penc="$(xml_unescape_basic "$penc")"
  summary="$(xml_unescape_basic "$summary")"
  # Decode embedded Summary newlines (stored as literal "\n" sequences by the parser).
  summary="${summary//$'\\n'/$'\n'}"
  # Decode numeric newline entities some ComicInfo.xml writers use (&#10; / &#xA; etc.).
  summary="$(xml_unescape_numeric_newlines "$summary")"

  genre="$(xml_unescape_basic "$genre")"
  status="$(xml_unescape_basic "$status")"

  # Normalize common HTML line breaks in the summary (best-effort).
  if command -v awk >/dev/null 2>&1; then
    summary="$(printf '%s' "$summary" | awk '{gsub(/<[bB][rR][[:space:]]*\/?>/,"\n"); print }')"
  fi

  # Many UIs (especially web-based) will collapse raw newlines in text. If
  # DETAILS_DESC_MODE is "br", convert newlines to HTML <br /> so line breaks render.
  if [[ "${DETAILS_DESC_MODE:-br}" == "br" || "${DETAILS_DESC_MODE:-br}" == "html" ]]; then
    summary="${summary//$'\r'/}"
    summary="${summary//$'\n'/<br />$'\n'}"
  fi
  # IMPORTANT: Use the sanitized/normalized display title for details.json "title".
  # Do NOT use ComicInfo.xml <Series> here: sources often include tags like "(official)", "[uncensored]", etc.
  # This daemon exists to sanitize names and group multiple variants under a single canonical title.
  # IMPORTANT: Always use the sanitized/normalized title (fallback_title).
  # Do NOT use ComicInfo.xml <Series> here: sources often include tags like "(official)",
  # "[uncensored]", etc. This daemon exists to sanitize names and group variants.
  series="$(trim_spaces "$fallback_title")"
  writer="$(trim_spaces "$writer")"
  penc="$(trim_spaces "$penc")"

  # Build genre array JSON
  local genres_json="[]"
  if [[ -n "${genre// }" ]]; then
    local parts=() part esc out=""
    local genre_norm="${genre//;/,}"
    IFS=',' read -r -a parts <<<"$genre_norm"
    for part in "${parts[@]}"; do
      part="$(trim_spaces "$part")"
      [[ -n "$part" ]] || continue
      esc="$(json_escape "$part")"
      if [[ -n "$out" ]]; then out+=", "; fi
      out+="\"$esc\""
    done
    genres_json="[$out]"
  fi

  local esc_title esc_author esc_artist esc_desc status_code
  esc_title="$(json_escape "$series")"
  esc_author="$(json_escape "$writer")"
  esc_artist="$(json_escape "$penc")"
  esc_desc="$(json_escape "$summary")"
  status_code="$(status_to_code "$status")"

  cat > "$tmp" <<EOF
{
  "title": "${esc_title}",
  "author": "${esc_author}",
  "artist": "${esc_artist}",
  "description": "${esc_desc}",
  "genre": ${genres_json},
  "status": "${status_code}",
  "_status values": ["0 = Unknown", "1 = Ongoing", "2 = Completed", "3 = Licensed"]
}
EOF

  [[ -s "$tmp" ]] && { mv -f -- "$tmp" "$out_path"; return 0; }
  rm -f -- "$tmp" 2>/dev/null || true
  return 1
}

ensure_details_json() {
  local override_dir="$1"
  local display_title="$2"
  shift 2
  local source_dirs=("$@")

  local details_path="$override_dir/details.json"
  [[ -e "$details_path" ]] && return 0

  # 1) Seed from an existing details.json in a source branch (if present).
  local src
  for src in "${source_dirs[@]}"; do
    if [[ -f "$src/details.json" ]]; then
      log "MergerFS: seed details.json from: $src"
      run_cmd cp -n -- "$src/details.json" "$details_path" || { log "MergerFS: cp failed for details.json -> $details_path"; return 0; }
      inherit_from_parent "$details_path" "$override_dir" || true
      return 0
    fi
  done

  # 2) Otherwise, create details.json from a VALID ComicInfo.xml.
  # Fast path: try a single ComicInfo.xml per source at the typical depth, and only
  # fall back to a broader scan if parsing fails (massive speedup on startup).
  local xml="" ok=0

  for src in "${source_dirs[@]}"; do
    [[ -d "$src" ]] || continue
    xml=""
    local _t_ci_fast=0 _dt_ci_fast=0
    if (( DEBUG_TIMING )); then _t_ci_fast="$(prof_ns)"; fi
    capture_first_line_timeout 8 xml find "$src" -mindepth 2 -maxdepth 2 -type f -name 'ComicInfo.xml' -print -quit
    if (( DEBUG_TIMING )); then
      _dt_ci_fast=$(( $(prof_ns) - _t_ci_fast ))
      if (( _dt_ci_fast / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "DEBUG Timing: details fast-path find took $(fmt_ms "$_dt_ci_fast")ms title=$(printf %q "$display_title") src=$(printf %q "$src")"
      fi
    fi
    [[ -n "$xml" ]] || continue
    log "MergerFS: create details.json from ComicInfo.xml: $xml"
    if (( DRY_RUN )); then return 0; fi
    if write_details_json_from_comicinfo "$xml" "$display_title" "$details_path"; then
      ok=1
      break
    fi
    log "MergerFS: failed to parse ComicInfo.xml; trying another chapter: $xml"
  done

  if (( ok )); then
    inherit_from_parent "$details_path" "$override_dir" || true
    return 0
  fi

  # Slow path: gather multiple candidates (covers malformed XML / unexpected nesting).
  local xml_candidates=() f found_in_src
  for src in "${source_dirs[@]}"; do
    [[ -d "$src" ]] || continue
    found_in_src=0

    local _count=0
    local _tmp_ci="$(tmpfile comicinfo)"
    : > "$_tmp_ci" 2>/dev/null || true

    # Typical structure: <manga>/<chapter>/ComicInfo.xml
    local _rc_ci1=0 _t_ci1=0 _dt_ci1=0
    if (( DEBUG_TIMING )); then _t_ci1="$(prof_ns)"; fi
    ro_timeout 20 find "$src" -mindepth 2 -maxdepth 2 -type f -name 'ComicInfo.xml' -print >"$_tmp_ci" 2>/dev/null || _rc_ci1=$?
    if (( DEBUG_TIMING )); then
      _dt_ci1=$(( $(prof_ns) - _t_ci1 ))
      if (( _dt_ci1 / 1000000 >= DEBUG_TIMING_SLOW_MS || _rc_ci1 == 124 )); then
        log "DEBUG Timing: details slow-path find(depth2) took $(fmt_ms "$_dt_ci1")ms rc=$_rc_ci1 title=$(printf %q "$display_title") src=$(printf %q "$src")"
      fi
    fi
    while IFS= read -r f; do
      [[ -n "$f" ]] || continue
      # Skip the one we already tried (if any)
      [[ -n "$xml" && "$f" == "$xml" ]] && continue
      xml_candidates+=("$f")
      found_in_src=1
      ((_count+=1))
      (( _count >= 30 )) && break
    done <"$_tmp_ci"
    rm -f -- "$_tmp_ci" 2>/dev/null || true

    # Fallback: tolerate extra nesting (volume/season buckets, etc.).
    if (( ! found_in_src )); then
      local _count2=0
      local _tmp_ci2="$(tmpfile comicinfo)"
      : > "$_tmp_ci2" 2>/dev/null || true
      local _rc_ci2=0 _t_ci2=0 _dt_ci2=0
      if (( DEBUG_TIMING )); then _t_ci2="$(prof_ns)"; fi
      ro_timeout 20 find "$src" -mindepth 2 -maxdepth 6 -type f -name 'ComicInfo.xml' -print >"$_tmp_ci2" 2>/dev/null || _rc_ci2=$?
      if (( DEBUG_TIMING )); then
        _dt_ci2=$(( $(prof_ns) - _t_ci2 ))
        if (( _dt_ci2 / 1000000 >= DEBUG_TIMING_SLOW_MS || _rc_ci2 == 124 )); then
          log "DEBUG Timing: details slow-path find(depth6) took $(fmt_ms "$_dt_ci2")ms rc=$_rc_ci2 title=$(printf %q "$display_title") src=$(printf %q "$src")"
        fi
      fi
      while IFS= read -r f; do
        [[ -n "$f" ]] || continue
        xml_candidates+=("$f")
        found_in_src=1
        ((_count2+=1))
        (( _count2 >= 30 )) && break
      done <"$_tmp_ci2"
      rm -f -- "$_tmp_ci2" 2>/dev/null || true
    fi
  done

  if (( ${#xml_candidates[@]} == 0 )); then
    log "MergerFS: no ComicInfo.xml yet; skipping details.json (will create on first chapter): $details_path"
    if (( DEBUG_COMICINFO )); then
      local _shown=0 _probe=""
      for src in "${source_dirs[@]}"; do
        [[ -n "$src" ]] || continue
        capture_first_line_timeout 5 _probe find "$src" -mindepth 2 -maxdepth 2 -type f -name 'ComicInfo.xml' -print -quit
        log "DEBUG ComicInfo: src=$(printf '%q' "$src") exists=$([[ -d "$src" ]] && echo yes || echo no) probe=$(printf '%q' "$_probe")"
        ((_shown+=1))
        (( _shown >= 5 )) && break
      done
    fi
    return 0
  fi

  local fxml
  for fxml in "${xml_candidates[@]}"; do
    log "MergerFS: create details.json from ComicInfo.xml: $fxml"
    if (( DRY_RUN )); then return 0; fi
    if write_details_json_from_comicinfo "$fxml" "$display_title" "$details_path"; then
      ok=1
      break
    fi
    log "MergerFS: failed to parse ComicInfo.xml; trying another chapter: $fxml"
  done

  if (( ok )); then
    inherit_from_parent "$details_path" "$override_dir" || true
    return 0
  fi

  log "MergerFS: found ComicInfo.xml but none parsed successfully; skipping details.json: $details_path"
  return 0
}



branchdir_id_for_groupkey() {
  local k="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    printf '%s' "$k" | sha256sum | awk '{print substr($1,1,16)}'
    return 0
  fi
  if command -v sha1sum >/dev/null 2>&1; then
    printf '%s' "$k" | sha1sum | awk '{print substr($1,1,16)}'
    return 0
  fi
  if command -v md5sum >/dev/null 2>&1; then
    printf '%s' "$k" | md5sum | awk '{print substr($1,1,16)}'
    return 0
  fi
  printf '%s' "$k" | cksum | awk '{print $1}'
}

short_hash() {
  # Produce a stable, short, filesystem-safe hash for strings (used in fsname).
  # Output: 12 chars (hex/digits) when possible.
  local s="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    printf '%s' "$s" | sha256sum | awk '{print substr($1,1,12)}'
    return 0
  fi
  if command -v sha1sum >/dev/null 2>&1; then
    printf '%s' "$s" | sha1sum | awk '{print substr($1,1,12)}'
    return 0
  fi
  if command -v md5sum >/dev/null 2>&1; then
    printf '%s' "$s" | md5sum | awk '{print substr($1,1,12)}'
    return 0
  fi
  # Fallback: cksum (decimal). Still safe, just not hex.
  printf '%s' "$s" | cksum | awk '{print $1}'
}


collect_in_use_branchdirs() {
  # Only return branchlink directories that are referenced by active mergerfs mounts.
  # We intentionally filter to BRANCHLINK_ROOT so other mergerfs usage on the system
  # doesn't influence our cleanup logic.
  local lines=() line src fstype
  local _tmp_findmnt="$(tmpfile findmnt)"
  ro_timeout_findmnt_pairs 5 -o SOURCE,FSTYPE >"$_tmp_findmnt" 2>/dev/null || true
  mapfile -t lines <"$_tmp_findmnt" 2>/dev/null || true
  rm -f -- "$_tmp_findmnt" 2>/dev/null || true

  local oldIFS part path dir
  local parts=()
  for line in "${lines[@]}"; do
    src="$(findmnt_p_get SOURCE "$line")"
    fstype="$(findmnt_p_get FSTYPE "$line")"

    [[ "${fstype,,}" == *mergerfs* ]] || continue

    src="$(unescape_findmnt_string "$src")"

    # New-style SOURCE when we set fsname explicitly:
    #   suwayomi_<branchdir_id>_<hash>
    # This is safe (no commas) and lets us map mounts back to branchlink dirs without
    # relying on mergerfs#<branches> being present.
    local _s="$src"
    [[ "$_s" == *"#"* ]] && _s="${_s#*#}"
    if [[ "$_s" =~ ^suwayomi_([^_]+)_([^_]+)$ ]]; then
      local _id="${BASH_REMATCH[1]}"
      local _dir=""
      _dir="$BRANCHLINK_ROOT_REAL/$_id"
      [[ -d "$_dir" ]] && printf '%s\n' "$_dir"
      _dir="$BRANCHLINK_ROOT/$_id"
      [[ -d "$_dir" ]] && printf '%s\n' "$_dir"
      continue
    fi

    # Typical mergerfs SOURCE looks like: mergerfs#<branch1=RW:branch2=RO:...>
    if [[ "$src" == *"#"* ]]; then
      src="${src#*#}"
    else
      src="${src#mergerfs#}"
    fi

    parts=()
    oldIFS="$IFS"
    IFS=':'
    read -r -a parts <<<"$src"
    IFS="$oldIFS"

    for part in "${parts[@]}"; do
      path="${part%%=*}"
      dir="${path%/*}"
      [[ -n "$dir" && "$dir" != "$path" ]] || continue
      # Branchlink dirs may live under user path or real cache-pool path.
      local _ok=0
      [[ "$dir" == "$BRANCHLINK_ROOT_REAL/"* ]] && _ok=1
      [[ "$dir" == "$BRANCHLINK_ROOT/"* ]] && _ok=1
      (( _ok )) || continue
      printf '%s\n' "$dir"
    done
  done
}


prepare_branchlinks() {
  # Args:
  #   $1 = canonical title (for debug)
  #   $2 = branchdir
  #   $@ (from $3) = targets: linkname<TAB>realpath
  local canon_title="${1:-}"
  local branchdir="$2"
  shift 2
  local targets=("$@")

  safe_mkdir_p "$branchdir" || { log "MergerFS: cannot create branchlink dir: $branchdir"; return 1; }

  local item linkname realpath dst cur
  local changed=0 skipped=0 total=0
  local _t_link=0 _dt_link=0

  # Batch chown for changed symlinks (massive speedup). Doing a timeboxed chown per-link
  # can dominate scan time due to timeout wrapper overhead. Ownership of these symlinks
  # does not affect mergerfs semantics, but we keep the prior behavior by applying it
  # in one go when inheritance is enabled.
  local -a chown_links=()
  local do_inherit=0
  if (( INHERIT_FROM_PARENT )) && [[ "${EUID:-$(id -u)}" -eq 0 ]]; then
    do_inherit=1
  fi

  for item in "${targets[@]}"; do
    ((total+=1))
    linkname="${item%%$'\t'*}"
    realpath="${item#*$'\t'}"
    dst="$branchdir/$linkname"

    # Fast path: if the symlink already points at the desired target, do nothing.
    if [[ -L "$dst" ]]; then
      cur="$(readlink -- "$dst" 2>/dev/null || true)"
      if [[ "$cur" == "$realpath" ]]; then
        ((skipped+=1))
        continue
      fi
    fi

    if (( DEBUG_TIMING )) && (( DEBUG_TIMING_LIVE )); then _t_link="$(prof_ns)"; fi

    run_cmd ln -sfn -- "$realpath" "$dst"
    ((changed+=1))
    (( do_inherit )) && chown_links+=("$dst")

    if (( DEBUG_TIMING )) && (( DEBUG_TIMING_LIVE )); then
      _dt_link=$(( $(prof_ns) - _t_link ))
      if (( _dt_link / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "Timing: slow branchlink_item $(fmt_ms \"$_dt_link\")ms title=$(printf %q \"$canon_title\") link=$(printf %q \"$linkname\") dst=$(printf %q \"$dst\")"
      fi
    fi
  done

  # Apply ownership inheritance in a single chown -h call (timeboxed).
  if (( do_inherit )) && (( ${#chown_links[@]} )); then
    cache_reference_perms || true
    if (( PERMS_CACHE_OK )); then
      run_cmd_timeout 5 chown -h "${PERMS_CACHE_UID}:${PERMS_CACHE_GID}" -- "${chown_links[@]}" 2>/dev/null || true
    fi
  fi

  BRANCHLINKS_LAST_TOTAL=$total
  BRANCHLINKS_LAST_CHANGED=$changed
  BRANCHLINKS_LAST_SKIPPED=$skipped
  if (( DEBUG_TIMING )) && (( DEBUG_TIMING_LIVE )) && (( total >= 10 )); then
    log "MergerFS: branchlinks stats title=$(printf %q \"$canon_title\") total=$total changed=$changed skipped=$skipped"
  fi
}



mergerfs_mount_pass_locked() {
  local _prof_pass_start=0 _prof_reason=""
  local _prof_src_count=0 _prof_manga_count=0 _prof_title_count=0
  local _prof_details_calls=0 _prof_need_actions=0
  local _prof_mount_ops=0 _prof_unmount_ops=0
  local _prof_mount_fail=0 _prof_unmount_fail=0

  if (( DEBUG_TIMING )); then
    prof_reset
    _prof_pass_start="$(prof_ns)"
    _prof_reason="${MERGE_SCAN_REASON:-}"
  fi

  # Refresh /mnt/disk* list once per scan (used for /mnt/user -> real path mapping)
  init_disk_mounts

  EQUIV_CANON_BY_KEY=()
  EQUIV_CANON_BY_NORM=()
  SRC_PRIORITY=()
  if (( DEBUG_TIMING )); then
    local _t_map
    _t_map="$(prof_ns)"
    load_equivs "$EQUIV_FILE"
    load_priority "$PRIORITY_FILE"
    prof_add "load_mappings" "$_t_map"
  else
    load_equivs "$EQUIV_FILE"
    load_priority "$PRIORITY_FILE"
  fi

  declare -A CANON_BY_GROUPKEY=()
  declare -A BRANCHES_BY_GROUPKEY=()
  declare -A BRANCH_SEEN=()
  declare -A DESIRED_MOUNTPOINTS=()
  declare -A DESIRED_BRANCHDIRS=()

  log "MergerFS: pass begin"
  log "MergerFS: roots user: local=$LOCAL_ROOT override=$OVERRIDE_ROOT branchlinks=$BRANCHLINK_ROOT"
  log "MergerFS: roots real: local=$LOCAL_ROOT_REAL override=$OVERRIDE_ROOT_REAL branchlinks=$BRANCHLINK_ROOT_REAL"

  # These are the only paths we *must* be able to touch to proceed.
  safe_mkdir_p "$MERGE_STATE_DIR" || true

  safe_mkdir_p "$LOCAL_ROOT_REAL" || return 0
  safe_mkdir_p "$OVERRIDE_ROOT_REAL" || return 0
  safe_mkdir_p "$BRANCHLINK_ROOT_REAL" || return 0

  # If the cache-pool path was auto-created by root (common when the directory only
  # existed on array disks previously), it can "mask" the array copy under /mnt/user
  # and appear as root-owned. Fix that *only* when root-owned by re-inheriting from
  # the parent directory.
  fix_root_owned_dir_if_needed "$LOCAL_ROOT_REAL" "$(dirname "$LOCAL_ROOT_REAL")" || true
  fix_root_owned_dir_if_needed "$OVERRIDE_ROOT_REAL" "$(dirname "$OVERRIDE_ROOT_REAL")" || true
  fix_root_owned_dir_if_needed "$BRANCHLINK_ROOT_REAL" "$(dirname "$BRANCHLINK_ROOT_REAL")" || true

  log "MergerFS: base roots ready"

  # Index existing override titles so we reuse the exact capitalization/punctuation
  # Suwayomi already knows when no explicit equivalence mapping exists.
  if (( DEBUG_TIMING )); then
    local _t_ov
    _t_ov="$(prof_ns)"
    build_override_canon_index || true
    prof_add "build_override_index" "$_t_ov"
  else
    build_override_canon_index || true
  fi

  # Snapshot current mounts once (findmnt per-title is slow on large libraries)
  declare -A FSTYPE_BY_TARGET=()
  declare -A SOURCE_BY_TARGET=()
  declare -A OPTS_BY_TARGET=()
  if (( DEBUG_TIMING )); then
    local _t_fm
    _t_fm="$(prof_ns)"
    build_findmnt_snapshot FSTYPE_BY_TARGET SOURCE_BY_TARGET OPTS_BY_TARGET
    prof_add "build_findmnt_snapshot" "$_t_fm"
  else
    build_findmnt_snapshot FSTYPE_BY_TARGET SOURCE_BY_TARGET OPTS_BY_TARGET
  fi


log "MergerFS: scan $DOWNLOAD_ROOT"
local src_dir manga_dir raw_title cleaned cleaned_norm k canon groupkey k_key

# Scan real storage roots first (cache pool + array disks) to avoid /mnt/user shfs?mergerfs deadlocks.
# IMPORTANT: Build this list dynamically each pass so newly-created /mnt/diskX paths (e.g. from mover)
# are picked up on the next scan.
local scan_roots=()
if [[ "$DOWNLOAD_ROOT" == /mnt/user/* ]]; then
  local _dl_rel="${DOWNLOAD_ROOT#/mnt/user}"
  if [[ -n "${UNRAID_CACHE_POOL:-}" && -d "/mnt/${UNRAID_CACHE_POOL}" ]]; then
    local _dl_cache="/mnt/${UNRAID_CACHE_POOL}${_dl_rel}"
    [[ -d "$_dl_cache" ]] && scan_roots+=("$_dl_cache")
  fi
  local _dl_d _dl_cand
  for _dl_d in /mnt/disk*; do
    [[ -d "$_dl_d" ]] || continue
    _dl_cand="${_dl_d}${_dl_rel}"
    [[ -d "$_dl_cand" ]] && scan_roots+=("$_dl_cand")
  done
  unset _dl_rel _dl_cache _dl_d _dl_cand
else
  [[ -d "$DOWNLOAD_ROOT" ]] && scan_roots+=("$DOWNLOAD_ROOT")
fi
(( ${#scan_roots[@]} == 0 )) && scan_roots+=("$DOWNLOAD_ROOT")

# Avoid globs on /mnt/user (can hang on Unraid during array transitions). Use timeboxed find.
local scan_root
for scan_root in "${scan_roots[@]}"; do
  local _tmp_src="$(tmpfile srcdirs)"
  local _rc_find_src=0
  local _t_find_src=0
  if (( DEBUG_TIMING )); then _t_find_src="$(prof_ns)"; fi
  ro_timeout 25 find "$scan_root" -mindepth 1 -maxdepth 1 -type d -print0 >"$_tmp_src" 2>/dev/null || _rc_find_src=$?
  if (( DEBUG_TIMING )); then
    local _t1_find_src _dt_find_src
    _t1_find_src="$(prof_ns)"
    _dt_find_src=$(( _t1_find_src - _t_find_src ))
    prof_add_ns "find_sources" "$_dt_find_src"
    top_insert FINDSOURCES "$_dt_find_src" "scan_root=$(printf %q "$scan_root") rc=$_rc_find_src"
    if (( _rc_find_src == 124 )); then
      prof_timeout "find_sources"
      log "DEBUG Timing: find_sources timed out (25s) scan_root=$(printf %q "$scan_root")"
    fi
    if (( DEBUG_TIMING_LIVE )) && (( _dt_find_src / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
      log "Timing: slow find_sources $(fmt_ms "$_dt_find_src")ms scan_root=$(printf %q "$scan_root") rc=$_rc_find_src"
    fi
  fi

  while IFS= read -r -d '' src_dir; do
    [[ -d "$src_dir" ]] || continue
    local src_name
    src_name="${src_dir##*/}"
    if is_excluded_source "$src_name"; then
      continue
    fi

    if (( DEBUG_TIMING )); then
      ((_prof_src_count+=1))
    fi

    local _tmp_manga="$(tmpfile mangadirs)"
    local _rc_find_manga=0
    local _t_find_manga=0
    if (( DEBUG_TIMING )); then _t_find_manga="$(prof_ns)"; fi
    ro_timeout 25 find "$src_dir" -mindepth 1 -maxdepth 1 -type d -print0 >"$_tmp_manga" 2>/dev/null || _rc_find_manga=$?
    if (( DEBUG_TIMING )); then
      local _t1_find_manga _dt_find_manga
      _t1_find_manga="$(prof_ns)"
      _dt_find_manga=$(( _t1_find_manga - _t_find_manga ))
      prof_add_ns "find_mangas" "$_dt_find_manga"
      top_insert FINDMANGAS "$_dt_find_manga" "src=$(printf %q "$src_name") path=$(printf %q "$src_dir") rc=$_rc_find_manga"
      if (( _rc_find_manga == 124 )); then
        prof_timeout "find_mangas"
        log "DEBUG Timing: find_mangas timed out (25s) src_dir=$(printf %q "$src_dir")"
      fi
      if (( DEBUG_TIMING_LIVE )) && (( _dt_find_manga / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "Timing: slow find_mangas $(fmt_ms "$_dt_find_manga")ms src=$(printf %q "$src_name") path=$(printf %q "$src_dir") rc=$_rc_find_manga"
      fi
    fi

    while IFS= read -r -d '' manga_dir; do
      local _t_md=0 _dt_md=0
      if (( DEBUG_TIMING )); then _t_md="$(prof_ns)"; fi
      raw_title="${manga_dir##*/}"

      if (( DEBUG_TIMING )); then
        ((_prof_manga_count+=1))
      fi


      cleaned="$(normalize_title "$raw_title")"
if [[ -z "${cleaned// }" ]]; then
  cleaned="$(trim_spaces "$raw_title")"
fi
cleaned_norm="$(normify_from_normalized "$cleaned")"

# Default group key for titles without an explicit equivalence entry.


      # Uses normify(): normalize_title + ASCII fold + lowercase + strip all non-alnum.


      k="$cleaned_norm"


      k_key="$(keyify "$cleaned")"


      if [[ -z "$k" ]]; then


        # Title is entirely non-alphanumeric; fall back to a stable hash-based id.


        k="x$(branchdir_id_for_groupkey "$raw_title")"


      fi



      # Prefer equivalence matches using the normify() key.


      if [[ -n "${cleaned_norm:-}" && -n "${EQUIV_CANON_BY_NORM[$cleaned_norm]+x}" ]]; then


        canon="${EQUIV_CANON_BY_NORM[$cleaned_norm]}"


        groupkey="$(normify "$canon")"


        [[ -z "$groupkey" ]] && groupkey="$k"


      elif [[ -n "${k_key:-}" && -n "${EQUIV_CANON_BY_KEY[$k_key]+x}" ]]; then


        canon="${EQUIV_CANON_BY_KEY[$k_key]}"


        groupkey="$(normify "$canon")"


        [[ -z "$groupkey" ]] && groupkey="$k"


      else



        # No explicit mapping in manga_equivalents.txt:

        # If a matching local-override directory already exists (case/punctuation-insensitive),

        # reuse its exact name so Suwayomi keeps tracking the same manga.

        local ov_canon=""

        if [[ -n "${cleaned_norm:-}" ]]; then

          ov_canon="$(override_canon_for_norm "$cleaned_norm")"

        fi

        if [[ -n "$ov_canon" ]]; then

          canon="$ov_canon"

          groupkey="$(normify "$canon")"

          [[ -z "$groupkey" ]] && groupkey="$k"

        else

          canon="$cleaned"

          groupkey="$k"

        fi



      fi

      CANON_BY_GROUPKEY["$groupkey"]="${CANON_BY_GROUPKEY[$groupkey]:-$canon}"


local -a manga_dir_reals=()
if [[ "$manga_dir" == /mnt/user/* ]]; then
  mapfile -t manga_dir_reals < <(list_real_dirs_for_user_dir "$manga_dir")
else
  manga_dir_reals=("$manga_dir")
fi
(( ${#manga_dir_reals[@]} )) || manga_dir_reals=("$manga_dir")

local md seen_key
for md in "${manga_dir_reals[@]}"; do
  [[ -n "${md:-}" ]] || continue
  seen_key="${groupkey}|${md}"
  [[ -n "${BRANCH_SEEN[$seen_key]+x}" ]] && continue
  BRANCH_SEEN["$seen_key"]=1
  if [[ -z "${BRANCHES_BY_GROUPKEY[$groupkey]+x}" || -z "${BRANCHES_BY_GROUPKEY[$groupkey]}" ]]; then
    BRANCHES_BY_GROUPKEY["$groupkey"]="$md"
  else
    BRANCHES_BY_GROUPKEY["$groupkey"]+=$'\n'"$md"
  fi
done

      if (( DEBUG_TIMING )); then
        _dt_md=$(( $(prof_ns) - _t_md ))
        prof_add_ns "process_manga_dir" "$_dt_md"
        top_insert MANGADIR "$_dt_md" "$src_name/$raw_title"
        if (( DEBUG_TIMING_LIVE )) && (( _dt_md / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
          log "Timing: slow manga_dir processing $(fmt_ms "$_dt_md")ms src=$(printf %q "$src_name") title=$(printf %q "$raw_title") path=$(printf %q "$manga_dir")"
        fi
      fi
    done <"$_tmp_manga"


    rm -f -- "$_tmp_manga" 2>/dev/null || true
  done <"$_tmp_src"

  rm -f -- "$_tmp_src" 2>/dev/null || true
done


  local groupkey_val canon_title mountpoint
  local _titles_total=${#CANON_BY_GROUPKEY[@]}
  local _title_idx=0
  local _last_prog_ns=0
  local _scan_start_ns=0
  if (( DEBUG_TIMING )); then
    _last_prog_ns="$(prof_ns)"
    _scan_start_ns="$_last_prog_ns"
  fi
  for groupkey_val in "${!CANON_BY_GROUPKEY[@]}"; do
    ((_title_idx+=1)) || true
    local _t_title=0 _dt_title=0
    if (( DEBUG_TIMING )); then
      ((_prof_title_count+=1))
      _t_title="$(prof_ns)"
      # Progress heartbeat (so we can see where it is when scans take hours)
      if (( (DEBUG_SCAN_PROGRESS_EVERY > 0) && (_title_idx % DEBUG_SCAN_PROGRESS_EVERY == 0) )); then
        local _now_ns _elapsed_ns _rate
        _now_ns="$(prof_ns)"
        _elapsed_ns=$(( _now_ns - _scan_start_ns ))
        _rate=$(( _title_idx > 0 ? (_elapsed_ns/1000000) / _title_idx : 0 ))
        log "MergerFS: progress titles=$_title_idx/$_titles_total elapsed=$(fmt_s "$_elapsed_ns")s avg_per_title=${_rate}ms"
        _last_prog_ns="$_now_ns"
      elif (( DEBUG_SCAN_PROGRESS_SECONDS > 0 )); then
        local _now_ns2 _since_ns
        _now_ns2="$(prof_ns)"
        _since_ns=$(( _now_ns2 - _last_prog_ns ))
        if (( _since_ns >= DEBUG_SCAN_PROGRESS_SECONDS * 1000000000 )); then
          local _elapsed_ns2 _rate2
          _elapsed_ns2=$(( _now_ns2 - _scan_start_ns ))
          _rate2=$(( _title_idx > 0 ? (_elapsed_ns2/1000000) / _title_idx : 0 ))
          log "MergerFS: progress titles=$_title_idx/$_titles_total elapsed=$(fmt_s "$_elapsed_ns2")s avg_per_title=${_rate2}ms"
          _last_prog_ns="$_now_ns2"
        fi
      fi
    fi
    canon_title="${CANON_BY_GROUPKEY[$groupkey_val]}"
    if [[ -z "${canon_title// }" ]]; then
      canon_title="Unknown-$groupkey_val"
      canon_title="$(trim_spaces "$canon_title")"
      [[ -z "$canon_title" ]] && canon_title="Unknown-$(branchdir_id_for_groupkey "$groupkey_val")"
    fi

    mountpoint="$LOCAL_ROOT_REAL/$canon_title"
    local override_primary=""
    local -a override_dirs=()
    if (( DEBUG_TIMING )); then
      local _t_ov _dt_ov
      _t_ov="$(prof_ns)"
      choose_override_dirs_for_title "$canon_title" override_primary override_dirs
      _dt_ov=$(( $(prof_ns) - _t_ov ))
      prof_add_ns "choose_override_dirs" "$_dt_ov"
      top_insert OVERRIDES "$_dt_ov" "$canon_title"
      if (( DEBUG_TIMING_LIVE )) && (( _dt_ov / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "Timing: slow choose_override_dirs $(fmt_ms "$_dt_ov")ms title=$(printf %q "$canon_title")"
      fi
    else
      choose_override_dirs_for_title "$canon_title" override_primary override_dirs
    fi

    DESIRED_MOUNTPOINTS["$mountpoint"]=1
    if (( DEBUG_TIMING )); then
      local _t_mk1 _dt_mk1
      _t_mk1="$(prof_ns)"
      safe_mkdir_p "$override_primary" || { log "MergerFS: skip title (cannot create override dir): $override_primary"; continue; }
      _dt_mk1=$(( $(prof_ns) - _t_mk1 ))
      prof_add_ns "mkdir_override" "$_dt_mk1"
      if (( DEBUG_TIMING_LIVE )) && (( _dt_mk1 / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "Timing: slow mkdir override $(fmt_ms "$_dt_mk1")ms path=$(printf %q "$override_primary")"
      fi
    else
      safe_mkdir_p "$override_primary" || { log "MergerFS: skip title (cannot create override dir): $override_primary"; continue; }
    fi
    if (( DEBUG_TIMING )); then
      local _t_mk2 _dt_mk2
      _t_mk2="$(prof_ns)"
      safe_mkdir_p "$mountpoint" || { log "MergerFS: skip title (cannot create mountpoint dir): $mountpoint"; continue; }
      _dt_mk2=$(( $(prof_ns) - _t_mk2 ))
      prof_add_ns "mkdir_mountpoint" "$_dt_mk2"
      if (( DEBUG_TIMING_LIVE )) && (( _dt_mk2 / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "Timing: slow mkdir mountpoint $(fmt_ms "$_dt_mk2")ms path=$(printf %q "$mountpoint")"
      fi
    else
      safe_mkdir_p "$mountpoint" || { log "MergerFS: skip title (cannot create mountpoint dir): $mountpoint"; continue; }
    fi

    local _t_ord=0 _dt_ord=0
    if (( DEBUG_TIMING )); then _t_ord="$(prof_ns)"; fi
    local branches=()
    mapfile -t branches <<<"${BRANCHES_BY_GROUPKEY[$groupkey_val]}"

    local ordered_sources=()
    local ordered_prios=()
    local b b_source p idx inserted
    for b in "${branches[@]}"; do
      [[ -n "${b:-}" ]] || continue
      b_source="${b%/*}"
      b_source="${b_source##*/}"
      p="$(priority_of_source "$b_source")"

      inserted=0
      for idx in "${!ordered_sources[@]}"; do
        if (( p < ordered_prios[idx] )); then
          ordered_sources=( "${ordered_sources[@]:0:idx}" "$b" "${ordered_sources[@]:idx}" )
          ordered_prios=( "${ordered_prios[@]:0:idx}" "$p" "${ordered_prios[@]:idx}" )
          inserted=1
          break
        elif (( p == ordered_prios[idx] )) && [[ "$b" < "${ordered_sources[idx]}" ]]; then
          ordered_sources=( "${ordered_sources[@]:0:idx}" "$b" "${ordered_sources[@]:idx}" )
          ordered_prios=( "${ordered_prios[@]:0:idx}" "$p" "${ordered_prios[@]:idx}" )
          inserted=1
          break
        fi
      done
      if (( ! inserted )); then
        ordered_sources+=( "$b" )
        ordered_prios+=( "$p" )
      fi
    done
    if (( DEBUG_TIMING )); then
      _dt_ord=$(( $(prof_ns) - _t_ord ))
      prof_add_ns "order_branches" "$_dt_ord"
      if (( DEBUG_TIMING_LIVE )) && (( _dt_ord / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "Timing: slow order_branches $(fmt_ms "$_dt_ord")ms title=$(printf %q "$canon_title") branches=${#branches[@]} sources=${#ordered_sources[@]}"
      fi
    fi

local has_details=0 od
for od in "${override_dirs[@]}"; do
  [[ -e "$od/details.json" ]] && { has_details=1; break; }
done
if (( ! has_details )); then
  if (( DEBUG_TIMING )); then
    local _t_det _dt_det
    _t_det="$(prof_ns)"
    ensure_details_json "$override_primary" "$canon_title" "${ordered_sources[@]}"
    _dt_det=$(( $(prof_ns) - _t_det ))
    prof_add_ns "ensure_details_json" "$_dt_det"
    ((_prof_details_calls+=1))
    top_insert DETAILS "$_dt_det" "$canon_title"
  else
    ensure_details_json "$override_primary" "$canon_title" "${ordered_sources[@]}"
  fi
fi

    local bdid branchdir
    bdid="$(branchdir_id_for_groupkey "$groupkey_val")"
    branchdir="$BRANCHLINK_ROOT_REAL/$bdid"
    DESIRED_BRANCHDIRS["$branchdir"]=1

    local targets=()
targets+=("00_override_primary"$'\t'"$override_primary")

local j=0 od label
for od in "${override_dirs[@]}"; do
  [[ "$od" == "$override_primary" ]] && continue
  # Label from top-level mount name (disk8/raid/etc), sanitized for linkname safety.
  label="${od#/mnt/}"
  label="${label%%/*}"
  label="${label//[^A-Za-z0-9_-]/_}"
  [[ -z "$label" ]] && label="extra"
  targets+=("01_override_${label}_$(printf '%03d' "$j")"$'\t'"$od")
  ((j+=1)) || true
done

    local i=0 safe_src
    for b in "${ordered_sources[@]}"; do
      b_source="${b%/*}"
      b_source="${b_source##*/}"
      safe_src="$(safe_link_name "$b_source")"
      targets+=("10_${safe_src}_$(printf '%03d' "$i")"$'\t'"$b")
      ((i+=1))
    done

    if (( DEBUG_TIMING )); then
      local _t_bl _dt_bl
      _t_bl="$(prof_ns)"
      prepare_branchlinks "$canon_title" "$branchdir" "${targets[@]}"
      _dt_bl=$(( $(prof_ns) - _t_bl ))
      prof_add_ns "prepare_branchlinks" "$_dt_bl"
      top_insert BRANCHLINKS "$_dt_bl" "$canon_title"
    else
      prepare_branchlinks "$canon_title" "$branchdir" "${targets[@]}"
    fi

local branch_specs=()

local item linkname
for item in "${targets[@]}"; do
  linkname="${item%%$'	'*}"
  if [[ "$linkname" == "00_override_primary" ]]; then
    branch_specs+=("$branchdir/$linkname=RW")
  elif [[ "$linkname" == 01_override_* ]]; then
    branch_specs+=("$branchdir/$linkname=RW")
  fi
done

for item in "${targets[@]}"; do
  linkname="${item%%$'	'*}"
  if [[ "$linkname" == 10_* ]]; then
    branch_specs+=("$branchdir/$linkname=RO")
  fi
done

    local desired_branch_string
    desired_branch_string="$(join_by ':' "${branch_specs[@]}")"

    # Safe per-title fsname used for change detection and to avoid commas in option strings.
    # Embed the branchdir id plus a short hash of the desired branch list.
    local desired_fsname
    desired_fsname="suwayomi_${bdid}_$(short_hash "$desired_branch_string")"

local need_action=0
local force_remount=0
if [[ -n "${FORCE_REMOUNT_MP[$mountpoint]+x}" ]]; then
  force_remount=1
  need_action=1
  log "MergerFS: mountpoint flagged for remount for '$canon_title' -> remount"
fi
    local mounted_any=0
    local mounted_mergerfs=0
    local _fstype="${FSTYPE_BY_TARGET[$mountpoint]-}"

    if [[ -n "$_fstype" ]]; then
      mounted_any=1
      if [[ "${_fstype,,}" == *mergerfs* ]]; then
        mounted_mergerfs=1
      fi
    fi

    if (( mounted_any )) && (( ! mounted_mergerfs )); then
      need_action=1
      log "MergerFS: non-mergerfs mount detected for '$canon_title' (fstype=$_fstype) -> remount"
    elif (( ! mounted_any )); then
      need_action=1
    else
      local current_source="${SOURCE_BY_TARGET[$mountpoint]-}"
      local current_opts="${OPTS_BY_TARGET[$mountpoint]-}"
      local current_fsname=""

      # For FUSE mounts, the fsname typically shows up in findmnt's SOURCE column (not OPTIONS).
      if [[ -n "$current_source" ]]; then
        current_fsname="$current_source"
      elif [[ -n "$current_opts" ]]; then
        current_fsname="$(mountopt_get_value "$current_opts" "fsname" 2>/dev/null || true)"
      fi

      # Validate we are mounted with the expected fsname (includes desired branch-hash).
      if [[ -z "$current_fsname" || "$current_fsname" != "$desired_fsname" ]]; then
        need_action=1
        log "MergerFS: fsname/source mismatch for '$canon_title' -> remount"
      else
        # Mounted mergerfs looks correct; ensure it is responsive.
        if ! mountpoint_is_healthy "$mountpoint"; then
          need_action=1
          log "MergerFS: mount appears unhealthy for '$canon_title' -> remount"
        fi
      fi
    fi

if (( need_action )); then
      if (( DEBUG_TIMING )); then
        ((_prof_need_actions+=1))
      fi

      if (( mounted_any || force_remount )); then
        if (( DEBUG_TIMING )); then
          local _t_un _dt_un
          _t_un="$(prof_ns)"
          if ! unmount_path "$mountpoint"; then
            _dt_un=$(( $(prof_ns) - _t_un ))
            prof_add_ns "unmount" "$_dt_un"
            ((_prof_unmount_ops+=1))
            ((_prof_unmount_fail+=1))
            top_insert UNMOUNT "$_dt_un" "$canon_title"
            log "MergerFS: unmount failed (busy?), will retry next scan: $mountpoint"
            continue
          fi
          _dt_un=$(( $(prof_ns) - _t_un ))
          prof_add_ns "unmount" "$_dt_un"
          ((_prof_unmount_ops+=1))
          top_insert UNMOUNT "$_dt_un" "$canon_title"
        else
          if ! unmount_path "$mountpoint"; then
            log "MergerFS: unmount failed (busy?), will retry next scan: $mountpoint"
            continue
          fi
        fi
      fi

      if (( DEBUG_TIMING )); then
        local _t_m _dt_m
        _t_m="$(prof_ns)"
        if ! mount_union "$mountpoint" "$desired_branch_string" "$desired_fsname"; then
          _dt_m=$(( $(prof_ns) - _t_m ))
          prof_add_ns "mount" "$_dt_m"
          ((_prof_mount_ops+=1))
          ((_prof_mount_fail+=1))
          top_insert MOUNT "$_dt_m" "$canon_title"
          log "MergerFS: mount failed; will retry next scan: $mountpoint"
          continue
        fi
        unset 'FORCE_REMOUNT_MP[$mountpoint]' 2>/dev/null || true
        _dt_m=$(( $(prof_ns) - _t_m ))
        prof_add_ns "mount" "$_dt_m"
        ((_prof_mount_ops+=1))
        top_insert MOUNT "$_dt_m" "$canon_title"
      else
        if ! mount_union "$mountpoint" "$desired_branch_string" "$desired_fsname"; then
          log "MergerFS: mount failed; will retry next scan: $mountpoint"
          continue
        fi
        unset 'FORCE_REMOUNT_MP[$mountpoint]' 2>/dev/null || true
      fi
    fi
    if (( DEBUG_TIMING )); then
      _dt_title=$(( $(prof_ns) - _t_title ))
      prof_add_ns "title_total" "$_dt_title"
      top_insert TITLE "$_dt_title" "$canon_title"
      if (( DEBUG_TIMING_LIVE )) && (( _dt_title / 1000000 >= DEBUG_TIMING_SLOW_MS )); then
        log "Timing: slow title_total $(fmt_ms "$_dt_title")ms title=$(printf %q "$canon_title")"
      fi
    fi
  done

  log "MergerFS: stale cleanup under local roots (user=$LOCAL_ROOT, real=$LOCAL_ROOT_REAL)"
  local lines=()
  local _tmp_findmnt="$(tmpfile findmnt)"
  local _rc_cleanup_fm=0
  local _t_cleanup_fm=0
  if (( DEBUG_TIMING )); then _t_cleanup_fm="$(prof_ns)"; fi
  ro_timeout_findmnt_pairs 5 -o TARGET,FSTYPE >"$_tmp_findmnt" 2>/dev/null || _rc_cleanup_fm=$?
  if (( DEBUG_TIMING )); then
    prof_add "cleanup_findmnt" "$_t_cleanup_fm"
    if (( _rc_cleanup_fm == 124 )); then
      prof_timeout "cleanup_findmnt"
      log "DEBUG Timing: cleanup findmnt timed out (5s)"
    fi
  fi
  mapfile -t lines <"$_tmp_findmnt" 2>/dev/null || true
  rm -f -- "$_tmp_findmnt" 2>/dev/null || true

  local current_mounts=()
  local line mp fstype
  for line in "${lines[@]}"; do
    mp="$(findmnt_p_get TARGET "$line")"
    fstype="$(findmnt_p_get FSTYPE "$line")"
    mp="$(unescape_findmnt_string "$mp")"

    local _under=0
    [[ "$mp" == "$LOCAL_ROOT_REAL" || "$mp" == "$LOCAL_ROOT_REAL/"* ]] && _under=1
    [[ "$mp" == "$LOCAL_ROOT" || "$mp" == "$LOCAL_ROOT/"* ]] && _under=1
    (( _under )) || continue
    [[ "${fstype,,}" == *mergerfs* ]] || continue
    current_mounts+=("$mp")
  done

  for mp in "${current_mounts[@]}"; do
    if [[ -z "${DESIRED_MOUNTPOINTS[$mp]+x}" ]]; then
      log "MergerFS: stale mount -> unmount $mp"
      unmount_path "$mp" || true
    fi
  done

  declare -A IN_USE_BRANCHDIR=()
  local d=""
  while IFS= read -r d; do
    [[ -n "$d" ]] && IN_USE_BRANCHDIR["$d"]=1
  done < <(collect_in_use_branchdirs | sort -u)

  local root=""
  # Clean branchlink dirs (prefer real root; avoid globs on /mnt/user which can hang).
  local roots=()
roots+=("$BRANCHLINK_ROOT_REAL")
if [[ "$BRANCHLINK_ROOT" != "$BRANCHLINK_ROOT_REAL" ]]; then
  roots+=("$BRANCHLINK_ROOT")
fi
  local _tmp_bl=""
for root in "${roots[@]}"; do
  [[ -d "$root" ]] || continue
  _tmp_bl="$(tmpfile branchlinks_scan)"
  local _rc_cleanup_bl=0
  local _t_cleanup_bl=0
  if (( DEBUG_TIMING )); then _t_cleanup_bl="$(prof_ns)"; fi
  ro_timeout 10 find "$root" -mindepth 1 -maxdepth 1 -type d -print0 >"$_tmp_bl" 2>/dev/null || _rc_cleanup_bl=$?
  if (( DEBUG_TIMING )); then
    prof_add "cleanup_branchlinks" "$_t_cleanup_bl"
    if (( _rc_cleanup_bl == 124 )); then
      prof_timeout "cleanup_branchlinks"
      log "DEBUG Timing: cleanup branchlinks find timed out (10s) root=$(printf %q "$root")"
    fi
  fi

  while IFS= read -r -d '' d; do
    [[ -d "$d" ]] || continue
    if [[ -z "${DESIRED_BRANCHDIRS[$d]+x}" ]]; then
      if [[ -n "${IN_USE_BRANCHDIR[$d]+x}" ]]; then
        log "MergerFS: stale branchlink dir still in use; skip removal: $d"
        continue
      fi
      log "MergerFS: remove stale branchlink dir: $d"
      local _rm_rc=0
      run_cmd_timeout 10 rm -rf -- "$d" >&3 2>&3 || _rm_rc=$?
      if (( _rm_rc != 0 )); then
        log "MergerFS: WARNING failed to remove stale branchlink dir rc=$_rm_rc: $d"
      fi
    fi
  done <"$_tmp_bl"

  rm -f -- "$_tmp_bl" 2>/dev/null || true
done


  if (( DEBUG_TIMING )); then
    prof_report_merge_pass "$_prof_pass_start" "$_prof_reason" \
      "$_prof_src_count" "$_prof_manga_count" "$_prof_title_count" \
      "$_prof_details_calls" "$_prof_need_actions" \
      "$_prof_mount_ops" "$_prof_unmount_ops" \
      "$_prof_mount_fail" "$_prof_unmount_fail"
  fi

  log "MergerFS: pass done"
}

# Set by mergerfs_daemon before calling with_lock_try
MERGE_SCAN_REASON=""
mergerfs_scan_pass_locked() {
  local reason="${MERGE_SCAN_REASON:-}"
  [[ -n "$reason" ]] && log "MergerFS: ${reason} -> scan"

  mergerfs_mount_pass_locked
}


mergerfs_daemon() {
  # Child must not inherit supervisor lock FD.
  release_supervisor_lock || true

  log "MergerFS: starting (interval=${MERGE_INTERVAL_SECONDS}s trigger-poll=${MERGE_TRIGGER_POLL_SECONDS}s)"

  safe_mkdir_p "$MERGE_STATE_DIR" || true


  # Ensure LOCAL_ROOT exists before scanning.
  safe_mkdir_p "$LOCAL_ROOT_REAL" || true

  local last_trigger_id=""
  local startup_line=""
  startup_line="$(read_merge_trigger_line "$MERGE_TRIGGER_FILE")"
  if [[ -n "$startup_line" ]]; then
    last_trigger_id="${startup_line%%$'	'*}"
  fi

  local last_scan=0
  local next_retry_at=0
  local lockheld_logged=0

  log "MergerFS: initial scan attempt"
  MERGE_SCAN_REASON="initial scan"
  if with_lock_try "$MERGE_LOCK_FILE" mergerfs_scan_pass_locked; then
    last_scan="$(date +%s)"
    next_retry_at=0
    lockheld_logged=0
  else
    local rc=$?
    if (( rc == 111 )); then
      log "MergerFS: scan already running (lock held); initial scan deferred"
      next_retry_at="$(( $(date +%s) + MERGE_LOCK_RETRY_SECONDS ))"
      lockheld_logged=1
    else
      log "MergerFS: scan failed rc=$rc; retrying in ${MERGE_LOCK_RETRY_SECONDS}s"
      next_retry_at="$(( $(date +%s) + MERGE_LOCK_RETRY_SECONDS ))"
      lockheld_logged=0
    fi
  fi
  MERGE_SCAN_REASON=""

  # Lower priority only after the first scan attempt so startup doesn\'t crawl.
  best_effort_low_prio_self

  while true; do
    local now
    now="$(date +%s)"

    local line="" cur_id="" cur_reason=""
    line="$(read_merge_trigger_line "$MERGE_TRIGGER_FILE")"
    if [[ -n "$line" ]]; then
      cur_id="${line%%$'	'*}"
      cur_reason="${line#*$'	'}"
      [[ "$cur_reason" == "$line" ]] && cur_reason=""
    fi

    local need_scan=0
    local scan_trigger_id="" scan_trigger_reason=""
    local scan_reason=""

    if [[ -n "$cur_id" && "$cur_id" != "$last_trigger_id" ]]; then
      if (( now - last_scan >= MERGE_MIN_SECONDS_BETWEEN_SCANS )); then
        need_scan=1
        scan_trigger_id="$cur_id"
        scan_trigger_reason="$cur_reason"
      fi
    fi

    if (( ! need_scan )) && (( now - last_scan >= MERGE_INTERVAL_SECONDS )); then
      need_scan=1
    fi

    if (( need_scan )); then
      # If a scan is requested while another scan is already running, back off so we
      # don't spam the log every poll interval.
      if (( next_retry_at == 0 )); then
        next_retry_at="$now"
      fi

      if (( now >= next_retry_at )); then
        if [[ -n "$scan_trigger_id" ]]; then
          if [[ -n "$scan_trigger_reason" ]]; then
            scan_reason="trigger detected (${scan_trigger_reason})"
          else
            scan_reason="trigger detected"
          fi
        else
          scan_reason="interval elapsed"
        fi

        MERGE_SCAN_REASON="$scan_reason"
        if with_lock_try "$MERGE_LOCK_FILE" mergerfs_scan_pass_locked; then
          last_scan="$(date +%s)"
          next_retry_at=0
          lockheld_logged=0
          if [[ -n "$scan_trigger_id" ]]; then
            last_trigger_id="$scan_trigger_id"
          fi
        else
          local rc=$?
          if (( rc == 111 )); then
            if (( ! lockheld_logged )); then
              log "MergerFS: scan already running (lock held); retrying in ${MERGE_LOCK_RETRY_SECONDS}s"
              lockheld_logged=1
            fi
          else
            log "MergerFS: scan failed rc=$rc; retrying in ${MERGE_LOCK_RETRY_SECONDS}s"
            lockheld_logged=0
          fi
          next_retry_at=$((now + MERGE_LOCK_RETRY_SECONDS))
        fi
        MERGE_SCAN_REASON=""
      fi
    fi

    sleep "$MERGE_TRIGGER_POLL_SECONDS"
  done
}



unmount_all_mergerfs_under_local_root() {
  try_pick_fusermount || true

  if (( DRY_RUN )); then
    log "DRY-RUN: would unmount mergerfs mounts under $LOCAL_ROOT (skipped)"
    return 0
  fi

  # Time-sensitive cleanup: boost our IO/CPU priority (and the mergerfs mount PIDs) so unmounts finish promptly.
  best_effort_high_prio_self

  # Gather mounts whose FSTYPE contains "mergerfs" (covers fuse.mergerfs, fuse3.mergerfs, etc.)
  local lines=()
  local _tmp_findmnt="$(tmpfile findmnt)"
  ro_timeout_findmnt_pairs 5 -o TARGET,FSTYPE,PID >"$_tmp_findmnt" 2>/dev/null || true
  mapfile -t lines <"$_tmp_findmnt" 2>/dev/null || true
  rm -f -- "$_tmp_findmnt" 2>/dev/null || true

  local mounts=()
  declare -A pid_by_mp=()
  local line tgt fstype pid
  for line in "${lines[@]}"; do
    # TARGET may contain findmnt escapes (e.g.,   for space)
    tgt="$(findmnt_p_get TARGET "$line")"
    fstype="$(findmnt_p_get FSTYPE "$line")"
    pid="$(findmnt_p_get PID "$line")"
    tgt="$(unescape_findmnt_string "$tgt")"

    # Record the mount process PID (helps us boost priority for a clean shutdown).
    [[ -n "${pid:-}" ]] && pid_by_mp["$tgt"]="$pid"

    local _under=0
    [[ "$tgt" == "$LOCAL_ROOT_REAL" || "$tgt" == "$LOCAL_ROOT_REAL/"* ]] && _under=1
    [[ "$tgt" == "$LOCAL_ROOT" || "$tgt" == "$LOCAL_ROOT/"* ]] && _under=1
    (( _under )) || continue
    [[ "${fstype,,}" == *mergerfs* ]] || continue
    mounts+=("$tgt")
  done

  (( ${#mounts[@]} )) || return 0

  # Unmount deepest paths first
  local oldIFS="$IFS"
  IFS=$'\n'
  local mounts_sorted=()
  mounts_sorted=($(printf '%s\n' "${mounts[@]}" | awk '{ print length($0) "\t" $0 }' | sort -rn | cut -f2-))
  IFS="$oldIFS"

  local mp
  for mp in "${mounts_sorted[@]}"; do
    best_effort_high_prio_pid "${pid_by_mp[$mp]:-}"
    # Re-check before unmounting (mounts can change during shutdown)
    [[ -d "$mp" ]] || { log "MergerFS: shutdown unmount skip (missing dir): $mp"; continue; }
    is_mounted_mergerfs "$mp" || { log "MergerFS: shutdown unmount skip (not mounted): $mp"; continue; }
    log "MergerFS: shutdown unmount $mp"
    if [[ -n "${FUSERMOUNT_BIN:-}" ]]; then

      # Takes too long to do all that checking. Just brute force unmount everything lazily. It'll work it's self out.
      run_cmd_timeout "$UNMOUNT_CMD_TIMEOUT_SECONDS" "$FUSERMOUNT_BIN" -uz "$mp" >&3 2>&3 || true
      command -v umount >/dev/null 2>&1 && run_cmd_timeout "$UNMOUNT_CMD_TIMEOUT_SECONDS" umount -l "$mp" >&3 2>&3 || true

#      if ! run_cmd_timeout "$UNMOUNT_CMD_TIMEOUT_SECONDS" "$FUSERMOUNT_BIN" -u "$mp" >&3 2>&3; then
#        # Busy mounts happen; try lazy unmount (-z). This detaches the mount when possible.
#        run_cmd_timeout "$UNMOUNT_CMD_TIMEOUT_SECONDS" "$FUSERMOUNT_BIN" -uz "$mp" >&3 2>&3 || true
#
#        # Give the kernel a moment to reflect the detach.
#        local _i=0
#        while (( _i < UNMOUNT_DETACH_WAIT_SECONDS )); do
#          is_mounted_mergerfs "$mp" || break
#          ((_i+=1))
#          sleep 1
#        done
#
#        # Only call umount if it's still mounted (otherwise umount prints "no mount point specified").
#        if is_mounted_mergerfs "$mp"; then
#          command -v umount >/dev/null 2>&1 && run_cmd_timeout "$UNMOUNT_CMD_TIMEOUT_SECONDS" umount -l "$mp" >&3 2>&3 || true
#        fi
#      fi
    else
      command -v umount >/dev/null 2>&1 && run_cmd_timeout "$UNMOUNT_CMD_TIMEOUT_SECONDS" umount -l "$mp" >&3 2>&3 || true
    fi

    if is_mounted_mergerfs "$mp"; then
      log "MergerFS: shutdown unmount still mounted (busy): $mp"
    fi
  done
}


################################################################################
# Supervisor / CLI
################################################################################

RUNNING_MERGE_PID=""
RUNNING_RENAME_PID=""
_CLEANUP_RUNNING=0

cleanup() {
  (( _CLEANUP_RUNNING )) && return 0
  _CLEANUP_RUNNING=1

  local orig_merge_pid="$RUNNING_MERGE_PID"
  local orig_rename_pid="$RUNNING_RENAME_PID"

  log "Supervisor: stopping..."

  # After the first stop line, switch logging to a safe RAM-backed file so
  # any later writes can't hang if the array (/mnt/user) becomes unavailable.
  open_safe_log_fd || true

  [[ -n "$orig_merge_pid" ]] && kill_tree "$orig_merge_pid" TERM || true
  [[ -n "$orig_rename_pid" ]] && kill_tree "$orig_rename_pid" TERM || true

  # Give children a moment to exit cleanly.
  local merge_alive="$orig_merge_pid"
  local rename_alive="$orig_rename_pid"
  local _i=0
  while (( _i < CHILD_EXIT_GRACE_SECONDS )); do
    [[ -n "$merge_alive" ]] && kill -0 "$merge_alive" 2>/dev/null || merge_alive=""
    [[ -n "$rename_alive" ]] && kill -0 "$rename_alive" 2>/dev/null || rename_alive=""
    [[ -z "$merge_alive" && -z "$rename_alive" ]] && break
    ((_i+=1))
    sleep 1
  done

  [[ -n "$merge_alive" ]] && kill_tree "$merge_alive" KILL || true
  [[ -n "$rename_alive" ]] && kill_tree "$rename_alive" KILL || true

  # Don't block in cleanup: only reap quickly if a child is already a zombie.
  # If a child is stuck in uninterruptible I/O (D-state), `wait` would hang and prevent --stop from completing.
  local _p="" _st=""
  for _p in "$orig_merge_pid" "$orig_rename_pid"; do
    [[ -n "${_p:-}" && "$_p" =~ ^[0-9]+$ ]] || continue
    if [[ -r "/proc/$_p/stat" ]]; then
      _st="$(awk '{print $3}' "/proc/$_p/stat" 2>/dev/null || true)"
      [[ "$_st" == "Z" ]] && wait "$_p" 2>/dev/null || true
    fi
  done

  RUNNING_MERGE_PID=""
  RUNNING_RENAME_PID=""

  # During shutdown, boost priority so busy mergerfs mounts can flush/unmount promptly.
  best_effort_high_prio_self

  if (( DRY_RUN )); then
    log "Supervisor: DRY-RUN mode; skipping unmount"
  elif (( UNMOUNT_ON_EXIT )); then
    log "Supervisor: unmounting mergerfs mounts under local roots (user=$LOCAL_ROOT, real=$LOCAL_ROOT_REAL)"
    unmount_all_mergerfs_under_local_root || true
  else
    log "Supervisor: leaving mounts as-is (--no-unmount)"
  fi

  rm -f -- "$PID_FILE" "$MERGE_PID_FILE" "$RENAME_PID_FILE" 2>/dev/null || true
  release_supervisor_lock || true
  log "Supervisor: stopped"
}

status_cmd() {
  if [[ -f "$PID_FILE" ]] && kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
    echo "RUNNING pid $(cat "$PID_FILE")"
    exit 0
  fi
  echo "NOT RUNNING"
  exit 1
}

kill_leftover_processes() {
  # Kill any remaining processes that still look like this daemon (excluding this --stop instance).
  # This helps when some pipeline/coprocess ends up detached.
  if ! command -v pgrep >/dev/null 2>&1; then
    return 0
  fi

  local pids=()
  mapfile -t pids < <(pgrep -f 'suwayomi_manga_daemon' 2>/dev/null || true)
  local filtered=()
  local p
  for p in "${pids[@]}"; do
    [[ "$p" =~ ^[0-9]+$ ]] || continue
    [[ "$p" -eq $$ ]] && continue
    filtered+=("$p")
  done

  (( ${#filtered[@]} )) || return 0
  log "CLI --stop: killing leftover daemon-related PIDs: ${filtered[*]}"
  for p in "${filtered[@]}"; do
    kill_tree "$p" TERM || true
  done
  sleep 1
  for p in "${filtered[@]}"; do
    kill -0 "$p" 2>/dev/null && kill_tree "$p" KILL || true
  done
  return 0
}

kill_mergerfs_mount_pids_under_local_root() {
  # If any mergerfs mount processes are still alive under LOCAL_ROOT, kill them.
  local lines=() line tgt fstype pid
  local _tmp_findmnt="$(tmpfile findmnt)"
  ro_timeout_findmnt_pairs 5 -o TARGET,FSTYPE,PID >"$_tmp_findmnt" 2>/dev/null || true
  mapfile -t lines <"$_tmp_findmnt" 2>/dev/null || true
  rm -f -- "$_tmp_findmnt" 2>/dev/null || true

  local pids=()
  for line in "${lines[@]}"; do
    tgt="$(findmnt_p_get TARGET "$line")"
    fstype="$(findmnt_p_get FSTYPE "$line")"
    pid="$(findmnt_p_get PID "$line")"
    tgt="$(unescape_findmnt_string "$tgt")"

    local _under=0
    [[ "$tgt" == "$LOCAL_ROOT_REAL" || "$tgt" == "$LOCAL_ROOT_REAL/"* ]] && _under=1
    [[ "$tgt" == "$LOCAL_ROOT" || "$tgt" == "$LOCAL_ROOT/"* ]] && _under=1
    (( _under )) || continue
    [[ "${fstype,,}" == *mergerfs* ]] || continue
    [[ -n "${pid:-}" && "$pid" =~ ^[0-9]+$ ]] || continue
    pids+=("$pid")
  done

  (( ${#pids[@]} )) || return 0

  # uniq
  local oldIFS="$IFS"
  IFS=$'\n'
  local uniq_pids=()
  uniq_pids=($(printf '%s\n' "${pids[@]}" | sort -u))
  IFS="$oldIFS"

  log "CLI --stop: killing mergerfs mount PIDs under local root: ${uniq_pids[*]}"
  local p
  for p in "${uniq_pids[@]}"; do
    kill_tree "$p" TERM || true
  done
  sleep 1
  for p in "${uniq_pids[@]}"; do
    kill -0 "$p" 2>/dev/null && kill_tree "$p" KILL || true
  done
  return 0
}

stop_cmd() {
  local pid=""

  # Avoid hanging on a log FD that points at /mnt/user during array stop.
  open_safe_log_fd || true

  if [[ -f "$PID_FILE" ]]; then
    pid="$(cat "$PID_FILE" 2>/dev/null || true)"
  fi

  if [[ -n "${pid:-}" && "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null; then
    echo "Stopping pid $pid"
    log "CLI --stop: sending TERM to supervisor pid $pid"
    kill_tree "$pid" TERM || true

    local _i=0
    while (( _i < STOP_TIMEOUT_SECONDS )); do
      kill -0 "$pid" 2>/dev/null || break
      if is_zombie_pid "$pid"; then
        break
      fi
      # Heartbeat so --stop doesn't look wedged.
      if (( _i % 5 == 0 )); then
        local st=""
        [[ -r "/proc/$pid/stat" ]] && st="$(awk '{print $3}' "/proc/$pid/stat" 2>/dev/null || true)"
        echo " ... waiting ($((_i))/${STOP_TIMEOUT_SECONDS}s) pid=$pid state=${st:-?}"
      fi
      ((_i+=1))
      sleep 1
    done

    if kill -0 "$pid" 2>/dev/null && ! is_zombie_pid "$pid"; then
      echo "Still running; sending SIGKILL to pid $pid (tree)"
      log "CLI --stop: supervisor still alive after ${STOP_TIMEOUT_SECONDS}s; sending KILL"
      kill_tree "$pid" KILL || true
      sleep 1
    fi
  else
    echo "No running supervisor PID found (pidfile missing/dead); best-effort cleanup"
    log "CLI --stop: no live supervisor pid; best-effort cleanup"
  fi

  # Best-effort: if any daemon-related processes are still around, kill them too.
  kill_leftover_processes || true

  # Best-effort: break any orphaned worker state/locks.
  try_break_stale_worker_locks || true

  # Even if the supervisor couldn't run cleanup, ensure mounts are down.
  if (( DRY_RUN )); then
    echo "DRY-RUN: stop: skipping unmount"
  elif (( UNMOUNT_ON_EXIT )); then
    best_effort_high_prio_self
    log "CLI --stop: ensuring mergerfs mounts under $LOCAL_ROOT are unmounted"

    # Shorten per-command timeout during stop to avoid very long wall-clock shutdowns on huge libraries.
    local _old_umount_timeout="$UNMOUNT_CMD_TIMEOUT_SECONDS"
    UNMOUNT_CMD_TIMEOUT_SECONDS="${UNMOUNT_CMD_TIMEOUT_SECONDS_STOP:-2}"
    unmount_all_mergerfs_under_local_root || true
    UNMOUNT_CMD_TIMEOUT_SECONDS="$_old_umount_timeout"

    # If anything is still alive/mounted, last-resort: kill mergerfs mount pids.
    kill_mergerfs_mount_pids_under_local_root || true
  fi

  rm -f -- "$PID_FILE" "$MERGE_PID_FILE" "$RENAME_PID_FILE" 2>/dev/null || true
  exit 0
}





has_mergerfs_mounts_under_local_root() {
  local lines=() line tgt fstype
  local _tmp_findmnt="$(tmpfile findmnt)"
  ro_timeout_findmnt_pairs 5 -o TARGET,FSTYPE >"$_tmp_findmnt" 2>/dev/null || true
  mapfile -t lines <"$_tmp_findmnt" 2>/dev/null || true
  rm -f -- "$_tmp_findmnt" 2>/dev/null || true

  for line in "${lines[@]}"; do
    tgt="$(findmnt_p_get TARGET "$line")"
    fstype="$(findmnt_p_get FSTYPE "$line")"
    tgt="$(unescape_findmnt_string "$tgt")"

    local _under=0
    [[ "$tgt" == "$LOCAL_ROOT_REAL" || "$tgt" == "$LOCAL_ROOT_REAL/"* ]] && _under=1
    [[ "$tgt" == "$LOCAL_ROOT" || "$tgt" == "$LOCAL_ROOT/"* ]] && _under=1
    (( _under )) || continue
    [[ "${fstype,,}" == *mergerfs* ]] || continue
    return 0
  done
  return 1
}


# (Removed) find_other_instance_pid(): used ps scanning, which can hang on some Unraid setups.


startup_cleanup_on_start() {
  # If a previous run was killed/crashed without --stop, do best-effort cleanup now.
  # We only do this when we believe no other instance is running.
  local stale=0
  local pid=""

  log "Startup cleanup: entered"

  # Always try to break orphaned worker locks/PIDs (safe; state files are in /mnt)
  try_break_stale_worker_locks || true

  if [[ -f "$PID_FILE" ]]; then
    pid="$(cat "$PID_FILE" 2>/dev/null || true)"
    if [[ -n "${pid:-}" && "$pid" =~ ^[0-9]+$ ]] && kill -0 "$pid" 2>/dev/null && ! is_zombie_pid "$pid"; then
      # A supervisor appears to be running; don't touch anything.
      return 0
    fi
    stale=1
  fi

  if (( ! stale )) && has_mergerfs_mounts_under_local_root; then
    stale=1
  fi

  (( stale )) || return 0

  log "Startup cleanup: detected stale state; attempting best-effort cleanup"
  rm -f -- "$PID_FILE" "$MERGE_PID_FILE" "$RENAME_PID_FILE" 2>/dev/null || true

  if (( DRY_RUN )); then
    log "Startup cleanup: DRY-RUN mode; skipping unmount and branchlink cleanup"
    return 0
  fi

  if (( UNMOUNT_ON_EXIT )); then
    unmount_all_mergerfs_under_local_root || true
  else
    log "Startup cleanup: leaving mounts as-is (--no-unmount)"
  fi

  # Remove branchlink dirs that aren't referenced by any active mergerfs mount.
  # This avoids touching other mergerfs usage and prevents removing dirs still in use.
  declare -A IN_USE_BRANCHDIR=()
  local d=""
  while IFS= read -r d; do
    [[ -n "$d" ]] && IN_USE_BRANCHDIR["$d"]=1
  done < <(collect_in_use_branchdirs | sort -u)

  # BRANCHLINK_ROOT may live on /mnt/user; avoid glob expansion (can hang). Timebox access.
  if ! run_cmd_timeout 5 stat "$BRANCHLINK_ROOT" >/dev/null 2>&1; then
    log "Startup cleanup: BRANCHLINK_ROOT not accessible; skipping branchlink cleanup: $BRANCHLINK_ROOT"
    return 0
  fi
  local _tmp_bl="$(tmpfile branchlinks)"
  run_cmd_timeout 8 find "$BRANCHLINK_ROOT" -mindepth 1 -maxdepth 1 -type d -print0 >"$_tmp_bl" 2>/dev/null || true

  local d=""
  while IFS= read -r -d '' d; do
    [[ -d "$d" ]] || continue
    if [[ -z "${IN_USE_BRANCHDIR[$d]+x}" ]]; then
      log "Startup cleanup: remove stale branchlink dir: $d"
      run_cmd_timeout 10 rm -rf -- "$d" >/dev/null 2>&1 || true
    fi
  done <"$_tmp_bl"

  rm -f -- "$_tmp_bl" 2>/dev/null || true
}


run_cmd_supervisor() {
  mkdir -p "$STATE_DIR" "$MERGE_STATE_DIR" "$RENAME_STATE_DIR"
  open_log_fd

  log "Supervisor: invoked pid=$$ dry_run=$DRY_RUN unmount_on_exit=$UNMOUNT_ON_EXIT"

  log "Supervisor: preflight begin"

  [[ $EUID -eq 0 ]] || die "Run as root (required for mounts)."

  log "Supervisor: checking DOWNLOAD_ROOT accessibility: $DOWNLOAD_ROOT"
  # Accessing /mnt/user can occasionally block if the array is mid-transition.
  # Use a timeout so startup never hangs silently.
  if ! run_cmd_timeout 8 stat "$DOWNLOAD_ROOT" >/dev/null 2>&1; then
    die "DOWNLOAD_ROOT not found or not accessible: $DOWNLOAD_ROOT"
  fi
  log "Supervisor: DOWNLOAD_ROOT accessible"

  # Snapshot reference permissions once (used for all created paths)
  cache_reference_perms || true

  log "Supervisor: checking required commands"

  need_cmd inotifywait
  need_cmd find
  need_cmd awk
  need_cmd sed
  need_cmd sort
  need_cmd cksum
  need_cmd ln
  need_cmd stat
  need_cmd findmnt
  probe_findmnt_pairs_mode || true
  log "Supervisor: findmnt pairs-mode selected: $(findmnt_pairs_mode_name)"
  need_cmd mergerfs
  need_cmd flock

  pick_fusermount
  log "Supervisor: fusermount selected: ${FUSERMOUNT_BIN:-none}"

  log "Supervisor: checking PID file guard"

  # If a PID file exists and the PID is alive (and not a zombie), we are already running.
  if [[ -f "$PID_FILE" ]]; then
    local p=""
    p="$(cat "$PID_FILE" 2>/dev/null || true)"
    if [[ -n "${p:-}" && "$p" =~ ^[0-9]+$ ]] && kill -0 "$p" 2>/dev/null && ! is_zombie_pid "$p"; then
      die "Already running (pid $p)"
    fi
  fi

log "Supervisor: acquiring supervisor lock"
acquire_supervisor_lock
log "Supervisor: lock acquired"

  if (( STARTUP_CLEANUP )); then
    log "Supervisor: running startup cleanup"
    startup_cleanup_on_start || true
  else
    log "Supervisor: startup cleanup disabled"
    # Still break orphaned worker locks/PIDs so the daemons can start cleanly.
    try_break_stale_worker_locks || true
  fi

  echo $$ > "$PID_FILE"
  log "Supervisor: wrote PID file $PID_FILE"

  trap 'cleanup; exit 0' INT TERM
  trap 'cleanup' EXIT


init_watch_roots || true
local _w=""
for _w in "${WATCH_ROOTS[@]:-}"; do
  log "Supervisor: watch root: $_w"
done

log "Supervisor: starting (merge interval=${MERGE_INTERVAL_SECONDS}s, rename rescan=${RENAME_RESCAN_SECONDS}s)"

  ( mergerfs_daemon ) &
  RUNNING_MERGE_PID=$!
  log "Supervisor: mergerfs daemon pid $RUNNING_MERGE_PID"
  echo "$RUNNING_MERGE_PID" > "$MERGE_PID_FILE" 2>/dev/null || true

  ( chapter_rename_daemon ) &
  RUNNING_RENAME_PID=$!
  log "Supervisor: chapter renamer pid $RUNNING_RENAME_PID"
  echo "$RUNNING_RENAME_PID" > "$RENAME_PID_FILE" 2>/dev/null || true

  wait
}

################################################################################
# Arg parsing
################################################################################
CMD="run"

while (( $# )); do
  case "$1" in
    --run) CMD="run"; shift ;;
    --stop) CMD="stop"; shift ;;
    --status) CMD="status"; shift ;;

    --dry-run) DRY_RUN=1; shift ;;
    --no-unmount) UNMOUNT_ON_EXIT=0; shift ;;
    --unmount) UNMOUNT_ON_EXIT=1; shift ;;
    --no-low-prio) LOW_PRIO=0; shift ;;
--stop-timeout) require_int "$1" "${2:-}"; STOP_TIMEOUT_SECONDS="$2"; shift 2 ;;
--child-exit-grace) require_int "$1" "${2:-}"; CHILD_EXIT_GRACE_SECONDS="$2"; shift 2 ;;
--unmount-cmd-timeout) require_int "$1" "${2:-}"; UNMOUNT_CMD_TIMEOUT_SECONDS="$2"; shift 2 ;;
--unmount-detach-wait) require_int "$1" "${2:-}"; UNMOUNT_DETACH_WAIT_SECONDS="$2"; shift 2 ;;
--cleanup-high-prio) CLEANUP_HIGH_PRIO=1; shift ;;
--no-cleanup-high-prio) CLEANUP_HIGH_PRIO=0; shift ;;
    --rescan-now) RESCAN_NOW=1; shift ;;
    --no-rescan-now) RESCAN_NOW=0; shift ;;
    --startup-cleanup) STARTUP_CLEANUP=1; shift ;;
    --no-startup-cleanup) STARTUP_CLEANUP=0; shift ;;

    --merge-interval) require_int "$1" "${2:-}"; MERGE_INTERVAL_SECONDS="$2"; shift 2 ;;
    --rename-delay) require_int "$1" "${2:-}"; RENAME_DELAY_SECONDS="$2"; shift 2 ;;
    --rename-quiet) require_int "$1" "${2:-}"; RENAME_QUIET_SECONDS="$2"; shift 2 ;;
    --rename-poll) require_int "$1" "${2:-}"; RENAME_POLL_SECONDS="$2"; shift 2 ;;
    --rename-rescan) require_int "$1" "${2:-}"; RENAME_RESCAN_SECONDS="$2"; shift 2 ;;

    --timing) DEBUG_TIMING=1; shift ;;
    --no-timing) DEBUG_TIMING=0; shift ;;
    --timing-top) require_int "$1" "${2:-}"; DEBUG_TIMING_TOP_N="$2"; shift 2 ;;
    --timing-min-ms) require_int "$1" "${2:-}"; DEBUG_TIMING_MIN_ITEM_MS="$2"; shift 2 ;;
    --timing-slow-ms) require_int "$1" "${2:-}"; DEBUG_TIMING_SLOW_MS="$2"; shift 2 ;;

    --timeout-poll-ms) require_int "$1" "${2:-}"; TIMEOUT_POLL_MS="$2"; shift 2 ;;

    --timing-live) DEBUG_TIMING_LIVE=1; shift ;;
    --no-timing-live) DEBUG_TIMING_LIVE=0; shift ;;
    --scan-progress-every) require_int "$1" "${2:-}"; DEBUG_SCAN_PROGRESS_EVERY="$2"; shift 2 ;;
    --scan-progress-seconds) require_int "$1" "${2:-}"; DEBUG_SCAN_PROGRESS_SECONDS="$2"; shift 2 ;;

    --equiv-file) require_arg "$1" "${2:-}"; EQUIV_FILE="$2"; shift 2 ;;
    --priority-file) require_arg "$1" "${2:-}"; PRIORITY_FILE="$2"; shift 2 ;;
    -h|--help)
      cat <<EOF
Usage:
  $(basename "$0") --run [options]
  $(basename "$0") --status
  $(basename "$0") --stop

Useful options:
  --rescan-now            Enable startup rename rescan (default: on)
  --no-rescan-now         Disable startup rename rescan (still does periodic rescans)
  --merge-interval N      MergerFS periodic scan interval (default: $MERGE_INTERVAL_SECONDS)
  --rename-rescan N       Rename missed-event rescan interval (default: $RENAME_RESCAN_SECONDS)
  --no-unmount            Don't unmount mergerfs mounts on exit
  --dry-run               Print actions only (won't unmount on exit)
  --timing                Enable timing summaries (default: on)
  --no-timing             Disable timing summaries
  --timing-top N          Show top N slowest items per category (default: ${DEBUG_TIMING_TOP_N})
  --timing-min-ms N       Ignore items faster than N ms in top lists (default: ${DEBUG_TIMING_MIN_ITEM_MS})
  --timing-slow-ms N      Emit extra detail logs when an op exceeds N ms (default: ${DEBUG_TIMING_SLOW_MS})
  --timeout-poll-ms N     Poll interval for timeboxed commands, in ms (default: ${TIMEOUT_POLL_MS})
  --timing-live           Emit slow-op timing lines as they happen (default: on)
  --no-timing-live        Disable live slow-op timing lines
  --scan-progress-every N Log scan progress every N titles (default: ${DEBUG_SCAN_PROGRESS_EVERY}, 0=disable)
  --scan-progress-seconds N Log scan progress at least every N seconds (default: ${DEBUG_SCAN_PROGRESS_SECONDS}, 0=disable)
--stop-timeout N         Seconds to wait for clean stop before SIGKILL (default: $STOP_TIMEOUT_SECONDS)
--child-exit-grace N     Seconds supervisor waits for worker exit (default: $CHILD_EXIT_GRACE_SECONDS)
--unmount-cmd-timeout N  Per fusermount/umount timeout seconds (default: $UNMOUNT_CMD_TIMEOUT_SECONDS)
--unmount-detach-wait N  Seconds to wait after lazy detach (default: $UNMOUNT_DETACH_WAIT_SECONDS)
--cleanup-high-prio      Boost nice/ionice during cleanup/unmount (default: on)
--no-cleanup-high-prio   Disable cleanup priority boost
EOF
      exit 0
      ;;
    *) die "Unknown arg: $1" ;;
  esac
done

case "$CMD" in
  status) status_cmd ;;
  stop) stop_cmd ;;
  run) run_cmd_supervisor ;;
esac
