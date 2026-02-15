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
  desired_group_name="$DEFAULT_SSM_GROUP"
  default_group_gid="$(getent group "$DEFAULT_SSM_GROUP" | cut -d: -f3 || true)"
  if [ -n "$default_group_gid" ] && [ "$default_group_gid" != "$PGID" ]; then
    desired_group_name="${DEFAULT_SSM_GROUP}-gid-$PGID"
    echo "WARN: Group '$DEFAULT_SSM_GROUP' already maps to GID '$default_group_gid'; using fallback group name '$desired_group_name' for requested PGID '$PGID'." >&2
  fi

  groupadd --gid "$PGID" "$desired_group_name"
elif [ "$existing_group_name" != "$DEFAULT_SSM_GROUP" ]; then
  echo "WARN: PGID '$PGID' already maps to group '$existing_group_name' (expected '$DEFAULT_SSM_GROUP'); continuing with existing group." >&2
fi

if ! getent passwd "$PUID" >/dev/null 2>&1; then
  desired_user_name="$DEFAULT_SSM_USER"
  default_user_uid="$(getent passwd "$DEFAULT_SSM_USER" | cut -d: -f3 || true)"
  if [ -n "$default_user_uid" ] && [ "$default_user_uid" != "$PUID" ]; then
    desired_user_name="${DEFAULT_SSM_USER}-uid-$PUID"
    echo "WARN: User '$DEFAULT_SSM_USER' already maps to UID '$default_user_uid'; using fallback user name '$desired_user_name' for requested PUID '$PUID'." >&2
  fi

  useradd --uid "$PUID" --gid "$PGID" --no-create-home --shell /usr/sbin/nologin "$desired_user_name"
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
