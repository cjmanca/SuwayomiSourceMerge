#!/usr/bin/env bash
set -euo pipefail

DEFAULT_MERGED_ROOT="/mnt/cache/appdata/ssm/merged"
DEFAULT_FUSE_CONF_PATH="/etc/fuse.conf"
DEFAULT_SECCOMP_DEST="/etc/docker/seccomp/ssm-mergerfs.json"
DEFAULT_APPARMOR_DEST="/etc/apparmor.d/ssm-mergerfs"
APPARMOR_PROFILE_NAME="ssm-mergerfs"
DEFAULT_PROFILE_BASE_URL="https://raw.githubusercontent.com/cjmanca/SuwayomiSourceMerge/main/docker/security"
CHECKSUM_MANIFEST_NAME="checksums.sha256"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SECCOMP_SOURCE_PATH="$REPO_ROOT/docker/security/seccomp-mergerfs.json"
APPARMOR_SOURCE_PATH="$REPO_ROOT/docker/security/apparmor/ssm-mergerfs"
CHECKSUM_MANIFEST_SOURCE_PATH="$REPO_ROOT/docker/security/$CHECKSUM_MANIFEST_NAME"
PROFILE_BASE_URL="$DEFAULT_PROFILE_BASE_URL"
PROFILE_CHECKSUM_URL="$DEFAULT_PROFILE_BASE_URL/$CHECKSUM_MANIFEST_NAME"

MERGED_ROOT="$DEFAULT_MERGED_ROOT"
FUSE_CONF_PATH="$DEFAULT_FUSE_CONF_PATH"
SECCOMP_DEST="$DEFAULT_SECCOMP_DEST"
APPARMOR_DEST="$DEFAULT_APPARMOR_DEST"
CHECKSUM_MANIFEST_PATH=""
CHECKSUM_MANIFEST_TEMP_PATH=""

cleanup_temp_artifacts() {
  if [[ -n "$CHECKSUM_MANIFEST_TEMP_PATH" ]]; then
    rm -f "$CHECKSUM_MANIFEST_TEMP_PATH"
  fi
}

trap cleanup_temp_artifacts EXIT

usage() {
  cat <<EOF
Usage: $(basename "$0") [options]

Prepares host state for hardened SSM runtime:
  - merged bind propagation (mount --bind, --make-private, --make-rshared)
  - host fuse.conf user_allow_other entry
  - seccomp profile install
  - AppArmor profile install/load (when available)

Options:
  --merged-root PATH    Merged host path (default: $DEFAULT_MERGED_ROOT)
  --fuse-conf PATH      fuse.conf path (default: $DEFAULT_FUSE_CONF_PATH)
  --seccomp-dest PATH   Seccomp destination (default: $DEFAULT_SECCOMP_DEST)
  --apparmor-dest PATH  AppArmor destination (default: $DEFAULT_APPARMOR_DEST)
  --profile-base-url URL  Remote profile base URL (default: $DEFAULT_PROFILE_BASE_URL)
  --profile-checksum-url URL  Remote checksum manifest URL (default: $DEFAULT_PROFILE_BASE_URL/$CHECKSUM_MANIFEST_NAME)
  --help                Show this help
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --merged-root)
      MERGED_ROOT="$2"
      shift 2
      ;;
    --fuse-conf)
      FUSE_CONF_PATH="$2"
      shift 2
      ;;
    --seccomp-dest)
      SECCOMP_DEST="$2"
      shift 2
      ;;
    --apparmor-dest)
      APPARMOR_DEST="$2"
      shift 2
      ;;
    --profile-base-url)
      PROFILE_BASE_URL="$2"
      PROFILE_CHECKSUM_URL="$PROFILE_BASE_URL/$CHECKSUM_MANIFEST_NAME"
      shift 2
      ;;
    --profile-checksum-url)
      PROFILE_CHECKSUM_URL="$2"
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

if [[ "${EUID}" -ne 0 ]]; then
  echo "This script must run as root." >&2
  exit 1
fi

if [[ ! -f "$SECCOMP_SOURCE_PATH" || ! -f "$APPARMOR_SOURCE_PATH" || ! -f "$CHECKSUM_MANIFEST_SOURCE_PATH" ]]; then
  if ! command -v curl >/dev/null 2>&1; then
    echo "curl is required when profile files or checksum manifest are not present locally." >&2
    exit 1
  fi
fi

ensure_sha256_tooling() {
  if command -v sha256sum >/dev/null 2>&1; then
    return
  fi

  if command -v shasum >/dev/null 2>&1; then
    return
  fi

  echo "A SHA-256 tool is required (install 'sha256sum' or 'shasum')." >&2
  exit 1
}

compute_sha256() {
  local file_path="$1"

  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file_path" | awk '{ print $1 }'
    return
  fi

  shasum -a 256 "$file_path" | awk '{ print $1 }'
}

resolve_checksum_manifest() {
  if [[ -n "$CHECKSUM_MANIFEST_PATH" ]]; then
    return
  fi

  if [[ -f "$CHECKSUM_MANIFEST_SOURCE_PATH" ]]; then
    CHECKSUM_MANIFEST_PATH="$CHECKSUM_MANIFEST_SOURCE_PATH"
    return
  fi

  CHECKSUM_MANIFEST_TEMP_PATH="$(mktemp)"
  if ! curl -fsSL "$PROFILE_CHECKSUM_URL" -o "$CHECKSUM_MANIFEST_TEMP_PATH"; then
    echo "Failed to download checksum manifest from '$PROFILE_CHECKSUM_URL'." >&2
    exit 1
  fi

  CHECKSUM_MANIFEST_PATH="$CHECKSUM_MANIFEST_TEMP_PATH"
}

