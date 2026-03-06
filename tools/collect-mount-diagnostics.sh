#!/usr/bin/env bash
set -u
set -o pipefail

SCRIPT_NAME="$(basename "$0")"
DEFAULT_CONTAINER="SuwayomiSourceMerge"
DEFAULT_HOST_MERGED="/mnt/cache/appdata/ssm/merged"
DEFAULT_CONTAINER_MERGED="/ssm/merged"
DEFAULT_SAMPLE_SECONDS=20
DEFAULT_SAMPLE_INTERVAL=2

CONTAINER_NAME="$DEFAULT_CONTAINER"
HOST_MERGED="$DEFAULT_HOST_MERGED"
CONTAINER_MERGED="$DEFAULT_CONTAINER_MERGED"
SAMPLE_SECONDS="$DEFAULT_SAMPLE_SECONDS"
SAMPLE_INTERVAL="$DEFAULT_SAMPLE_INTERVAL"
OUTPUT_FILE=""

DOCKER_AVAILABLE="false"
CONTAINER_EXISTS="false"
CONTAINER_RUNNING="false"
CONTAINER_ID=""
CONTAINER_PID=""

print_usage() {
	cat <<USAGE
Usage: $SCRIPT_NAME [options]

Options:
  --container <name>          Container name (default: $DEFAULT_CONTAINER)
  --host-merged <path>        Host merged root (default: $DEFAULT_HOST_MERGED)
  --container-merged <path>   Container merged root (default: $DEFAULT_CONTAINER_MERGED)
  --output <file>             Output report path (default: ./ssm_mount_diagnostics_<timestamp>.log)
  --sample-seconds <int>      Time-series sample duration in seconds (default: $DEFAULT_SAMPLE_SECONDS)
  --sample-interval <int>     Time-series sample interval in seconds (default: $DEFAULT_SAMPLE_INTERVAL)
  --help                      Show this help
USAGE
}

normalize_path() {
	local input_path="$1"
	if [[ "$input_path" == "/" ]]; then
		printf '/\n'
		return
	fi

	while [[ "$input_path" == */ ]]; do
		input_path="${input_path%/}"
	done

	if [[ -z "$input_path" ]]; then
		printf '/\n'
		return
	fi

	printf '%s\n' "$input_path"
}

is_non_negative_integer() {
	local value="$1"
	[[ "$value" =~ ^[0-9]+$ ]]
}

is_positive_integer() {
	local value="$1"
	[[ "$value" =~ ^[0-9]+$ ]] && [[ "$value" -gt 0 ]]
}

while [[ $# -gt 0 ]]; do
	case "$1" in
		--container)
			CONTAINER_NAME="${2-}"
			shift 2
			;;
		--host-merged)
			HOST_MERGED="${2-}"
			shift 2
			;;
		--container-merged)
			CONTAINER_MERGED="${2-}"
			shift 2
			;;
		--output)
			OUTPUT_FILE="${2-}"
			shift 2
			;;
		--sample-seconds)
			SAMPLE_SECONDS="${2-}"
			shift 2
			;;
		--sample-interval)
			SAMPLE_INTERVAL="${2-}"
			shift 2
			;;
		--help)
			print_usage
			exit 0
			;;
		*)
			echo "Unknown argument: $1" >&2
			print_usage >&2
			exit 1
			;;
	esac
done

if [[ -z "$CONTAINER_NAME" ]]; then
	echo "Container name cannot be empty." >&2
	exit 1
fi

if [[ -z "$HOST_MERGED" ]]; then
	echo "Host merged path cannot be empty." >&2
	exit 1
fi

if [[ -z "$CONTAINER_MERGED" ]]; then
	echo "Container merged path cannot be empty." >&2
	exit 1
fi

if ! is_non_negative_integer "$SAMPLE_SECONDS"; then
	echo "--sample-seconds must be a non-negative integer." >&2
	exit 1
fi

