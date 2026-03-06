#!/usr/bin/env bash
set -u
set -o pipefail
IFS=$'\n\t'

CONTAINER="SuwayomiSourceMerge"
HOST_MERGED="/mnt/cache/appdata/ssm/merged"
CONTAINER_MERGED="/ssm/merged"
PROBE_IMAGE="alpine:3.20"
STARTUP_WAIT=45
FINAL_MODE="legacy" # legacy | isolated | none
OUTPUT="./ssm_propagation_ab_$(date +%Y%m%d_%H%M%S).log"

# Snapshot result placeholders
LEGACY_HOST_CHILDREN=""
LEGACY_HOST_DUP_MAX=""
LEGACY_CONTAINER_CHILDREN=""
LEGACY_CONTAINER_DUP_MAX=""
LEGACY_PROBE_CHILDREN=""
LEGACY_PROBE_DUP_MAX=""

ISOLATED_HOST_CHILDREN=""
ISOLATED_HOST_DUP_MAX=""
ISOLATED_CONTAINER_CHILDREN=""
ISOLATED_CONTAINER_DUP_MAX=""
ISOLATED_PROBE_CHILDREN=""
ISOLATED_PROBE_DUP_MAX=""

usage()
{
  cat <<USAGE
Usage: $(basename "$0") [options]

A/B test script for merged-root propagation topology.
It compares:
  1) legacy:   bind + rshared
  2) isolated: bind + private + rshared

Options:
  --container NAME            Container name (default: ${CONTAINER})
  --host-merged PATH          Host merged root (default: ${HOST_MERGED})
  --container-merged PATH     Container merged root (default: ${CONTAINER_MERGED})
  --output PATH               Output log path (default: ${OUTPUT})
  --startup-wait SECONDS      Wait for mounts after start (default: ${STARTUP_WAIT})
  --probe-image IMAGE         Probe image (default: ${PROBE_IMAGE})
  --final-mode MODE           Final mode after A/B: legacy|isolated|none (default: ${FINAL_MODE})
  --help                      Show this help
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --container)
      CONTAINER="$2"
      shift 2
      ;;
    --host-merged)
      HOST_MERGED="$2"
      shift 2
      ;;
    --container-merged)
      CONTAINER_MERGED="$2"
      shift 2
      ;;
    --output)
      OUTPUT="$2"
      shift 2
      ;;
    --startup-wait)
      STARTUP_WAIT="$2"
      shift 2
      ;;
    --probe-image)
      PROBE_IMAGE="$2"
      shift 2
      ;;
    --final-mode)
      FINAL_MODE="$2"
      shift 2
      ;;
    --help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ "${FINAL_MODE}" != "legacy" && "${FINAL_MODE}" != "isolated" && "${FINAL_MODE}" != "none" ]]; then
  echo "--final-mode must be one of: legacy, isolated, none" >&2
  exit 1
fi

if [[ "${EUID}" -ne 0 ]]; then
  echo "This script must run as root (it changes mount propagation)." >&2
  exit 1
fi

need_cmd()
{
  local name="$1"
  if ! command -v "$name" >/dev/null 2>&1; then
    echo "Missing required command: $name" >&2
    exit 1
  fi
}

need_cmd docker
need_cmd mount
need_cmd umount
need_cmd mountpoint
need_cmd findmnt
need_cmd awk
need_cmd grep
need_cmd sed
need_cmd sort
need_cmd uniq
need_cmd wc

mkdir -p "$(dirname "$OUTPUT")"
: > "$OUTPUT"

log()
{
  printf '%s\n' "$*" | tee -a "$OUTPUT" >/dev/null
}

section()
{
  log ""
  log "================================================================================"
  log "$1"
  log "================================================================================"
}

run_cmd()
{
  local rendered
  printf -v rendered '%q ' "$@"
  log "[cmd] ${rendered% }"
  "$@" >>"$OUTPUT" 2>&1
  local rc=$?
  log "[rc] ${rc}"
  log ""
  return $rc
}

run_sh()
{
  local cmd="$1"
  log "[cmd] bash -lc $cmd"
  bash -lc "$cmd" >>"$OUTPUT" 2>&1
  local rc=$?
  log "[rc] ${rc}"
  log ""
  return $rc
}

