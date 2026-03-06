#!/usr/bin/env bash
set -u
set -o pipefail

# -----------------------------
# Defaults
# -----------------------------
CONTAINER="SuwayomiSourceMerge"
HOST_MERGED="/mnt/cache/appdata/ssm/merged"
CONTAINER_MERGED="/ssm/merged"
RESTART_CYCLES=3
RESTART_WAIT=8
DO_RECREATE_EXPERIMENT=0
PROBE_SLEEP=20
PARENT_SOURCES="/tmp/ssm_probe_sources"
PARENT_OVERRIDES="/tmp/ssm_probe_overrides"
OUTPUT=""

usage() {
	cat <<'USAGE'
Usage: ./collect-mount-rca.sh [options]

Options:
  --container NAME               Container name (default: SuwayomiSourceMerge)
  --host-merged PATH             Host merged root (default: /mnt/cache/appdata/ssm/merged)
  --container-merged PATH        Container merged root (default: /ssm/merged)
  --output FILE                  Report output path (default: ./ssm_mount_rca_<timestamp>.log)
  --restart-cycles N             Restart cycles for trend test (default: 3)
  --restart-wait SECONDS         Wait after each restart/probe start (default: 8)
  --do-recreate-experiment       Run probe A/B experiment (mutating; creates temp containers)
  --probe-sleep SECONDS          Probe container sleep duration (default: 20)
  --parent-sources PATH          Parent bind path for /ssm/sources in probe B (default: /tmp/ssm_probe_sources)
  --parent-overrides PATH        Parent bind path for /ssm/override in probe B (default: /tmp/ssm_probe_overrides)
  --help                         Show this help
USAGE
}

is_pos_int() {
	[[ "${1:-}" =~ ^[0-9]+$ ]] && [[ "$1" -gt 0 ]]
}

print_cmd() {
	printf '[cmd]'
	for a in "$@"; do
		printf ' %q' "$a"
	done
	printf '\n' >>"$OUTPUT"
}

run_cmd() {
	print_cmd "$@"
	"$@" >>"$OUTPUT" 2>&1
	local rc=$?
	printf '[rc] %s\n\n' "$rc" >>"$OUTPUT"
	return "$rc"
}

run_sh() {
	local cmd="$1"
	printf '[cmd] bash -lc %q\n' "$cmd" >>"$OUTPUT"
	bash -lc "$cmd" >>"$OUTPUT" 2>&1
	local rc=$?
	printf '[rc] %s\n\n' "$rc" >>"$OUTPUT"
	return "$rc"
}

log() {
	printf '%s\n' "$1" >>"$OUTPUT"
}

section() {
	log ""
	log "================================================================================"
	log "$1"
	log "================================================================================"
}

# -----------------------------
# Parse args
# -----------------------------
while [[ $# -gt 0 ]]; do
	case "$1" in
		--container) CONTAINER="${2:-}"; shift 2 ;;
		--host-merged) HOST_MERGED="${2:-}"; shift 2 ;;
		--container-merged) CONTAINER_MERGED="${2:-}"; shift 2 ;;
		--output) OUTPUT="${2:-}"; shift 2 ;;
		--restart-cycles) RESTART_CYCLES="${2:-}"; shift 2 ;;
		--restart-wait) RESTART_WAIT="${2:-}"; shift 2 ;;
		--do-recreate-experiment) DO_RECREATE_EXPERIMENT=1; shift 1 ;;
		--probe-sleep) PROBE_SLEEP="${2:-}"; shift 2 ;;
		--parent-sources) PARENT_SOURCES="${2:-}"; shift 2 ;;
		--parent-overrides) PARENT_OVERRIDES="${2:-}"; shift 2 ;;
		--help) usage; exit 0 ;;
		*) echo "Unknown argument: $1" >&2; usage >&2; exit 1 ;;
	esac
done