if ! is_positive_integer "$SAMPLE_INTERVAL"; then
	echo "--sample-interval must be a positive integer." >&2
	exit 1
fi

HOST_MERGED="$(normalize_path "$HOST_MERGED")"
CONTAINER_MERGED="$(normalize_path "$CONTAINER_MERGED")"

if [[ -z "$OUTPUT_FILE" ]]; then
	timestamp="$(date +%Y%m%d_%H%M%S)"
	OUTPUT_FILE="./ssm_mount_diagnostics_${timestamp}.log"
fi

OUTPUT_DIR="$(dirname "$OUTPUT_FILE")"
mkdir -p "$OUTPUT_DIR"
: > "$OUTPUT_FILE"

log_line() {
	printf '%s\n' "$1" >> "$OUTPUT_FILE"
}

log_section() {
	log_line ""
	log_line "================================================================================"
	log_line "$1"
	log_line "================================================================================"
}

run_block() {
	local title="$1"
	local script_content
	script_content="$(cat)"

	log_section "$title"
	log_line "[timestamp] $(date -Iseconds)"
	log_line "[script]"
	log_line "$script_content"
	log_line "[output]"

	bash -o pipefail -c "$script_content" >> "$OUTPUT_FILE" 2>&1
	local rc=$?
	log_line "[exit_code] $rc"
	log_line ""
}

compute_top_duplicate_targets_host() {
	if ! command -v findmnt >/dev/null 2>&1; then
		return
	fi

	mapfile -t TOP_DUPLICATE_TARGETS_HOST < <(
		findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS 2>/dev/null |
		awk -v root="$HOST_MERGED" '
			function normalize_root(v) {
				if (v == "/") {
					return "/";
				}
				sub(/\/+$/, "", v);
				if (v == "") {
					return "/";
				}
				return v;
			}
			function is_under(path, root_path) {
				if (root_path == "/") {
					return substr(path, 1, 1) == "/";
				}
				if (path == root_path) {
					return 1;
				}
				return index(path, root_path "/") == 1;
			}
			BEGIN {
				root_norm = normalize_root(root);
			}
			match($0, /TARGET="([^"]+)"/, targetMatch) {
				target = targetMatch[1];
				if (is_under(target, root_norm)) {
					print target;
				}
			}
		' |
		sort |
		uniq -c |
		sort -nr |
		awk '$1 > 1 { $1 = ""; sub(/^ +/, "", $0); print }' |
		head -n 10
	)
}

compute_sampling_iterations() {
	local total_seconds="$1"
	local interval_seconds="$2"

	if [[ "$total_seconds" -eq 0 ]]; then
		printf '1\n'
		return
	fi

	local iterations=$(( total_seconds / interval_seconds ))
	if (( total_seconds % interval_seconds != 0 )); then
		iterations=$(( iterations + 1 ))
	fi

	if (( iterations < 1 )); then
		iterations=1
	fi

	printf '%s\n' "$iterations"
}