container_exists()
{
  docker inspect "$CONTAINER" >/dev/null 2>&1
}

container_running()
{
  local running
  running="$(docker inspect --format '{{.State.Running}}' "$CONTAINER" 2>/dev/null || true)"
  [[ "$running" == "true" ]]
}

host_child_count()
{
  findmnt -n -P -o TARGET 2>/dev/null \
    | grep -F "TARGET=\"${HOST_MERGED}/" \
    | wc -l \
    | awk '{print $1+0}'
}

host_dup_max()
{
  local v
  v="$(findmnt -n -P -o TARGET 2>/dev/null \
    | grep -F "TARGET=\"${HOST_MERGED}/" \
    | sed -E 's/.*TARGET="([^"]+)".*/\1/' \
    | sort \
    | uniq -c \
    | awk 'max<$1{max=$1} END{print max+0}')"
  if [[ -z "$v" ]]; then
    echo 0
    return 0
  fi

  echo "$v"
}

container_child_count()
{
  if ! container_running; then
    echo -1
    return 0
  fi

  docker exec "$CONTAINER" sh -lc "findmnt -n -P -o TARGET 2>/dev/null | grep -F 'TARGET=\"${CONTAINER_MERGED}/' | wc -l" 2>/dev/null \
    | awk '{print $1+0}'
}

container_dup_max()
{
  if ! container_running; then
    echo -1
    return 0
  fi

  local v
  v="$(docker exec "$CONTAINER" sh -lc "findmnt -n -P -o TARGET 2>/dev/null | grep -F 'TARGET=\"${CONTAINER_MERGED}/' | sed -E 's/.*TARGET=\"([^\"]+)\".*/\\1/' | sort | uniq -c | awk 'max<\$1{max=\$1} END{print max+0}'" 2>/dev/null || true)"

  if [[ -z "$v" ]]; then
    echo 0
    return 0
  fi

  echo "$v"
}

probe_visibility()
{
  local probe_name="ssm-propagation-probe"
  local probe_child="-1"
  local probe_dup="-1"

  docker rm -f "$probe_name" >/dev/null 2>&1 || true

  if docker run -d --rm --name "$probe_name" \
      --mount "type=bind,src=${HOST_MERGED},dst=/probe,bind-propagation=rslave" \
      "$PROBE_IMAGE" sh -lc "sleep 300" >/dev/null 2>&1; then
    sleep 2

    probe_child="$(docker exec "$probe_name" sh -lc "awk '\$5 ~ \"^/probe/\" { c++ } END { print c+0 }' /proc/self/mountinfo" 2>/dev/null || echo -1)"
    probe_dup="$(docker exec "$probe_name" sh -lc "awk '\$5 ~ \"^/probe/\" { d[\$5]++ } END { m=0; for (k in d) if (d[k] > m) m=d[k]; print m+0 }' /proc/self/mountinfo" 2>/dev/null || echo -1)"
  fi

  docker rm -f "$probe_name" >/dev/null 2>&1 || true
  echo "${probe_child}|${probe_dup}"
}

stop_container_if_running()
{
  if container_running; then
    run_cmd docker stop "$CONTAINER" || true
  fi
}

start_container_and_wait()
{
  run_cmd docker start "$CONTAINER" || return 1

  local deadline=$((SECONDS + STARTUP_WAIT))
  local mounted=0

  while [[ $SECONDS -lt $deadline ]]; do
    if container_running; then
      local ccount
      ccount="$(container_child_count)"
      if [[ "$ccount" =~ ^[0-9]+$ ]] && [[ "$ccount" -gt 0 ]]; then
        mounted=1
        break
      fi
    fi

    sleep 2
  done

  if [[ "$mounted" -eq 0 ]]; then
    log "[warn] Container did not show merged child mounts within ${STARTUP_WAIT}s."
  fi

  return 0
}

