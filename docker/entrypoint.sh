#!/usr/bin/env bash
set -euo pipefail

DEFAULT_PUID=99
DEFAULT_PGID=100
DEFAULT_SSM_USER="ssm"
DEFAULT_SSM_GROUP="ssm"
DEFAULT_LOG_FILE_NAME="daemon.log"
DEFAULT_LOG_ROOT_PATH="/ssm/config"
MERGED_ROOT_PATH="/ssm/merged"
FUSE_CONF_PATH="${FUSE_CONF_PATH:-/etc/fuse.conf}"
FUSE_DEVICE_PATH="${FUSE_DEVICE_PATH:-/dev/fuse}"
ENTRYPOINT_SETTINGS_PATH="${ENTRYPOINT_SETTINGS_PATH:-/ssm/config/settings.yml}"
ENTRYPOINT_LOG_FILE=""
# Runtime identity string is intentionally formatted for direct gosu invocation.
RUNTIME_GOSU_IDENTITY=""

PUID="${PUID:-$DEFAULT_PUID}"
PGID="${PGID:-$DEFAULT_PGID}"

resolve_settings_scalar() {
  local file_path="$1"
  local section_name="$2"
  local key_name="$3"

  awk -v section_name="$section_name" -v key_name="$key_name" '
    function ltrim(value) {
      sub(/^[[:space:]]+/, "", value)
      return value
    }

    function rtrim(value) {
      sub(/[[:space:]]+$/, "", value)
      return value
    }

    function trim(value) {
      return rtrim(ltrim(value))
    }

    function strip_inline_comment(value,    i, ch, quote, result, single_quote, escaped, previous_char) {
      single_quote = sprintf("%c", 39)
      quote = ""
      result = ""
      escaped = 0

      for (i = 1; i <= length(value); i++) {
        ch = substr(value, i, 1)
        # In plain-scalar context, '#' starts a comment only at token boundary.
        if (quote == "") {
          previous_char = i > 1 ? substr(value, i - 1, 1) : ""
          if (ch == "#" && (i == 1 || previous_char ~ /[[:space:]]/)) {
            return rtrim(result)
          }

          # Enter quoted context; comment stripping is disabled until quote closes.
          if (ch == "\"" || ch == single_quote) {
            quote = ch
            escaped = 0
          }

          result = result ch
        } else {
          result = result ch

          # Track backslash parity so escaped quotes do not terminate quoted mode.
          if (ch == "\\") {
            escaped = 1 - escaped
            continue
          }

          if (ch == quote && escaped == 0) {
            quote = ""
          }

          escaped = 0
        }
      }

      return rtrim(result)
    }

    function unquote(value,    first, last, single_quote) {
      single_quote = sprintf("%c", 39)
      if (length(value) < 2) {
        return value
      }

      first = substr(value, 1, 1)
      last = substr(value, length(value), 1)
      if ((first == "\"" || first == single_quote) && last == first) {
        return substr(value, 2, length(value) - 2)
      }

      return value
    }

    {
      if ($0 ~ "^[[:space:]]*" section_name ":[[:space:]]*($|#)") {
        in_section = 1
        next
      }

      if (in_section && $0 ~ "^[^[:space:]]") {
        in_section = 0
      }

      if (in_section && $0 ~ "^[[:space:]]*" key_name ":[[:space:]]*") {
        line = $0
        sub("^[[:space:]]*" key_name ":[[:space:]]*", "", line)
        line = trim(strip_inline_comment(line))
        line = unquote(line)
        print line
        exit
      }
    }
  ' "$file_path"
}