capture_host_holder_checks() {
	log_section "Host Holder Checks For Top Duplicate Targets"
	if [[ ${#TOP_DUPLICATE_TARGETS_HOST[@]} -eq 0 ]]; then
		log_line "No duplicated host targets under '$HOST_MERGED' were detected, or findmnt was unavailable."
		return
	fi

	for target in "${TOP_DUPLICATE_TARGETS_HOST[@]}"; do
		log_line ""
		log_line "--- holder target: $target ---"

		if command -v fuser >/dev/null 2>&1; then
			log_line "[command] fuser -vm -- $target"
			fuser -vm -- "$target" >> "$OUTPUT_FILE" 2>&1 || true
		else
			log_line "fuser not available on host."
		fi

		if command -v lsof >/dev/null 2>&1; then
			log_line "[command] lsof +f -- $target"
			lsof +f -- "$target" >> "$OUTPUT_FILE" 2>&1 || true
		else
			log_line "lsof not available on host."
		fi
	done
}

capture_host_time_series() {
	local iterations
	iterations="$(compute_sampling_iterations "$SAMPLE_SECONDS" "$SAMPLE_INTERVAL")"

	log_section "Host Duplicate Depth Time Series"
	log_line "[sample_seconds] $SAMPLE_SECONDS"
	log_line "[sample_interval] $SAMPLE_INTERVAL"
	log_line "[iterations] $iterations"

	if ! command -v findmnt >/dev/null 2>&1; then
		log_line "findmnt unavailable on host; skipping host time series."
		return
	fi

	local iteration
	for (( iteration=1; iteration<=iterations; iteration++ )); do
		log_line ""
		log_line "[sample $iteration/$iterations] $(date -Iseconds)"
		findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS 2>> "$OUTPUT_FILE" |
		awk -v root="$HOST_MERGED" '
			function normalize_root(v) {
				if (v == "/") {
					return "/";
				}
				sub(/\/+$/, "", v);
				if (v == "") {
					return "/";
				}
				return v;
			}
			function is_under(path, root_path) {
				if (root_path == "/") {
					return substr(path, 1, 1) == "/";
				}
				if (path == root_path) {
					return 1;
				}
				return index(path, root_path "/") == 1;
			}
			BEGIN {
				root_norm = normalize_root(root);
			}
			match($0, /TARGET="([^"]+)"/, targetMatch) {
				target = targetMatch[1];
				if (is_under(target, root_norm)) {
					print target;
				}
			}
		' |
		sort |
		uniq -c |
		sort -nr |
		head -n 15 >> "$OUTPUT_FILE"

		if (( iteration < iterations )); then
			sleep "$SAMPLE_INTERVAL"
		fi
	done
}

export CONTAINER_NAME HOST_MERGED CONTAINER_MERGED SAMPLE_SECONDS SAMPLE_INTERVAL

if command -v docker >/dev/null 2>&1; then
	DOCKER_AVAILABLE="true"
	if docker inspect "$CONTAINER_NAME" >/dev/null 2>&1; then
		CONTAINER_EXISTS="true"
		CONTAINER_ID="$(docker inspect --format '{{.Id}}' "$CONTAINER_NAME" 2>/dev/null || true)"
		CONTAINER_RUNNING="$(docker inspect --format '{{.State.Running}}' "$CONTAINER_NAME" 2>/dev/null || echo false)"
		if [[ "$CONTAINER_RUNNING" == "true" ]]; then
			CONTAINER_PID="$(docker inspect --format '{{.State.Pid}}' "$CONTAINER_NAME" 2>/dev/null || true)"
		fi
	fi
fi

export DOCKER_AVAILABLE CONTAINER_EXISTS CONTAINER_RUNNING CONTAINER_ID CONTAINER_PID

log_section "Diagnostics Metadata"
log_line "[timestamp] $(date -Iseconds)"
log_line "[script] $SCRIPT_NAME"
log_line "[container] $CONTAINER_NAME"
log_line "[host_merged] $HOST_MERGED"
log_line "[container_merged] $CONTAINER_MERGED"
log_line "[output_file] $OUTPUT_FILE"
log_line "[sample_seconds] $SAMPLE_SECONDS"
log_line "[sample_interval] $SAMPLE_INTERVAL"
log_line "[docker_available] $DOCKER_AVAILABLE"
log_line "[container_exists] $CONTAINER_EXISTS"
log_line "[container_running] $CONTAINER_RUNNING"
log_line "[container_id] ${CONTAINER_ID:-<none>}"
log_line "[container_pid] ${CONTAINER_PID:-<none>}"

run_block "Host Runtime Context" <<'SCRIPT'
set +e
printf 'date: %s\n' "$(date -Iseconds)"
printf 'hostname: %s\n' "$(hostname 2>/dev/null || echo unknown)"
printf 'kernel: %s\n' "$(uname -a)"
printf 'user: %s\n' "$(id 2>/dev/null || echo unknown)"
printf 'cwd: %s\n' "$(pwd)"
printf 'bash: %s\n' "${BASH_VERSION:-unknown}"
printf 'findmnt path: %s\n' "$(command -v findmnt 2>/dev/null || echo missing)"
printf 'docker path: %s\n' "$(command -v docker 2>/dev/null || echo missing)"
printf 'fuser path: %s\n' "$(command -v fuser 2>/dev/null || echo missing)"
printf 'lsof path: %s\n' "$(command -v lsof 2>/dev/null || echo missing)"
SCRIPT

run_block "Docker / Container Context" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]]; then
	echo "docker not available on host."
	exit 0