clear_child_mounts()
{
  local targets
  targets="$(findmnt -R -n -o TARGET "$HOST_MERGED" 2>/dev/null | awk 'NR>1' | sort -r)"

  if [[ -z "$targets" ]]; then
    return 0
  fi

  while IFS= read -r target; do
    [[ -z "$target" ]] && continue
    run_cmd umount -l "$target" || true
  done <<< "$targets"
}

reset_root_bind_layer()
{
  # Remove existing dedicated root bind if present, then recreate a clean one.
  if mountpoint -q "$HOST_MERGED"; then
    run_cmd umount "$HOST_MERGED" || true
  fi

  if ! mountpoint -q "$HOST_MERGED"; then
    run_cmd mount --bind "$HOST_MERGED" "$HOST_MERGED" || return 1
  fi

  return 0
}

apply_mode()
{
  local mode="$1"

  case "$mode" in
    legacy)
      reset_root_bind_layer || return 1
      run_cmd mount --make-rshared "$HOST_MERGED" || return 1
      ;;
    isolated)
      reset_root_bind_layer || return 1
      run_cmd mount --make-private "$HOST_MERGED" || return 1
      run_cmd mount --make-rshared "$HOST_MERGED" || return 1
      ;;
    *)
      log "[error] Unknown mode: $mode"
      return 1
      ;;
  esac

  run_cmd findmnt -o TARGET,PROPAGATION,SOURCE,FSTYPE --target "$HOST_MERGED" || true
  return 0
}

append_summary_row()
{
  local label="$1"
  local mode="$2"
  local host_prop="$3"
  local host_children="$4"
  local host_dup="$5"
  local cont_children="$6"
  local cont_dup="$7"
  local probe_children="$8"
  local probe_dup="$9"

  log "${label}|${mode}|${host_prop}|${host_children}|${host_dup}|${cont_children}|${cont_dup}|${probe_children}|${probe_dup}"
}

snapshot()
{
  local label="$1"
  local mode="$2"

  section "SNAPSHOT: ${label} (${mode})"

  run_cmd date -Is || true
  run_cmd docker inspect --format 'status={{.State.Status}} exit={{.State.ExitCode}} error={{.State.Error}} started={{.State.StartedAt}} finished={{.State.FinishedAt}} pid={{.State.Pid}}' "$CONTAINER" || true
  run_cmd findmnt -o TARGET,PROPAGATION,SOURCE,FSTYPE --target "$HOST_MERGED" || true
  run_sh "grep -F ' ${HOST_MERGED}' /proc/self/mountinfo | head -n 60" || true

  if container_running; then
    run_cmd docker exec "$CONTAINER" sh -lc "findmnt -o TARGET,PROPAGATION,SOURCE,FSTYPE --target '${CONTAINER_MERGED}'" || true
    run_cmd docker exec "$CONTAINER" sh -lc "grep -F ' ${CONTAINER_MERGED}' /proc/self/mountinfo | head -n 60" || true
  fi

  local host_prop
  local host_children
  local host_dup
  local cont_children
  local cont_dup
  local probe_pair
  local probe_children
  local probe_dup

  host_prop="$(findmnt -n -o PROPAGATION --target "$HOST_MERGED" 2>/dev/null | head -n 1 | tr -d '[:space:]')"
  host_prop="${host_prop:-unknown}"
  host_children="$(host_child_count)"
  host_dup="$(host_dup_max)"
  cont_children="$(container_child_count)"
  cont_dup="$(container_dup_max)"
  probe_pair="$(probe_visibility)"
  probe_children="${probe_pair%%|*}"
  probe_dup="${probe_pair##*|}"

  log "[metric] host_prop=${host_prop}"
  log "[metric] host_children=${host_children}"
  log "[metric] host_dup_max=${host_dup}"
  log "[metric] container_children=${cont_children}"
  log "[metric] container_dup_max=${cont_dup}"
  log "[metric] probe_children=${probe_children}"
  log "[metric] probe_dup_max=${probe_dup}"

  append_summary_row "$label" "$mode" "$host_prop" "$host_children" "$host_dup" "$cont_children" "$cont_dup" "$probe_children" "$probe_dup"

  if [[ "$mode" == "legacy" ]]; then
    LEGACY_HOST_CHILDREN="$host_children"
    LEGACY_HOST_DUP_MAX="$host_dup"
    LEGACY_CONTAINER_CHILDREN="$cont_children"
    LEGACY_CONTAINER_DUP_MAX="$cont_dup"
    LEGACY_PROBE_CHILDREN="$probe_children"
    LEGACY_PROBE_DUP_MAX="$probe_dup"
  elif [[ "$mode" == "isolated" ]]; then
    ISOLATED_HOST_CHILDREN="$host_children"
    ISOLATED_HOST_DUP_MAX="$host_dup"
    ISOLATED_CONTAINER_CHILDREN="$cont_children"
    ISOLATED_CONTAINER_DUP_MAX="$cont_dup"
    ISOLATED_PROBE_CHILDREN="$probe_children"
    ISOLATED_PROBE_DUP_MAX="$probe_dup"
  fi
}