is_safe_entrypoint_log_root_path() {
  local value="$1"
  [[ -n "$value" && "$value" = /* ]]
}

is_reserved_windows_device_name() {
  local value="$1"
  local base_name="${value%%.*}"
  local had_nocasematch=0
  if shopt -q nocasematch; then
    had_nocasematch=1
  fi

  shopt -s nocasematch
  local is_reserved=1
  case "$base_name" in
    CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])
      is_reserved=0
      ;;
  esac

  if [[ "$had_nocasematch" -eq 0 ]]; then
    shopt -u nocasematch
  fi

  return "$is_reserved"
}

is_safe_entrypoint_log_file_name() {
  local value="$1"
  if [[ -z "$value" ]]; then
    return 1
  fi

  if [[ "$value" =~ ^[[:space:]] || "$value" =~ [[:space:]]$ ]]; then
    return 1
  fi

  if [[ "$value" = /* ]]; then
    return 1
  fi

  if [[ "$value" == *"/"* || "$value" == *"\\"* ]]; then
    return 1
  fi

  if [[ "$value" = "." || "$value" = ".." ]]; then
    return 1
  fi

  if [[ "$value" == *"." ]]; then
    return 1
  fi

  if [[ "$value" =~ [[:cntrl:]] ]]; then
    return 1
  fi

  if [[ "$value" == *"<"* || "$value" == *">"* || "$value" == *":"* || "$value" == *"\""* || "$value" == *"|"* || "$value" == *"?"* || "$value" == *"*"* ]]; then
    return 1
  fi

  if is_reserved_windows_device_name "$value"; then
    return 1
  fi

  return 0
}

resolve_entrypoint_log_file() {
  local log_root_path="$DEFAULT_LOG_ROOT_PATH"
  local log_file_name="$DEFAULT_LOG_FILE_NAME"
  local root_path_warning=""
  local file_name_warning=""

  if [[ -f "$ENTRYPOINT_SETTINGS_PATH" ]]; then
    local parsed_log_root_path
    local parsed_log_file_name
    parsed_log_root_path="$(resolve_settings_scalar "$ENTRYPOINT_SETTINGS_PATH" "paths" "log_root_path" || true)"
    parsed_log_file_name="$(resolve_settings_scalar "$ENTRYPOINT_SETTINGS_PATH" "logging" "file_name" || true)"
    if [[ -n "$parsed_log_root_path" ]]; then
      if is_safe_entrypoint_log_root_path "$parsed_log_root_path"; then
        log_root_path="$parsed_log_root_path"
      else
        root_path_warning="WARN: Ignoring unsafe paths.log_root_path='$parsed_log_root_path' from '$ENTRYPOINT_SETTINGS_PATH'; falling back to default '$DEFAULT_LOG_ROOT_PATH'."
      fi
    fi

    if [[ -n "$parsed_log_file_name" ]]; then
      if is_safe_entrypoint_log_file_name "$parsed_log_file_name"; then
        log_file_name="$parsed_log_file_name"
      else
        file_name_warning="WARN: Ignoring unsafe logging.file_name='$parsed_log_file_name' from '$ENTRYPOINT_SETTINGS_PATH'; falling back to default '$DEFAULT_LOG_FILE_NAME'."
      fi
    fi
  fi

  ENTRYPOINT_LOG_FILE="$log_root_path/$log_file_name"

  if [[ -n "$root_path_warning" ]]; then
    entrypoint_log "$root_path_warning"
  fi

  if [[ -n "$file_name_warning" ]]; then
    entrypoint_log "$file_name_warning"
  fi
}

append_entrypoint_log_line() {
  local message_line="$1"
  if [[ -z "$ENTRYPOINT_LOG_FILE" ]]; then
    return
  fi

  local log_directory_path
  log_directory_path="$(dirname "$ENTRYPOINT_LOG_FILE")"
  {
    mkdir -p "$log_directory_path"
    printf '%s\n' "$message_line" >> "$ENTRYPOINT_LOG_FILE"
  } >/dev/null 2>&1 || true
}

entrypoint_log() {
  local message_line="$1"
  printf '%s\n' "$message_line" >&2
  append_entrypoint_log_line "$message_line"
}

entrypoint_log_block() {
  local message_block="$1"
  local message_line
  while IFS= read -r message_line || [[ -n "$message_line" ]]; do
    entrypoint_log "$message_line"
  done <<< "$message_block"
}

report_fuse_conf_write_failure() {
  local write_error_detail="${1:-unknown write failure}"
  local failure_message
  failure_message="$(cat <<EOF
ERROR: Failed to update '$FUSE_CONF_PATH' with 'user_allow_other' while running as non-root (PUID=$PUID).
Mergerfs option 'allow_other' requires this setting for non-root mounts.
Root cause detail: $write_error_detail

How to fix:
1) Manual edit (keep non-root runtime):
   - Ensure '$FUSE_CONF_PATH' is writable in the container.
   - Add this line exactly once:
       user_allow_other
   - Example command:
       sh -c "grep -Eq '^[[:space:]]*user_allow_other([[:space:]]*#.*)?$' \"$FUSE_CONF_PATH\" || printf '\nuser_allow_other\n' >> \"$FUSE_CONF_PATH\""

2) Run as root:
   - Set environment variable:
       PUID=0
EOF
)"
  entrypoint_log_block "$failure_message"
}

resolve_entrypoint_log_file

read_login_defs_value() {
  local key="$1"
  local fallback="$2"
  local value

  # Extract the first whitespace-separated value (second field) for the given key
  # from /etc/login.defs. If multiple lines match the key, the last one is used.
  # Any error (missing file, awk failure, no matches) results in an empty value.
  value="$(awk -v lookup_key="$key" '$1 == lookup_key { print $2 }' /etc/login.defs 2>/dev/null | tail -n1 || true)"
  if [[ "$value" =~ ^[0-9]+$ ]]; then
    echo "$value"
  else
    echo "$fallback"
  fi
}

if ! [[ "$PUID" =~ ^[0-9]+$ ]]; then
  entrypoint_log "Invalid PUID value: '$PUID'. Expected an integer."
  exit 64
fi

if ! [[ "$PGID" =~ ^[0-9]+$ ]]; then
  entrypoint_log "Invalid PGID value: '$PGID'. Expected an integer."
  exit 64
fi

ensure_user_allow_other() {
  if [[ "$PUID" = "0" ]]; then
    return
  fi

  if [[ ! -f "$FUSE_CONF_PATH" ]]; then
    local create_error
    if ! create_error="$(printf 'user_allow_other\n' > "$FUSE_CONF_PATH" 2>&1)"; then
      report_fuse_conf_write_failure "$create_error"
      exit 70
    fi
    return
  fi

  if grep -Eq '^[[:space:]]*user_allow_other([[:space:]]*#.*)?$' "$FUSE_CONF_PATH"; then
    return
  fi

  local append_error
  if ! append_error="$(printf '\nuser_allow_other\n' >> "$FUSE_CONF_PATH" 2>&1)"; then
    report_fuse_conf_write_failure "$append_error"
    exit 70
  fi
}

ensure_user_allow_other

resolve_runtime_gosu_identity() {
  local runtime_user_name
  runtime_user_name="$(getent passwd "$PUID" | cut -d: -f1 || true)"
  if [[ -n "$runtime_user_name" ]]; then
    RUNTIME_GOSU_IDENTITY="$runtime_user_name:$PGID"
    return
  fi

  RUNTIME_GOSU_IDENTITY="$PUID:$PGID"
}

ensure_runtime_fuse_device_access() {
  if [[ "$PUID" = "0" ]]; then
    return
  fi

  if [[ -e "/ssm/mock-bin/mergerfs" ]]; then
    entrypoint_log "WARN: Skipping FUSE device preflight because mock mergerfs tool '/ssm/mock-bin/mergerfs' is present."
    return
  fi

  if [[ ! -e "$FUSE_DEVICE_PATH" ]]; then
    entrypoint_log "WARN: '$FUSE_DEVICE_PATH' is not present. Mergerfs mounts will fail unless runtime is using mocked tool scripts."
    return
  fi

  local runtime_user_name
  runtime_user_name="$(getent passwd "$PUID" | cut -d: -f1 || true)"
  local fuse_device_group_id
  fuse_device_group_id="$(stat -c '%g' "$FUSE_DEVICE_PATH" 2>/dev/null || true)"
  if [[ -n "$runtime_user_name" && "$fuse_device_group_id" =~ ^[0-9]+$ ]]; then
    local fuse_device_group_name
    fuse_device_group_name="$(getent group "$fuse_device_group_id" | cut -d: -f1 || true)"
    if [[ -n "$fuse_device_group_name" ]]; then
      local add_group_error
      if ! add_group_error="$(usermod -a -G "$fuse_device_group_name" "$runtime_user_name" 2>&1)"; then
        entrypoint_log "WARN: Failed to add runtime user '$runtime_user_name' to FUSE device group '$fuse_device_group_name'. Detail: $add_group_error"
      fi
    fi
  fi

  # Keep direct test invocations separate to avoid extra shell indirection/quoting with sh -c.
  if gosu "$RUNTIME_GOSU_IDENTITY" test -r "$FUSE_DEVICE_PATH" &&
    gosu "$RUNTIME_GOSU_IDENTITY" test -w "$FUSE_DEVICE_PATH"; then
    return
  fi

  local fuse_device_access_detail
  fuse_device_access_detail="$(stat -c 'mode=%a owner=%u group=%g' "$FUSE_DEVICE_PATH" 2>/dev/null || echo "stat unavailable")"
  entrypoint_log "ERROR: Runtime identity '$RUNTIME_GOSU_IDENTITY' cannot read/write '$FUSE_DEVICE_PATH' ($fuse_device_access_detail). Mergerfs mounts will fail with 'Operation not permitted'."
  entrypoint_log "Fix access to '$FUSE_DEVICE_PATH' for PUID=$PUID (for example by mapping the device and granting user/group read+write permissions)."
  exit 70
}

existing_group_name="$(getent group "$PGID" | cut -d: -f1 || true)"
if [[ -z "$existing_group_name" ]]; then
  desired_group_name="$DEFAULT_SSM_GROUP"
  default_group_gid="$(getent group "$DEFAULT_SSM_GROUP" | cut -d: -f3 || true)"
  if [[ -n "$default_group_gid" && "$default_group_gid" != "$PGID" ]]; then
    desired_group_name="${DEFAULT_SSM_GROUP}-gid-$PGID"
    entrypoint_log "WARN: Group '$DEFAULT_SSM_GROUP' already maps to GID '$default_group_gid'; using fallback group name '$desired_group_name' for requested PGID '$PGID'."
  fi

  groupadd --gid "$PGID" "$desired_group_name"
elif [[ "$existing_group_name" != "$DEFAULT_SSM_GROUP" ]]; then
  default_group_gid="$(getent group "$DEFAULT_SSM_GROUP" | cut -d: -f3 || true)"
  if [[ -n "$default_group_gid" && "$default_group_gid" != "$PGID" ]]; then
    entrypoint_log "WARN: PGID '$PGID' already maps to group '$existing_group_name' (expected '$DEFAULT_SSM_GROUP'); continuing with existing group."
  fi
fi

if ! getent passwd "$PUID" >/dev/null 2>&1; then
  desired_user_name="$DEFAULT_SSM_USER"
  default_user_uid="$(getent passwd "$DEFAULT_SSM_USER" | cut -d: -f3 || true)"
  if [[ -n "$default_user_uid" && "$default_user_uid" != "$PUID" ]]; then
    desired_user_name="${DEFAULT_SSM_USER}-uid-$PUID"
    entrypoint_log "WARN: User '$DEFAULT_SSM_USER' already maps to UID '$default_user_uid'; using fallback user name '$desired_user_name' for requested PUID '$PUID'."
  fi

  uid_min="$(read_login_defs_value UID_MIN 1000)"
  uid_max="$(read_login_defs_value UID_MAX 60000)"
  useradd_args=(--uid "$PUID" --gid "$PGID" --no-create-home --shell /usr/sbin/nologin)

  if [[ "$PUID" -lt "$uid_min" ]]; then
    useradd_args+=(-K "UID_MIN=$PUID")
  fi

  if [[ "$PUID" -gt "$uid_max" ]]; then
    useradd_args+=(-K "UID_MAX=$PUID")
  fi

  useradd "${useradd_args[@]}" "$desired_user_name"
fi

ensure_merged_root_ownership() {
  chown "$PUID:$PGID" "$MERGED_ROOT_PATH"

  local merged_child_path
  for merged_child_path in "$MERGED_ROOT_PATH"/*; do
    if [[ ! -e "$merged_child_path" ]]; then
      break
    fi

    if [[ ! -d "$merged_child_path" ]]; then
      continue
    fi

    local current_owner
    current_owner="$(stat -c '%u:%g' "$merged_child_path" 2>/dev/null || true)"
    if [[ "$current_owner" = "$PUID:$PGID" ]]; then
      continue
    fi

    local child_chown_error
    if ! child_chown_error="$(chown "$PUID:$PGID" "$merged_child_path" 2>&1)"; then
      entrypoint_log "WARN: Failed to chown existing merged child '$merged_child_path' to '$PUID:$PGID'. Continuing without recursive repair. Detail: $child_chown_error"
    fi
  done
}

ensure_entrypoint_log_file_ownership() {
  if [[ -z "$ENTRYPOINT_LOG_FILE" || ! -e "$ENTRYPOINT_LOG_FILE" ]]; then
    return
  fi

  chown "$PUID:$PGID" "$ENTRYPOINT_LOG_FILE" >/dev/null 2>&1 || true
}

mkdir -p /ssm/config /ssm/state "$MERGED_ROOT_PATH"
chown -R "$PUID:$PGID" /ssm/config /ssm/state
# Only chown the merged root itself (not recursive) so stale FUSE mountpoints beneath
# $MERGED_ROOT_PATH does not hard-fail container startup with transport-endpoint errors.
ensure_merged_root_ownership
resolve_runtime_gosu_identity
ensure_runtime_fuse_device_access

if [[ -d /ssm/mock-bin ]]; then
  chmod +x /ssm/mock-bin/* >/dev/null 2>&1 || true
fi

if [[ "$#" -eq 0 ]]; then
  set -- dotnet /app/SuwayomiSourceMerge.dll
fi

ensure_entrypoint_log_file_ownership
exec gosu "$RUNTIME_GOSU_IDENTITY" "$@"