fi

docker version
echo ""
docker info 2>/dev/null | sed -n '1,120p'
echo ""
docker ps -a --no-trunc

echo ""
echo "container inspect summary:"
docker inspect --format 'name={{.Name}} id={{.Id}} running={{.State.Running}} pid={{.State.Pid}} started={{.State.StartedAt}} finished={{.State.FinishedAt}} restart_count={{.RestartCount}}' "$CONTAINER_NAME" 2>/dev/null || true

if [[ "$CONTAINER_RUNNING" == "true" ]] && [[ -n "$CONTAINER_PID" ]]; then
	echo ""
	echo "host namespace references:"
	ls -l "/proc/$CONTAINER_PID/ns/mnt" 2>/dev/null || true
	ls -l /proc/self/ns/mnt 2>/dev/null || true
	if command -v lsns >/dev/null 2>&1; then
		echo ""
		lsns -t mnt -p "$CONTAINER_PID" || true
	fi
fi
SCRIPT

run_block "Host Mount Snapshot Counts" <<'SCRIPT'
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available on host."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | wc -l
findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | wc -c
SCRIPT

run_block "Host Mount Entries Under Host Merged Root" <<'SCRIPT'
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available on host."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS |
awk -v root="$HOST_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		if (match($0, /TARGET="([^"]+)"/, targetMatch) && is_under(targetMatch[1], root_norm)) {
			print $0;
		}
	}
'
SCRIPT

run_block "Host Duplicate TARGET Depth Summary Under Host Merged Root" <<'SCRIPT'
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available on host."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS |
awk -v root="$HOST_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		if (match($0, /TARGET="([^"]+)"/, targetMatch) && is_under(targetMatch[1], root_norm)) {
			print targetMatch[1];
		}
	}
' |
sort |
uniq -c |
sort -nr |
head -n 80
SCRIPT

run_block "Host Global Duplicate TARGET Depth Summary" <<'SCRIPT'
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available on host."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS |
awk '
	match($0, /TARGET="([^"]+)"/, targetMatch) {
		print targetMatch[1];
	}
' |
sort |
uniq -c |
sort -nr |
head -n 100
SCRIPT

run_block "Host /proc/self/mountinfo Filtered To Host Merged Root" <<'SCRIPT'
set +e
awk -v root="$HOST_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		mountpoint = $5;
		gsub(/\\040/, " ", mountpoint);
		if (is_under(mountpoint, root_norm)) {
			print $0;
		}
	}
' /proc/self/mountinfo
SCRIPT

run_block "Container Mount + Runtime Context" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]]; then
	echo "docker not available on host; skipping container context."
	exit 0
fi
if [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container '$CONTAINER_NAME' is not running; skipping docker exec context."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<'INNER'
set +e
printf 'container date: %s\n' "$(date -Iseconds 2>/dev/null || date)"
printf 'container hostname: %s\n' "$(hostname 2>/dev/null || echo unknown)"
printf 'container id: %s\n' "$(id 2>/dev/null || echo unknown)"
printf 'container mnt ns: ' && ls -l /proc/self/ns/mnt 2>/dev/null || true
printf 'findmnt path: %s\n' "$(command -v findmnt 2>/dev/null || echo missing)"
printf 'fuser path: %s\n' "$(command -v fuser 2>/dev/null || echo missing)"
printf 'lsof path: %s\n' "$(command -v lsof 2>/dev/null || echo missing)"
INNER
SCRIPT

run_block "Container Mount Snapshot Counts" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping container mount counts."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<'INNER'
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available in container."
	exit 0
fi
findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | wc -l
findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | wc -c
INNER
SCRIPT

run_block "Container Mount Entries Under Container Merged Root" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping container merged-root view."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<INNER
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available in container."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | awk -v root="$CONTAINER_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		if (match(\$0, /TARGET="([^"]+)"/, targetMatch) && is_under(targetMatch[1], root_norm)) {
			print \$0;
		}
	}