recommend_mode()
{
  local recommended="legacy"

  if [[ "$ISOLATED_HOST_DUP_MAX" =~ ^[0-9]+$ ]] \
    && [[ "$LEGACY_HOST_DUP_MAX" =~ ^[0-9]+$ ]] \
    && [[ "$ISOLATED_CONTAINER_CHILDREN" =~ ^[0-9]+$ ]] \
    && [[ "$ISOLATED_PROBE_CHILDREN" =~ ^[0-9]+$ ]]; then

    if [[ "$ISOLATED_HOST_DUP_MAX" -lt "$LEGACY_HOST_DUP_MAX" ]] \
      && [[ "$ISOLATED_CONTAINER_CHILDREN" -gt 0 ]] \
      && [[ "$ISOLATED_PROBE_CHILDREN" -gt 0 ]]; then
      recommended="isolated"
    fi
  fi

  echo "$recommended"
}

if ! container_exists; then
  echo "Container not found: ${CONTAINER}" >&2
  exit 1
fi

section "METADATA"
log "[timestamp] $(date -Is)"
log "[container] ${CONTAINER}"
log "[host_merged] ${HOST_MERGED}"
log "[container_merged] ${CONTAINER_MERGED}"
log "[probe_image] ${PROBE_IMAGE}"
log "[startup_wait] ${STARTUP_WAIT}"
log "[final_mode] ${FINAL_MODE}"
log "[output] ${OUTPUT}"
run_cmd docker version || true

INITIAL_RUNNING=0
if container_running; then
  INITIAL_RUNNING=1
fi
log "[initial_container_running] ${INITIAL_RUNNING}"

section "SUMMARY TABLE"
log "label|mode|host_prop|host_children|host_dup_max|container_children|container_dup_max|probe_children|probe_dup_max"

snapshot "baseline" "current"

section "EXPERIMENT: legacy"
stop_container_if_running
clear_child_mounts
apply_mode "legacy"
start_container_and_wait || true
snapshot "after_legacy" "legacy"

section "EXPERIMENT: isolated"
stop_container_if_running
clear_child_mounts
apply_mode "isolated"
start_container_and_wait || true
snapshot "after_isolated" "isolated"

REC_MODE="$(recommend_mode)"
section "RECOMMENDATION"
log "[recommended_mode] ${REC_MODE}"
log "[legacy] host_dup_max=${LEGACY_HOST_DUP_MAX} container_children=${LEGACY_CONTAINER_CHILDREN} probe_children=${LEGACY_PROBE_CHILDREN}"
log "[isolated] host_dup_max=${ISOLATED_HOST_DUP_MAX} container_children=${ISOLATED_CONTAINER_CHILDREN} probe_children=${ISOLATED_PROBE_CHILDREN}"

section "FINAL APPLY"
if [[ "$FINAL_MODE" == "none" ]]; then
  log "[final_apply] skipped (leaving current state from last experiment)."
else
  stop_container_if_running
  clear_child_mounts
  apply_mode "$FINAL_MODE"

  if [[ "$INITIAL_RUNNING" -eq 1 ]]; then
    start_container_and_wait || true
  else
    log "[final_apply] initial container state was stopped; leaving stopped."
  fi

  snapshot "final" "$FINAL_MODE"
fi

section "DONE"
log "Completed. Report written to: ${OUTPUT}"