if ! is_pos_int "$RESTART_CYCLES"; then echo "--restart-cycles must be > 0" >&2; exit 1; fi
if ! is_pos_int "$RESTART_WAIT"; then echo "--restart-wait must be > 0" >&2; exit 1; fi
if ! is_pos_int "$PROBE_SLEEP"; then echo "--probe-sleep must be > 0" >&2; exit 1; fi

if [[ -z "$OUTPUT" ]]; then
	OUTPUT="./ssm_mount_rca_$(date +%Y%m%d_%H%M%S).log"
fi
mkdir -p "$(dirname "$OUTPUT")"
: >"$OUTPUT"

# -----------------------------
# Globals for summary
# -----------------------------
declare -a SNAP_LABELS=()
declare -a SNAP_MOUNTINFO=()
declare -a SNAP_ANON=()
declare -a SNAP_HOST_DUPMAX=()
declare -a SNAP_HOST_MERGED_LINES=()
declare -a SNAP_CONTAINER_MERGED_LINES=()

snapshot() {
	local label="$1"

	section "SNAPSHOT: $label"
	log "[timestamp] $(date -Iseconds)"

	local running="false"
	running="$(docker inspect --format '{{.State.Running}}' "$CONTAINER" 2>/dev/null || echo false)"

	local mountinfo_lines anon_lines host_merged_lines container_merged_lines
	local host_dup_max

	mountinfo_lines="$(wc -l < /proc/1/mountinfo 2>/dev/null || echo -1)"
	anon_lines="$(grep -Ec '/var/lib/docker/volumes/.*/_data/(disk|priority)' /proc/mounts 2>/dev/null || echo -1)"
	host_merged_lines="$(grep -cF " $HOST_MERGED/" /proc/self/mountinfo 2>/dev/null || echo 0)"

	if [[ "$running" == "true" ]]; then
		container_merged_lines="$(docker exec "$CONTAINER" sh -c "grep -cF ' $CONTAINER_MERGED/' /proc/self/mountinfo" 2>/dev/null || echo -1)"
	else
		container_merged_lines="-1"
	fi

	host_dup_max="$(
		findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS 2>/dev/null \
		| grep -F "TARGET=\"$HOST_MERGED/" \
		| sed -E 's/.*TARGET="([^"]+)".*/\1/' \
		| sort | uniq -c | sort -nr \
		| head -n1 | awk '{print $1+0}'
	)"
	[[ -z "$host_dup_max" ]] && host_dup_max=0

	log "[metric] mountinfo_lines=$mountinfo_lines"
	log "[metric] anon_volume_disk_or_priority_lines=$anon_lines"
	log "[metric] host_merged_mountinfo_lines=$host_merged_lines"
	log "[metric] container_merged_mountinfo_lines=$container_merged_lines"
	log "[metric] host_merged_duplicate_max=$host_dup_max"

	SNAP_LABELS+=("$label")
	SNAP_MOUNTINFO+=("$mountinfo_lines")
	SNAP_ANON+=("$anon_lines")
	SNAP_HOST_DUPMAX+=("$host_dup_max")
	SNAP_HOST_MERGED_LINES+=("$host_merged_lines")
	SNAP_CONTAINER_MERGED_LINES+=("$container_merged_lines")

	run_sh "docker inspect --format 'status={{.State.Status}} exit={{.State.ExitCode}} error={{.State.Error}} started={{.State.StartedAt}} finished={{.State.FinishedAt}} pid={{.State.Pid}}' '$CONTAINER'"
	run_sh "docker inspect --format '{{range .Mounts}}{{println .Type \"|\" .Destination \"|\" .Source \"|\" .Propagation}}{{end}}' '$CONTAINER' | sort"

	run_sh "grep -E '/var/lib/docker/volumes/.*/_data/(disk|priority)' /proc/mounts | sort | uniq -c | sort -nr | head -n 40"
	run_sh "findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | grep -F 'TARGET=\"$HOST_MERGED/' | sed -E 's/.*TARGET=\"([^\"]+)\".*/\\1/' | sort | uniq -c | sort -nr | head -n 40"

	run_sh "grep -F ' $HOST_MERGED' /proc/self/mountinfo | head -n 120"
	if [[ "$running" == "true" ]]; then
		run_sh "docker exec '$CONTAINER' sh -c \"grep -F ' $CONTAINER_MERGED' /proc/self/mountinfo | head -n 120\""
	fi
}

