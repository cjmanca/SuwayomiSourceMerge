#!/usr/bin/env bash
set -euo pipefail

DEFAULT_MERGED_ROOT="/mnt/cache/appdata/ssm/merged"
DEFAULT_FUSE_CONF_PATH="/etc/fuse.conf"
DEFAULT_HOST_MNT_ROOT="/mnt"
DEFAULT_FALLBACK_PUID=99
DEFAULT_FALLBACK_PGID=100
LOCK_DIRECTORY_NAME=".ssm-lock"
LOCK_SENTINEL_FILE_NAME=".nosync"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

MERGED_ROOT="$DEFAULT_MERGED_ROOT"
MERGED_ROOT_EXPLICIT=0
FUSE_CONF_PATH="$DEFAULT_FUSE_CONF_PATH"
HOST_MNT_ROOT="${SETUP_HOST_SECURITY_MNT_ROOT:-$DEFAULT_HOST_MNT_ROOT}"
INSPECT_CONTAINER_NAME=""
INSPECTED_MERGED_ROOT=""
FALLBACK_PUID=""
FALLBACK_PGID=""
FALLBACK_PUID_EXPLICIT=0
FALLBACK_PGID_EXPLICIT=0

declare -a MANUAL_BIND_PATHS=()
declare -a INSPECTED_BIND_PATHS=()
declare -a EFFECTIVE_BIND_PATHS=()

normalize_path()
{
  local path="$1"
  while [[ "$path" != "/" && "$path" == */ ]]; do
    path="${path%/}"
  done

  printf '%s\n' "$path"
}

is_numeric_id()
{
  local value="$1"
  [[ "$value" =~ ^[0-9]+$ ]]
}

parse_disk_number()
{
  local disk_name="$1"
  if [[ "$disk_name" =~ ^disk([0-9]+)$ ]]; then
    printf '%s\n' "${BASH_REMATCH[1]}"
    return
  fi

  printf '\n'
}

validate_bind_path()
{
  local bind_path
  bind_path="$(normalize_path "$1")"

  if [[ "$bind_path" != /* ]]; then
    echo "Bind path '$bind_path' must be absolute." >&2
    exit 1
  fi

  if [[ "$bind_path" != "$HOST_MNT_ROOT"/* ]]; then
    echo "Bind path '$bind_path' must be under mount root '$HOST_MNT_ROOT'." >&2
    exit 1
  fi

  local relative_to_mount_root="${bind_path#"$HOST_MNT_ROOT"/}"
  if [[ "$relative_to_mount_root" = "$bind_path" || "$relative_to_mount_root" != */* ]]; then
    echo "Bind path '$bind_path' must include at least one directory below '$HOST_MNT_ROOT/<root>' so host mount roots are not modified." >&2
    exit 1
  fi

  printf '%s\n' "$bind_path"
}