'
INNER
SCRIPT

run_block "Container Duplicate TARGET Depth Summary Under Container Merged Root" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping container duplicate summary."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<INNER
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available in container."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | awk -v root="$CONTAINER_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		if (match(\$0, /TARGET="([^"]+)"/, targetMatch) && is_under(targetMatch[1], root_norm)) {
			print targetMatch[1];
		}
	}
' | sort | uniq -c | sort -nr | head -n 80
INNER
SCRIPT

run_block "Container /proc/self/mountinfo Filtered To Container Merged Root" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping container mountinfo filter."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<INNER
set +e
awk -v root="$CONTAINER_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		mountpoint = \$5;
		gsub(/\\040/, " ", mountpoint);
		if (is_under(mountpoint, root_norm)) {
			print \$0;
		}
	}
' /proc/self/mountinfo
INNER
SCRIPT

run_block "Container Process List" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping container process list."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<'INNER'
set +e
ps -eo pid,ppid,user,args 2>/dev/null || ps -ef 2>/dev/null || true
INNER
SCRIPT

run_block "Container settings.yml Mount/Cleanup Excerpt" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping settings excerpt."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<'INNER'
set +e
if [[ ! -f /ssm/config/settings.yml ]]; then
	echo "/ssm/config/settings.yml not found."
	exit 0
fi

awk '
	BEGIN {
		in_runtime = 0;
		in_shutdown = 0;
	}
	/^[^[:space:]]/ {
		in_runtime = ($0 ~ /^runtime:/);
		in_shutdown = ($0 ~ /^shutdown:/);
	}
	in_runtime || in_shutdown {
		print NR ":" $0;
	}
' /ssm/config/settings.yml
INNER
SCRIPT

run_block "Container daemon.log Tail + Summary Counters" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping daemon log diagnostics."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<'INNER'
set +e
if [[ ! -f /ssm/config/daemon.log ]]; then
	echo "/ssm/config/daemon.log not found."
	exit 0
fi

echo "counter cleanup_max_iteration=$(grep -c "maximum cleanup iteration limit" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter still_mounted_managed=$(grep -c "still_mounted_managed_mounts=" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter unresolved_unmapped=$(grep -c "unresolved_unmapped_branch_count=" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter stale_unmount_reason=$(grep -c "reason=\"StaleMount\"" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter snapshot_degraded=$(grep -c "snapshot_degraded=\"true\"" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter mount_snap_001=$(grep -c "MOUNT-SNAP-001" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter mount_snap_002=$(grep -c "MOUNT-SNAP-002" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter enotconn=$(grep -ci "Transport endpoint is not connected" /ssm/config/daemon.log 2>/dev/null || true)"
echo "counter action_fail_fast=$(grep -c "merge.workflow.action_fail_fast" /ssm/config/daemon.log 2>/dev/null || true)"

echo ""
echo "top repeated stale unmount action mountpoints:"
awk '
	match($0, /event="merge.workflow.action"/) &&
	match($0, /kind="Unmount"/) &&
	match($0, /reason="StaleMount"/) &&
	match($0, /mountpoint="([^"]+)"/, m) {
		print m[1];
	}
' /ssm/config/daemon.log |
sort |
uniq -c |
sort -nr |
head -n 60

echo ""
echo "recent daemon.log tail (last 300 lines):"
tail -n 300 /ssm/config/daemon.log
INNER
SCRIPT

compute_top_duplicate_targets_host
capture_host_holder_checks

run_block "Container Holder Checks For Top Duplicate Targets" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping container holder checks."
	exit 0
fi

docker exec -i "$CONTAINER_NAME" sh <<INNER
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available in container."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | awk -v root="$CONTAINER_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		if (match(\$0, /TARGET="([^"]+)"/, targetMatch) && is_under(targetMatch[1], root_norm)) {
			print targetMatch[1];
		}
	}
