#!/usr/bin/env bash
set -euo pipefail

DEFAULT_PUID=99
DEFAULT_PGID=100
DEFAULT_SSM_USER="ssm"
DEFAULT_SSM_GROUP="ssm"

PUID="${PUID:-$DEFAULT_PUID}"
PGID="${PGID:-$DEFAULT_PGID}"

if ! [[ "$PUID" =~ ^[0-9]+$ ]]; then
  echo "Invalid PUID value: '$PUID'. Expected an integer." >&2
  exit 64
fi

if ! [[ "$PGID" =~ ^[0-9]+$ ]]; then
  echo "Invalid PGID value: '$PGID'. Expected an integer." >&2
  exit 64
fi

existing_group_name="$(getent group "$PGID" | cut -d: -f1 || true)"
if [ -z "$existing_group_name" ]; then
  groupadd --gid "$PGID" "$DEFAULT_SSM_GROUP"
elif [ "$existing_group_name" != "$DEFAULT_SSM_GROUP" ]; then
  echo "WARN: PGID '$PGID' already maps to group '$existing_group_name' (expected '$DEFAULT_SSM_GROUP'); continuing with existing group." >&2
fi

if ! getent passwd "$PUID" >/dev/null 2>&1; then
  useradd --uid "$PUID" --gid "$PGID" --no-create-home --shell /usr/sbin/nologin "$DEFAULT_SSM_USER"
fi

mkdir -p /ssm/config /ssm/state /ssm/merged
chown -R "$PUID:$PGID" /ssm/config /ssm/state /ssm/merged

if [ -d /ssm/mock-bin ]; then
  chmod +x /ssm/mock-bin/* >/dev/null 2>&1 || true
fi

if [ "$#" -eq 0 ]; then
  set -- dotnet /app/SuwayomiSourceMerge.dll
fi

exec gosu "$PUID:$PGID" "$@"