discover_container_mount_paths()
{
  local inspect_output
  if ! command -v docker >/dev/null 2>&1; then
    echo "--inspect-container was provided but 'docker' is not available in PATH." >&2
    exit 1
  fi

  if ! inspect_output="$(docker inspect --format '{{range .Mounts}}{{printf "%s|%s|%s\n" .Type .Source .Destination}}{{end}}' "$INSPECT_CONTAINER_NAME" 2>&1)"; then
    echo "Failed to inspect container '$INSPECT_CONTAINER_NAME' for bind paths." >&2
    echo "docker inspect output: $inspect_output" >&2
    exit 1
  fi

  INSPECTED_BIND_PATHS=()
  INSPECTED_MERGED_ROOT=""
  local line
  while IFS= read -r line; do
    [[ -n "$line" ]] || continue
    local mount_type
    local mount_source
    local mount_destination
    IFS='|' read -r mount_type mount_source mount_destination <<< "$line"
    if [[ "$mount_type" != "bind" ]]; then
      continue
    fi

    case "$mount_destination" in
      /ssm/sources/*|/ssm/override/*)
        INSPECTED_BIND_PATHS+=("$mount_source")
        ;;
      /ssm/merged)
        if [[ -n "$INSPECTED_MERGED_ROOT" && "$INSPECTED_MERGED_ROOT" != "$mount_source" ]]; then
          echo "Container '$INSPECT_CONTAINER_NAME' exposes multiple bind mounts for /ssm/merged." >&2
          echo "Provide --merged-root explicitly to avoid ambiguity." >&2
          exit 1
        fi

        INSPECTED_MERGED_ROOT="$mount_source"
        ;;
    esac
  done <<< "$inspect_output"
}

discover_container_fallback_identity()
{
  if [[ -z "$INSPECT_CONTAINER_NAME" ]]; then
    return
  fi

  if [[ "$FALLBACK_PUID_EXPLICIT" -eq 1 && "$FALLBACK_PGID_EXPLICIT" -eq 1 ]]; then
    return
  fi

  local inspect_output
  if ! inspect_output="$(docker inspect --format '{{range .Config.Env}}{{println .}}{{end}}' "$INSPECT_CONTAINER_NAME" 2>&1)"; then
    echo "Failed to inspect container '$INSPECT_CONTAINER_NAME' environment for fallback identity." >&2
    echo "docker inspect output: $inspect_output" >&2
    exit 1
  fi

  local discovered_puid=""
  local discovered_pgid=""
  local line
  while IFS= read -r line; do
    case "$line" in
      PUID=*)
        discovered_puid="${line#PUID=}"
        ;;
      PGID=*)
        discovered_pgid="${line#PGID=}"
        ;;
    esac
  done <<< "$inspect_output"

  if [[ "$FALLBACK_PUID_EXPLICIT" -eq 0 && -n "$discovered_puid" ]] && is_numeric_id "$discovered_puid"; then
    FALLBACK_PUID="$discovered_puid"
  fi

  if [[ "$FALLBACK_PGID_EXPLICIT" -eq 0 && -n "$discovered_pgid" ]] && is_numeric_id "$discovered_pgid"; then
    FALLBACK_PGID="$discovered_pgid"
  fi
}

resolve_fallback_identity()
{
  if [[ -z "$FALLBACK_PUID" ]]; then
    FALLBACK_PUID="$DEFAULT_FALLBACK_PUID"
  fi

  if [[ -z "$FALLBACK_PGID" ]]; then
    FALLBACK_PGID="$DEFAULT_FALLBACK_PGID"
  fi

  if ! is_numeric_id "$FALLBACK_PUID"; then
    echo "Invalid fallback PUID '$FALLBACK_PUID'. Expected a numeric UID." >&2
    exit 1
  fi

  if ! is_numeric_id "$FALLBACK_PGID"; then
    echo "Invalid fallback PGID '$FALLBACK_PGID'. Expected a numeric GID." >&2
    exit 1
  fi
}

resolve_merged_root_path()
{
  if [[ "$MERGED_ROOT_EXPLICIT" -eq 0 && -n "$INSPECT_CONTAINER_NAME" ]]; then
    if [[ -z "$INSPECTED_MERGED_ROOT" ]]; then
      echo "Container '$INSPECT_CONTAINER_NAME' did not expose a bind mount for '/ssm/merged', and --merged-root was not provided." >&2
      echo "Provide --merged-root PATH (or fix container mounts) before running this script." >&2
      exit 1
    fi

    MERGED_ROOT="$INSPECTED_MERGED_ROOT"
  fi

  MERGED_ROOT="$(normalize_path "$MERGED_ROOT")"
  if [[ "$MERGED_ROOT" != /* ]]; then
    echo "Merged root '$MERGED_ROOT' must be an absolute path." >&2
    exit 1
  fi
}

prepare_bind_paths()
{
  if [[ -n "$INSPECT_CONTAINER_NAME" ]]; then
    discover_container_mount_paths
    discover_container_fallback_identity
  fi

  resolve_fallback_identity
  resolve_merged_root_path

  EFFECTIVE_BIND_PATHS=()
  declare -A unique_bind_paths=()

  local bind_path
  for bind_path in "${INSPECTED_BIND_PATHS[@]}" "${MANUAL_BIND_PATHS[@]}"; do
    [[ -n "$bind_path" ]] || continue
    local validated_bind_path
    validated_bind_path="$(validate_bind_path "$bind_path")"
    if [[ -n "${unique_bind_paths[$validated_bind_path]:-}" ]]; then
      continue
    fi

    unique_bind_paths[$validated_bind_path]=1
    EFFECTIVE_BIND_PATHS+=("$validated_bind_path")
  done

  if [[ -n "$INSPECT_CONTAINER_NAME" && "${#INSPECTED_BIND_PATHS[@]}" -eq 0 && "${#MANUAL_BIND_PATHS[@]}" -eq 0 ]]; then
    echo "Container '$INSPECT_CONTAINER_NAME' did not expose usable bind mounts for /ssm/sources/* or /ssm/override/*, and no --bind-path was provided." >&2
    echo "Provide one or more --bind-path values (or fix container mounts) before running this script." >&2
    exit 1
  fi
}

read_directory_metadata()
{
  local directory_path="$1"
  local stat_output
  stat_output="$(stat -c '%u|%g|%a|%Y' "$directory_path")"

  DIRECTORY_UID=""
  DIRECTORY_GID=""
  DIRECTORY_MODE=""
  DIRECTORY_MTIME=""
  IFS='|' read -r DIRECTORY_UID DIRECTORY_GID DIRECTORY_MODE DIRECTORY_MTIME <<< "$stat_output"
}

select_peer_metadata_for_relative_path()
{
  local relative_path="$1"
  local target_segment_path="$2"

  PEER_METADATA_FOUND=0
  PEER_SELECTED_UID=""
  PEER_SELECTED_GID=""
  PEER_SELECTED_MODE=""
  PEER_SELECTED_SOURCE_PATH=""

  declare -A tuple_counts=()
  declare -A tuple_best_mtime=()
  declare -A tuple_best_disk=()
  declare -A tuple_best_path=()

  local disk_root_path
  for disk_root_path in "$HOST_MNT_ROOT"/disk*; do
    if [[ ! -d "$disk_root_path" ]]; then
      continue
    fi

    local disk_name
    local disk_number
    disk_name="$(basename "$disk_root_path")"
    disk_number="$(parse_disk_number "$disk_name")"
    if [[ -z "$disk_number" ]]; then
      continue
    fi

    local candidate_path="$disk_root_path/$relative_path"
    if [[ "$candidate_path" = "$target_segment_path" ]]; then
      continue
    fi

    if [[ ! -d "$candidate_path" ]]; then
      continue
    fi

    if [[ -L "$candidate_path" ]]; then
      continue
    fi

    local candidate_stat_output
    candidate_stat_output="$(stat -c '%u|%g|%a|%Y' "$candidate_path")"
    local candidate_uid
    local candidate_gid
    local candidate_mode
    local candidate_mtime
    IFS='|' read -r candidate_uid candidate_gid candidate_mode candidate_mtime <<< "$candidate_stat_output"

    local metadata_key="$candidate_uid:$candidate_gid:$candidate_mode"
    tuple_counts[$metadata_key]=$(( ${tuple_counts[$metadata_key]:-0} + 1 ))

    if [[ -z "${tuple_best_mtime[$metadata_key]:-}" || "$candidate_mtime" -gt "${tuple_best_mtime[$metadata_key]}" ]]; then
      tuple_best_mtime[$metadata_key]="$candidate_mtime"
      tuple_best_disk[$metadata_key]="$disk_number"
      tuple_best_path[$metadata_key]="$candidate_path"
      continue
    fi

    if [[ "$candidate_mtime" -eq "${tuple_best_mtime[$metadata_key]}" && "$disk_number" -lt "${tuple_best_disk[$metadata_key]}" ]]; then
      tuple_best_disk[$metadata_key]="$disk_number"
      tuple_best_path[$metadata_key]="$candidate_path"
    fi
  done

  if [[ "${#tuple_counts[@]}" -eq 0 ]]; then
    return
  fi

  local highest_vote_count=0
  local metadata_key
  for metadata_key in "${!tuple_counts[@]}"; do
    if [[ "${tuple_counts[$metadata_key]}" -gt "$highest_vote_count" ]]; then
      highest_vote_count="${tuple_counts[$metadata_key]}"
    fi
  done

  local selected_key=""
  local selected_mtime=-1
  local selected_disk=999999

  for metadata_key in "${!tuple_counts[@]}"; do
    if [[ "${tuple_counts[$metadata_key]}" -ne "$highest_vote_count" ]]; then
      continue
    fi

    local candidate_mtime="${tuple_best_mtime[$metadata_key]}"
    local candidate_disk="${tuple_best_disk[$metadata_key]}"
    if [[ "$candidate_mtime" -gt "$selected_mtime" ]]; then
      selected_key="$metadata_key"
      selected_mtime="$candidate_mtime"
      selected_disk="$candidate_disk"
      continue
    fi

    if [[ "$candidate_mtime" -eq "$selected_mtime" && "$candidate_disk" -lt "$selected_disk" ]]; then
      selected_key="$metadata_key"
      selected_disk="$candidate_disk"
    fi
  done

  if [[ -z "$selected_key" ]]; then
    return
  fi

  PEER_METADATA_FOUND=1
  PEER_SELECTED_SOURCE_PATH="${tuple_best_path[$selected_key]}"
  IFS=':' read -r PEER_SELECTED_UID PEER_SELECTED_GID PEER_SELECTED_MODE <<< "$selected_key"
}

repair_bind_path_chain()
{
  local bind_path="$1"

  local relative_to_mount_root="${bind_path#"$HOST_MNT_ROOT"/}"
  local root_name="${relative_to_mount_root%%/*}"
  local relative_to_root="${relative_to_mount_root#"$root_name"/}"
  local root_path="$HOST_MNT_ROOT/$root_name"

  if [[ ! -d "$root_path" ]]; then
    echo "Mount-root path '$root_path' does not exist for bind path '$bind_path'." >&2
    echo "Create or mount '$root_path' before running this script." >&2
    exit 1
  fi

  if [[ -L "$root_path" ]]; then
    echo "Mount-root path '$root_path' is a symlink and is not supported for ownership repair." >&2
    exit 1
  fi

  echo "Repairing bind-path chain: $bind_path"

  local current_path="$root_path"
  local accumulated_relative_path=""
  local segment

  IFS='/' read -r -a relative_segments <<< "$relative_to_root"
  for segment in "${relative_segments[@]}"; do
    accumulated_relative_path="$accumulated_relative_path/$segment"
    accumulated_relative_path="${accumulated_relative_path#/}"
    current_path="$current_path/$segment"

    local created_segment=0
    if [[ ! -e "$current_path" ]]; then
      # Use mkdir -p so repeated (or concurrent) runs remain idempotent.
      mkdir -p "$current_path"
      created_segment=1
    fi

    if [[ ! -d "$current_path" ]]; then
      echo "Path '$current_path' exists but is not a directory. Cannot repair bind-path chain." >&2
      exit 1
    fi

    if [[ -L "$current_path" ]]; then
      echo "Path '$current_path' is a symlink. Refusing to follow symlink during ownership repair." >&2
      exit 1
    fi

    read_directory_metadata "$current_path"
    local original_uid="$DIRECTORY_UID"
    local original_gid="$DIRECTORY_GID"
    local original_mode="$DIRECTORY_MODE"

    select_peer_metadata_for_relative_path "$accumulated_relative_path" "$current_path"

    local target_uid
    local target_gid
    local target_mode=""
    local source_label="fallback"

    if [[ "$PEER_METADATA_FOUND" -eq 1 ]]; then
      target_uid="$PEER_SELECTED_UID"
      target_gid="$PEER_SELECTED_GID"
      target_mode="$PEER_SELECTED_MODE"
      source_label="peer:$PEER_SELECTED_SOURCE_PATH"
    else
      target_uid="$FALLBACK_PUID"
      target_gid="$FALLBACK_PGID"
    fi

    if [[ "$original_uid" != "$target_uid" || "$original_gid" != "$target_gid" ]]; then
      chown "$target_uid:$target_gid" "$current_path"
    fi

    if [[ -n "$target_mode" && "$original_mode" != "$target_mode" ]]; then
      chmod "$target_mode" "$current_path"
    fi

    read_directory_metadata "$current_path"
    local action_label="existing"
    if [[ "$created_segment" -eq 1 ]]; then
      action_label="created"
    fi

    echo "  [$action_label] $current_path => owner=$DIRECTORY_UID:$DIRECTORY_GID mode=$DIRECTORY_MODE source=$source_label"
  done
}

ensure_bind_path_mover_lock_sentinel()
{
  local bind_path="$1"
  local lock_directory_path="$bind_path/$LOCK_DIRECTORY_NAME"
  local lock_sentinel_path="$lock_directory_path/$LOCK_SENTINEL_FILE_NAME"

  if [[ -e "$lock_directory_path" && ! -d "$lock_directory_path" ]]; then
    echo "Mover lock path '$lock_directory_path' exists but is not a directory. Cannot continue." >&2
    exit 1
  fi

  mkdir -p "$lock_directory_path"

  if [[ -L "$lock_directory_path" ]]; then
    echo "Mover lock path '$lock_directory_path' is a symlink. Refusing to follow symlink during lock setup." >&2
    exit 1
  fi

  if [[ -e "$lock_sentinel_path" && ! -f "$lock_sentinel_path" ]]; then
    echo "Mover lock sentinel path '$lock_sentinel_path' exists but is not a regular file. Cannot continue." >&2
    exit 1
  fi

  if [[ -L "$lock_sentinel_path" ]]; then
    echo "Mover lock sentinel path '$lock_sentinel_path' is a symlink. Refusing to follow symlink during lock setup." >&2
    exit 1
  fi

  # Truncate or create the sentinel file after type and symlink safety checks.
  : > "$lock_sentinel_path"

  read_directory_metadata "$bind_path"
  local bind_uid="$DIRECTORY_UID"
  local bind_gid="$DIRECTORY_GID"

  chown "$bind_uid:$bind_gid" "$lock_directory_path" "$lock_sentinel_path"
  chmod 0644 "$lock_sentinel_path"

  echo "  [lock] $lock_sentinel_path => owner=$bind_uid:$bind_gid mode=0644"
}

repair_bind_path_ownership()
{
  if [[ "${#EFFECTIVE_BIND_PATHS[@]}" -eq 0 ]]; then
    echo "No bind-path ownership repair requested (use --inspect-container and/or --bind-path)."
    return
  fi

  echo "Repairing bind-path parent ownership/mode using fallback identity '$FALLBACK_PUID:$FALLBACK_PGID' and peer lookup under '$HOST_MNT_ROOT/disk*'..."

  local bind_path
  for bind_path in "${EFFECTIVE_BIND_PATHS[@]}"; do
    repair_bind_path_chain "$bind_path"
    ensure_bind_path_mover_lock_sentinel "$bind_path"
  done
}

usage()
{
  cat <<__USAGE__
Usage: $(basename "$0") [options]

Prepares host state for hardened SSM runtime:
  - bind-path parent ownership/mode repair for source and override volumes
  - merged bind propagation (mount --bind, --make-private, --make-rshared)
  - host fuse.conf user_allow_other entry

Options:
  --inspect-container NAME  Discover bind paths via 'docker inspect' for /ssm/sources/* and /ssm/override/*
  --bind-path PATH          Bind path to repair; repeatable fallback when container is not yet created
  --fallback-puid UID       Fallback owner UID when no peer disk path exists (default: container PUID, else $DEFAULT_FALLBACK_PUID)
  --fallback-pgid GID       Fallback owner GID when no peer disk path exists (default: container PGID, else $DEFAULT_FALLBACK_PGID)
  --mount-root PATH         Host mount root for bind paths and peer scanning (default: $DEFAULT_HOST_MNT_ROOT)
  --merged-root PATH        Merged host path (default: inspect /ssm/merged bind when --inspect-container is used, else $DEFAULT_MERGED_ROOT)
  --fuse-conf PATH          fuse.conf path (default: $DEFAULT_FUSE_CONF_PATH)
  --help                    Show this help

Notes:
  - Peer metadata is searched only from disk paths under:
__USAGE__
  printf '    %s/disk*\n' "$HOST_MNT_ROOT"
  cat <<__USAGE__
  - Existing and missing bind-path chain segments are repaired.
  - Owner/group/mode is cloned from peer majority; ties use newest mtime, then lowest disk number.
__USAGE__
}

require_option_value()
{
  local option_name="$1"
  local option_value="${2:-}"
  if [[ -z "$option_value" ]]; then
    echo "Missing value for $option_name." >&2
    usage >&2
    exit 1
  fi
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --inspect-container)
      require_option_value "$1" "${2:-}"
      INSPECT_CONTAINER_NAME="$2"
      shift 2
      ;;
    --bind-path)
      require_option_value "$1" "${2:-}"
      MANUAL_BIND_PATHS+=("$2")
      shift 2
      ;;
    --fallback-puid)
      require_option_value "$1" "${2:-}"
      FALLBACK_PUID="$2"
      FALLBACK_PUID_EXPLICIT=1
      shift 2
      ;;
    --fallback-pgid)
      require_option_value "$1" "${2:-}"
      FALLBACK_PGID="$2"
      FALLBACK_PGID_EXPLICIT=1
      shift 2
      ;;
    --mount-root)
      require_option_value "$1" "${2:-}"
      HOST_MNT_ROOT="$2"
      shift 2
      ;;
    --merged-root)
      require_option_value "$1" "${2:-}"
      MERGED_ROOT="$2"
      MERGED_ROOT_EXPLICIT=1
      shift 2
      ;;
    --fuse-conf)
      require_option_value "$1" "${2:-}"
      FUSE_CONF_PATH="$2"
      shift 2
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

HOST_MNT_ROOT="$(normalize_path "$HOST_MNT_ROOT")"
if [[ "$HOST_MNT_ROOT" != /* ]]; then
  echo "--mount-root must be an absolute path. Received '$HOST_MNT_ROOT'." >&2
  exit 1
fi

if [[ "${SETUP_HOST_SECURITY_SKIP_ROOT_CHECK:-0}" != "1" && "${EUID}" -ne 0 ]]; then
  echo "This script must run as root." >&2
  exit 1
fi

ensure_user_allow_other()
{
  if [[ ! -f "$FUSE_CONF_PATH" ]]; then
    printf 'user_allow_other\n' > "$FUSE_CONF_PATH"
    return
  fi

  if grep -Eq '^[[:space:]]*user_allow_other([[:space:]]*#.*)?[[:space:]]*$' "$FUSE_CONF_PATH"; then
    return
  fi

  printf '\nuser_allow_other\n' >> "$FUSE_CONF_PATH"
}

prepare_merged_propagation()
{
  # Keep merged bind setup identical to README host snippet semantics.
  mkdir -p "$MERGED_ROOT"
  mountpoint -q "$MERGED_ROOT" || mount --bind "$MERGED_ROOT" "$MERGED_ROOT"
  mount --make-private "$MERGED_ROOT"
  mount --make-rshared "$MERGED_ROOT"
}

print_runtime_snippets()
{
  cat <<__SNIPPETS__

Host preparation complete.

Container runtime flags:
  --device /dev/fuse
  --cap-add SYS_ADMIN
  -e ENTRYPOINT_FUSE_CONF_MODE=host-managed
  -v /etc/fuse.conf:/etc/fuse.conf:ro

Compose snippet:
  volumes:
    - /etc/fuse.conf:/etc/fuse.conf:ro
  devices:
    - /dev/fuse:/dev/fuse
  cap_add:
    - SYS_ADMIN
  environment:
    ENTRYPOINT_FUSE_CONF_MODE: "host-managed"
__SNIPPETS__
}

prepare_bind_paths
repair_bind_path_ownership

echo "Preparing merged bind propagation on '$MERGED_ROOT'..."
prepare_merged_propagation

echo "Ensuring 'user_allow_other' exists in '$FUSE_CONF_PATH'..."
ensure_user_allow_other

print_runtime_snippets