' | sort | uniq -c | sort -nr | awk '\$1 > 1 {\$1=""; sub(/^ +/, "", \$0); print}' | head -n 10 > /tmp/ssm_diag_targets.txt

if [[ ! -s /tmp/ssm_diag_targets.txt ]]; then
	echo "No duplicated container targets under $CONTAINER_MERGED were detected."
	rm -f /tmp/ssm_diag_targets.txt
	exit 0
fi

while IFS= read -r target; do
	echo "--- holder target: $target ---"
	if command -v fuser >/dev/null 2>&1; then
		fuser -vm -- "$target" 2>&1 || true
	else
		echo "fuser not available in container."
	fi

	if command -v lsof >/dev/null 2>&1; then
		lsof +f -- "$target" 2>&1 || true
	else
		echo "lsof not available in container."
	fi
done < /tmp/ssm_diag_targets.txt

rm -f /tmp/ssm_diag_targets.txt
INNER
SCRIPT

capture_host_time_series

run_block "Container Duplicate Depth Time Series" <<'SCRIPT'
set +e
if [[ "$DOCKER_AVAILABLE" != "true" ]] || [[ "$CONTAINER_RUNNING" != "true" ]]; then
	echo "container not running or docker unavailable; skipping container time series."
	exit 0
fi

iterations=1
if [[ "$SAMPLE_SECONDS" -gt 0 ]]; then
	iterations=$(( SAMPLE_SECONDS / SAMPLE_INTERVAL ))
	if (( SAMPLE_SECONDS % SAMPLE_INTERVAL != 0 )); then
		iterations=$(( iterations + 1 ))
	fi
	if (( iterations < 1 )); then
		iterations=1
	fi
fi

echo "sample_seconds=$SAMPLE_SECONDS"
echo "sample_interval=$SAMPLE_INTERVAL"
echo "iterations=$iterations"

sample=1
while [[ "$sample" -le "$iterations" ]]; do
	echo ""
	echo "[sample $sample/$iterations] $(date -Iseconds)"
	docker exec -i "$CONTAINER_NAME" sh <<INNER
set +e
if ! command -v findmnt >/dev/null 2>&1; then
	echo "findmnt not available in container."
	exit 0
fi

findmnt -n -P -o TARGET,FSTYPE,SOURCE,OPTIONS | awk -v root="$CONTAINER_MERGED" '
	function normalize_root(v) {
		if (v == "/") {
			return "/";
		}
		sub(/\/+$/, "", v);
		if (v == "") {
			return "/";
		}
		return v;
	}
	function is_under(path, root_path) {
		if (root_path == "/") {
			return substr(path, 1, 1) == "/";
		}
		if (path == root_path) {
			return 1;
		}
		return index(path, root_path "/") == 1;
	}
	BEGIN {
		root_norm = normalize_root(root);
	}
	{
		if (match(\$0, /TARGET="([^"]+)"/, targetMatch) && is_under(targetMatch[1], root_norm)) {
			print targetMatch[1];
		}
	}
' | sort | uniq -c | sort -nr | head -n 15
INNER

	if [[ "$sample" -lt "$iterations" ]]; then
		sleep "$SAMPLE_INTERVAL"
	fi
	sample=$(( sample + 1 ))
done
SCRIPT

log_section "Diagnostics Completed"
log_line "Report written to: $OUTPUT_FILE"
log_line "Completed at: $(date -Iseconds)"

echo "Diagnostics report written to: $OUTPUT_FILE"