lookup_expected_checksum() {
  local manifest_key="$1"
  local expected

  expected="$(awk -v key="$manifest_key" '$2 == key { print $1 }' "$CHECKSUM_MANIFEST_PATH" | tail -n 1 | tr '[:upper:]' '[:lower:]')"
  if [[ ! "$expected" =~ ^[a-f0-9]{64}$ ]]; then
    echo "Missing or invalid checksum entry for '$manifest_key' in '$CHECKSUM_MANIFEST_PATH'." >&2
    exit 1
  fi

  printf '%s\n' "$expected"
}

verify_profile_checksum() {
  local file_path="$1"
  local manifest_key="$2"
  local expected
  local actual

  expected="$(lookup_expected_checksum "$manifest_key")"
  actual="$(compute_sha256 "$file_path")"
  if [[ "$actual" != "$expected" ]]; then
    echo "Checksum verification failed for '$manifest_key'." >&2
    echo "Expected: $expected" >&2
    echo "Actual:   $actual" >&2
    exit 1
  fi
}

ensure_user_allow_other() {
  if [[ ! -f "$FUSE_CONF_PATH" ]]; then
    printf 'user_allow_other\n' > "$FUSE_CONF_PATH"
    return
  fi

  if grep -Eq '^[[:space:]]*user_allow_other([[:space:]]*#.*)?[[:space:]]*$' "$FUSE_CONF_PATH"; then
    return
  fi

  printf '\nuser_allow_other\n' >> "$FUSE_CONF_PATH"
}

prepare_merged_propagation() {
  # Keep merged bind setup identical to README host snippet semantics.
  mkdir -p "$MERGED_ROOT"
  mountpoint -q "$MERGED_ROOT" || mount --bind "$MERGED_ROOT" "$MERGED_ROOT"
  mount --make-private "$MERGED_ROOT"
  mount --make-rshared "$MERGED_ROOT"
}

install_seccomp_profile() {
  mkdir -p "$(dirname "$SECCOMP_DEST")"
  local profile_temp_path
  profile_temp_path="$(mktemp)"

  if [[ -f "$SECCOMP_SOURCE_PATH" ]]; then
    cp "$SECCOMP_SOURCE_PATH" "$profile_temp_path"
  else
    curl -fsSL "$PROFILE_BASE_URL/seccomp-mergerfs.json" -o "$profile_temp_path"
  fi

  verify_profile_checksum "$profile_temp_path" "seccomp-mergerfs.json"
  mv "$profile_temp_path" "$SECCOMP_DEST"
  chmod 0644 "$SECCOMP_DEST"
}

install_apparmor_profile() {
  mkdir -p "$(dirname "$APPARMOR_DEST")"
  local profile_temp_path
  profile_temp_path="$(mktemp)"

  if [[ -f "$APPARMOR_SOURCE_PATH" ]]; then
    cp "$APPARMOR_SOURCE_PATH" "$profile_temp_path"
  else
    curl -fsSL "$PROFILE_BASE_URL/apparmor/ssm-mergerfs" -o "$profile_temp_path"
  fi

  verify_profile_checksum "$profile_temp_path" "apparmor/ssm-mergerfs"
  mv "$profile_temp_path" "$APPARMOR_DEST"
  chmod 0644 "$APPARMOR_DEST"
  apparmor_parser -r "$APPARMOR_DEST"
}

print_runtime_snippets() {
  local apparmor_enabled="$1"
  local apparmor_opt
  local seccomp_opt

  if [[ "$apparmor_enabled" = "1" ]]; then
    apparmor_opt="apparmor=$APPARMOR_PROFILE_NAME"
    seccomp_opt="seccomp=$SECCOMP_DEST"
  else
    apparmor_opt="apparmor=unconfined"
    seccomp_opt="seccomp=unconfined"
  fi

  cat <<EOF

Host preparation complete.

Container runtime flags:
  --device /dev/fuse
  --cap-add SYS_ADMIN
  --security-opt $apparmor_opt
  --security-opt $seccomp_opt
  -e ENTRYPOINT_FUSE_CONF_MODE=host-managed

Compose snippet:
  devices:
    - /dev/fuse:/dev/fuse
  cap_add:
    - SYS_ADMIN
  security_opt:
    - $apparmor_opt
    - $seccomp_opt
  environment:
    ENTRYPOINT_FUSE_CONF_MODE: "host-managed"
EOF

  if [[ "$apparmor_enabled" != "1" ]]; then
    cat <<EOF

WARNING: AppArmor tooling/support was not detected on this host.
Fallback to legacy unconfined flags is required on this host unless AppArmor is enabled later.
EOF
  fi
}

echo "Preparing merged bind propagation on '$MERGED_ROOT'..."
prepare_merged_propagation

echo "Loading security profile checksum manifest..."
ensure_sha256_tooling
resolve_checksum_manifest

echo "Ensuring 'user_allow_other' exists in '$FUSE_CONF_PATH'..."
ensure_user_allow_other

echo "Installing seccomp profile to '$SECCOMP_DEST'..."
install_seccomp_profile

APPARMOR_ENABLED=0
if command -v apparmor_parser >/dev/null 2>&1 && [[ -d /sys/module/apparmor ]]; then
  echo "Installing/loading AppArmor profile '$APPARMOR_PROFILE_NAME'..."
  install_apparmor_profile
  APPARMOR_ENABLED=1
else
  echo "AppArmor tools/support not found; skipping AppArmor install and using legacy fallback flags."
fi

print_runtime_snippets "$APPARMOR_ENABLED"