declare -a SUBDIR_BINDS=()

collect_subdir_binds() {
	section "COLLECT SUBDIR BINDS"
	SUBDIR_BINDS=()

	mapfile -t rows < <(docker inspect --format '{{range .Mounts}}{{println .Type "|" .Source "|" .Destination "|" .Propagation}}{{end}}' "$CONTAINER" 2>/dev/null || true)

	local row type src dst prop
	for row in "${rows[@]}"; do
		IFS='|' read -r type src dst prop <<<"$row"
		[[ "$type" != "bind" ]] && continue
		case "$dst" in
			/ssm/sources/*|/ssm/override/*)
				[[ -z "$prop" ]] && prop="rprivate"
				SUBDIR_BINDS+=("$src|$dst|$prop")
				;;
		esac
	done

	log "[info] subdir_bind_count=${#SUBDIR_BINDS[@]}"
	for row in "${SUBDIR_BINDS[@]}"; do
		log "[subdir_bind] $row"
	done
}

build_probe_args() {
	local include_parents="$1"
	local entrypoint="$2"
	local image="$3"

	PROBE_ARGS=(docker run -d)
	PROBE_ARGS+=(--entrypoint "$entrypoint")

	if [[ "$include_parents" == "1" ]]; then
		mkdir -p "$PARENT_SOURCES" "$PARENT_OVERRIDES"
		PROBE_ARGS+=(--mount "type=bind,src=$PARENT_SOURCES,dst=/ssm/sources,bind-propagation=rprivate")
		PROBE_ARGS+=(--mount "type=bind,src=$PARENT_OVERRIDES,dst=/ssm/override,bind-propagation=rprivate")
	fi

	local row src dst prop
	for row in "${SUBDIR_BINDS[@]}"; do
		IFS='|' read -r src dst prop <<<"$row"
		PROBE_ARGS+=(--mount "type=bind,src=$src,dst=$dst,bind-propagation=$prop")
	done

	PROBE_ARGS+=("$image" -c "sleep $PROBE_SLEEP")
}

start_probe() {
	local name="$1"
	local include_parents="$2"
	local image="$3"

	local -a PROBE_ARGS=()
	build_probe_args "$include_parents" "/bin/sh" "$image"
	PROBE_ARGS=("${PROBE_ARGS[@]}" --name "$name")
	if run_cmd "${PROBE_ARGS[@]}"; then
		return 0
	fi

	build_probe_args "$include_parents" "/bin/bash" "$image"
	PROBE_ARGS=("${PROBE_ARGS[@]}" --name "$name")
	run_cmd "${PROBE_ARGS[@]}"
}

cleanup_probe() {
	local name="$1"
	run_cmd docker rm -f -v "$name" || true
}

run_restart_loop() {
	section "RESTART LOOP"
	local i
	for (( i=1; i<=RESTART_CYCLES; i++ )); do
		log "[info] restart_cycle=$i/$RESTART_CYCLES"
		run_cmd docker restart "$CONTAINER" || true
		sleep "$RESTART_WAIT"
		snapshot "after_restart_$i"
	done
}

run_recreate_experiment() {
	section "RECREATE-STYLE PROBE EXPERIMENT"

	collect_subdir_binds
	if [[ "${#SUBDIR_BINDS[@]}" -eq 0 ]]; then
		log "[warn] No /ssm/sources/* or /ssm/override/* bind mounts found. Skipping probe experiment."
		return
	fi

	local image
	image="$(docker inspect --format '{{.Config.Image}}' "$CONTAINER" 2>/dev/null || true)"
	if [[ -z "$image" ]]; then
		log "[warn] Unable to resolve image for $CONTAINER. Skipping probe experiment."
		return
	fi
	log "[info] probe_image=$image"

	local ts
	ts="$(date +%Y%m%d_%H%M%S)"
	local probe_a="ssm_probe_a_${ts}"
	local probe_b="ssm_probe_b_${ts}"

	# Probe A: subdir binds only (lets image VOLUME behavior kick in)
	run_cmd docker rm -f -v "$probe_a" || true
	start_probe "$probe_a" "0" "$image" || true
	sleep "$RESTART_WAIT"
	snapshot "probe_a_running_subdir_binds_only"
	cleanup_probe "$probe_a"
	sleep 2
	snapshot "probe_a_removed"

	# Probe B: explicit parent binds + same subdir binds
	run_cmd docker rm -f -v "$probe_b" || true
	start_probe "$probe_b" "1" "$image" || true
	sleep "$RESTART_WAIT"
	snapshot "probe_b_running_with_parent_binds"
	cleanup_probe "$probe_b"
	sleep 2
	snapshot "probe_b_removed"
}

print_summary() {
	section "SUMMARY TABLE"

	log "label|mountinfo_lines|anon_disk_priority_lines|host_dup_max|host_merged_lines|container_merged_lines"
	local i
	for i in "${!SNAP_LABELS[@]}"; do
		log "${SNAP_LABELS[$i]}|${SNAP_MOUNTINFO[$i]}|${SNAP_ANON[$i]}|${SNAP_HOST_DUPMAX[$i]}|${SNAP_HOST_MERGED_LINES[$i]}|${SNAP_CONTAINER_MERGED_LINES[$i]}"
	done

	if [[ "${#SNAP_LABELS[@]}" -ge 2 ]]; then
		local last=$(( ${#SNAP_LABELS[@]} - 1 ))
		local d_mount d_anon d_dup
		d_mount=$(( ${SNAP_MOUNTINFO[$last]} - ${SNAP_MOUNTINFO[0]} ))
		d_anon=$(( ${SNAP_ANON[$last]} - ${SNAP_ANON[0]} ))
		d_dup=$(( ${SNAP_HOST_DUPMAX[$last]} - ${SNAP_HOST_DUPMAX[0]} ))
		log ""
		log "[delta_from_first_to_last] mountinfo_lines=$d_mount anon_disk_priority_lines=$d_anon host_dup_max=$d_dup"
	fi
}

# -----------------------------
# Main
# -----------------------------
section "METADATA"
log "[timestamp] $(date -Iseconds)"
log "[container] $CONTAINER"
log "[host_merged] $HOST_MERGED"
log "[container_merged] $CONTAINER_MERGED"
log "[restart_cycles] $RESTART_CYCLES"
log "[restart_wait] $RESTART_WAIT"
log "[do_recreate_experiment] $DO_RECREATE_EXPERIMENT"
log "[probe_sleep] $PROBE_SLEEP"
log "[parent_sources] $PARENT_SOURCES"
log "[parent_overrides] $PARENT_OVERRIDES"
log "[output] $OUTPUT"

if ! command -v docker >/dev/null 2>&1; then
	log "[fatal] docker command not found"
	echo "Docker not found; see $OUTPUT"
	exit 1
fi

if ! docker inspect "$CONTAINER" >/dev/null 2>&1; then
	log "[fatal] container not found: $CONTAINER"
	echo "Container not found; see $OUTPUT"
	exit 1
fi

run_cmd docker version || true
snapshot "baseline"

run_restart_loop

if [[ "$DO_RECREATE_EXPERIMENT" -eq 1 ]]; then
	run_recreate_experiment
else
	section "RECREATE-STYLE PROBE EXPERIMENT"
	log "[info] Skipped. Use --do-recreate-experiment to include it."
fi

snapshot "final"
print_summary

echo "Done. Report written to: $OUTPUT"
