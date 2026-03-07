# SuwayomiSourceMerge

SuwayomiSourceMerge combines manga from multiple download sources into one merged library.
You then point Suwayomi Local Source to that merged library.
This helps you keep reading in one place, even when chapters are split across different sources.
It also supports override folders, so your own `details.json`, `cover.jpg`, or custom files can take priority.
If you set up FlareSolverr in the config, it can generate `details.json`, `cover.jpg` and find alternate titles automatically from comick.dev.

## Before You Start

- This project is Linux-only.
- Most users should run it in Docker. It was designed with Unraid in mind, but should work on any Linux system.
- Create host folders for `config`, `sources`, `override`, `merged`, and `state`.
- If you already used Suwayomi Local Source, move those files into the created override folder before you start.

### Required Mount Rules (Do Not Skip)

- Use direct disk or pool paths like `/mnt/disk*`, `/mnt/cache`, or `/mnt/<pool-name>`.
- Do not use `/mnt/user/...` for SSM paths.
- Map sources as child paths under `/ssm/sources/*`.
- Map overrides as child paths under `/ssm/override/*`.
- Do not mount Docker named/anonymous volumes to `/ssm/sources` or `/ssm/override` parent roots.
- Map `/ssm/merged` as `rw,shared` in the container.
- On the host, prepare the merged root as isolated shared in this order: `mount --make-private` then `mount --make-rshared`.

## Quick Setup (Unraid + Docker Compose)

1. Create host folders (example):
   - `/mnt/cache/appdata/ssm/config`
   - `/mnt/cache/appdata/ssm/merged` (This should NOT be on the array)
   - `/mnt/cache/appdata/ssm/state`
   - `/mnt/disk1/share/override` (Note that you can put override on the array, but make sure to use individual disks when setting up docker volumes, ie. `/mnt/disk1/share/override`)
2. Confirm each source path ends right before the source-name folders.
3. Run the host security bootstrap script in the next section.
4. Set your Suwayomi Local Source bind/path to use the merged folder with `rw,slave` (details in "Connect Suwayomi Local Source to the Merged Folder" below).
5. Choose one container creation method below (Option A, B, or C).
6. Start the container with your chosen method and verify output.

What you should see:
- Source paths look like:
	- For a path like: `/mnt/user/share/suwayomi-manga-downloads/mangas/Source Name/Manga Title/Chapter/file.webp`
	- You'd use:
		- `/mnt/cachepool/share/suwayomi-manga-downloads/mangas`
		- `/mnt/disk1/share/suwayomi-manga-downloads/mangas`
		- `/mnt/disk2/share/suwayomi-manga-downloads/mangas`
		- `/mnt/disk3/share/suwayomi-manga-downloads/mangas`
- Override paths exist and are writable. If override is on the array, they should also be added via raw disks, similar to above.
- The `/ssm/override/priority` container volume is a special override directory. Anything written to the final local source directory will be written to that priority container. Good for an SSD cache pool that the mover would normally move files off from.

## Prepare Host Security and Merged Sharing (Default)

Run this once on host startup (array start in Unraid). It repairs bind-path parent ownership/mode on host volumes, prepares merged bind propagation, installs the hardened seccomp profile, loads AppArmor when available, and ensures host `fuse.conf` contains `user_allow_other`.

### Option A: Download host script

Download [`tools/setup-host-security.sh`](tools/setup-host-security.sh), and run it at startup.

For existing containers (recommended), use Docker inspect-based bind discovery:
```bash
sudo /path/to/SuwayomiSourceMerge/tools/setup-host-security.sh \
  --merged-root /mnt/cache/appdata/ssm/merged \
  --inspect-container suwayomi-source-merge
```

For first setup (container not created yet), provide bind paths directly:
```bash
sudo /path/to/SuwayomiSourceMerge/tools/setup-host-security.sh \
  --merged-root /mnt/cache/appdata/ssm/merged \
  --bind-path /mnt/disk1/share/suwayomi-manga-downloads/mangas \
  --bind-path /mnt/disk2/share/suwayomi-manga-downloads/mangas \
  --bind-path /mnt/disk1/share/override \
  --bind-path /mnt/cache/ssm/override
```

This bind-path preflight clones owner/group/mode from matching peer paths under `/mnt/disk*` (majority vote, tie by newest mtime, then lowest disk number). If no peer exists, it falls back to container `PUID`/`PGID` (or `99:100` when not discoverable).
The script is idempotent and safe to run repeatedly at host startup.

For most users, only `/path/to/SuwayomiSourceMerge/tools/setup-host-security.sh`, `--merged-root`, and `--inspect-container` need changes, however there are more switches available for advanced users.
The script strictly verifies downloaded security profiles against `docker/security/checksums.sha256` and exits on mismatch.

What you should see:
- The script exits without errors.
- It prints bind-path repair diagnostics showing peer source or fallback ownership used for each path segment.
- It prints the exact `docker run`/Compose security flags for your host.
- `docker compose up -d` can start without merged-mount propagation issues.

### Option B: Copy/paste to User Scripts or `/boot/config/go`

Use this only when you cannot use Option A. Option A includes strict checksum verification and bind-path ownership preflight.

```bash
#!/bin/bash
MERGED="/mnt/cache/appdata/ssm/merged"
PROFILE_BASE_URL="https://raw.githubusercontent.com/cjmanca/SuwayomiSourceMerge/main/docker/security"
mkdir -p "$MERGED" /etc/docker/seccomp
mountpoint -q "$MERGED" || mount --bind "$MERGED" "$MERGED"
mount --make-private "$MERGED"; mount --make-rshared "$MERGED"
grep -Eq '^[[:space:]]*user_allow_other([[:space:]]*#.*)?$' /etc/fuse.conf || printf '\nuser_allow_other\n' >> /etc/fuse.conf
curl -fsSL "$PROFILE_BASE_URL/seccomp-mergerfs.json" -o /etc/docker/seccomp/ssm-mergerfs.json
if command -v apparmor_parser >/dev/null 2>&1 && [[ -d /sys/module/apparmor ]]; then curl -fsSL "$PROFILE_BASE_URL/apparmor/ssm-mergerfs" -o /etc/apparmor.d/ssm-mergerfs && apparmor_parser -r /etc/apparmor.d/ssm-mergerfs; fi
```

On Unraid, add this script to startup (for example in `/boot/config/go` or the User Scripts plugin).

## Connect Suwayomi Local Source to the Merged Folder

Set this on your Suwayomi container as part of setup so Local Source points to the merged output.

If Suwayomi is in Docker, bind the same merged folder into Suwayomi with `rw,slave`:

```bash
-v '/mnt/cache/appdata/ssm/merged':'/home/suwayomi/.local/share/Tachidesk/local':'rw,slave'
```

In Unraid using the GUI, edit the Local Manga path and set `Access Mode` to `Read/Write - Slave`.

Then in Suwayomi, use Local Source as usual.

What you should see:
- Local Source titles come from the merged folder.
- Chapter gaps are reduced because matching titles are grouped together.

## Create the Container (Choose One Method)

### Option A: Unraid Template (GUI Setup)

If you prefer Unraid's Docker UI instead of Compose, use this template:

- Raw template URL: `https://raw.githubusercontent.com/cjmanca/SuwayomiSourceMerge/main/unraid/templates/my-SuwayomiSourceMerge.xml`
- Repo file: [`unraid/templates/my-SuwayomiSourceMerge.xml`](unraid/templates/my-SuwayomiSourceMerge.xml)

On the Unraid host, download the template to your user templates folder:

```bash
mkdir -p /boot/config/plugins/dockerMan/templates-user
wget -O /boot/config/plugins/dockerMan/templates-user/my-SuwayomiSourceMerge.xml \
  https://raw.githubusercontent.com/cjmanca/SuwayomiSourceMerge/main/unraid/templates/my-SuwayomiSourceMerge.xml
```

Then in the Unraid web UI:

1. Go to `Docker` > `Add Container`.
2. Select the `SuwayomiSourceMerge` template.
3. Fill in your host paths (use direct disk or pool paths, not `/mnt/user`).
4. Keep `/ssm/merged` as `rw,shared`.
5. Apply and start the container.

What you should see:
- The container starts without permission/mount errors.
- Paths in the template map to the same required container targets used in this README.

### Option B: Docker Compose

```yaml
services:
  suwayomi-source-merge:
    image: ghcr.io/cjmanca/suwayomisourcemerge:latest
    container_name: suwayomi-source-merge
    environment:
      PUID: "99"
      PGID: "100"
      ENTRYPOINT_FUSE_CONF_MODE: "host-managed"
    volumes:
      - /mnt/cache/appdata/ssm/config:/ssm/config
      - /mnt/cache/appdata/ssm/merged:/ssm/merged:rw,shared
      - /mnt/cache/appdata/ssm/state:/ssm/state
      - /mnt/disk1/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk1
      - /mnt/disk2/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk2
      - /mnt/disk3/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk3
      - /mnt/pool/share/override:/ssm/override/priority
      - /mnt/disk1/share/override:/ssm/override/disk1
      - /mnt/disk2/share/override:/ssm/override/disk2
      - /mnt/disk3/share/override:/ssm/override/disk3
    devices:
      - /dev/fuse:/dev/fuse
    cap_add:
      - SYS_ADMIN
    security_opt:
      - apparmor:ssm-mergerfs
      - seccomp:/etc/docker/seccomp/ssm-mergerfs.json
    restart: unless-stopped
```

```bash
docker compose up -d
docker compose logs -f suwayomi-source-merge
```

### Option C: Docker Run (Short Form)

```bash
docker run --rm \
  --device /dev/fuse \
  --cap-add SYS_ADMIN \
  --security-opt apparmor=ssm-mergerfs \
  --security-opt seccomp=/etc/docker/seccomp/ssm-mergerfs.json \
  -e ENTRYPOINT_FUSE_CONF_MODE=host-managed \
  -e PUID=99 \
  -e PGID=100 \
  -v /mnt/cache/appdata/ssm/config:/ssm/config \
  -v /mnt/cache/appdata/ssm/merged:/ssm/merged:rw,shared \
  -v /mnt/cache/appdata/ssm/state:/ssm/state \
  -v /mnt/disk1/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk1 \
  -v /mnt/pool/share/override:/ssm/override/priority \
  ghcr.io/cjmanca/suwayomisourcemerge:latest
```

### Legacy Fallback (When Hardened Profiles Are Not Available)

If your host cannot support profile loading (for example, no AppArmor tooling), keep using the legacy flags:

```bash
--security-opt apparmor=unconfined \
--security-opt seccomp=unconfined
```

## Verify Startup

What you should see:
- The container stays running in `docker ps`.
- Logs show normal startup and scan activity (no repeating fatal errors).
- After initial scan, manga title folders appear under your merged host path.

## Configure `settings.yml` and `manga_equivalents.yml`

Your config files live in `/ssm/config` (host path example: `/mnt/cache/appdata/ssm/config`).

On first run, SuwayomiSourceMerge will create missing config files with defaults where supported. You can then edit them and restart the container.

### `settings.yml` (app behavior)

Use this file for scan timing, logging, metadata settings, and runtime options. It will be generated on first run, and auto-healed if missing settings.

Start with defaults, then only change what you need. Common user changes are:
- log level (`logging.level`)
- excluded source names (`runtime.excluded_sources`)
- FlareSolverr URL for metadata (`runtime.flaresolverr_server_url`)

Example (partial):

```yaml
runtime:
  flaresolverr_server_url: http://192.168.1.50:8191
  excluded_sources:
    - Local source

logging:
  level: normal
```

### `manga_equivalents.yml` (manual title matching)

Use this file when the same series appears with different names across sources.

- `canonical` is the final merged display name.
- `aliases` are alternate names that should map to the same series.

Example:

```yaml
groups:
  - canonical: Solo Leveling
    aliases:
      - I Alone Level Up
      - Only I Level Up
```

What you should see:
- After restart, listed aliases merge into the canonical title.
- If YAML has a formatting or validation error, startup fails and logs show the config error.
- Full schema and all settings: [`docs/config-schema.md`](docs/config-schema.md)
- This will be auto-populated from comick.dev metadata if you have flaresolverr information entered.

## Common Mistakes

1. Using `/mnt/user/...` paths.
   - Fix: use direct disk/pool paths (`/mnt/disk*`, `/mnt/cache`, `/mnt/<pool-name>`).
2. Wrong bind propagation on merged mount.
   - Fix: container mount must be `rw,shared`, and host merged root must run `private` then `rshared`.
3. Wrong source folder depth.
   - Fix: each `/ssm/sources/*` bind should point to a folder whose direct children are source names.

## Non-Container Setup (Optional)

### Bare Metal (Linux)

Bare metal is supported, but defaults are container-oriented (`/ssm/...` paths). For full bare-metal steps and contributor-focused runtime details, use `DEVELOPMENT.md`.

## Optional Advanced / Developer Links

- Contributor and runtime internals: [`DEVELOPMENT.md`](DEVELOPMENT.md)
- Config schema reference: [`docs/config-schema.md`](docs/config-schema.md)
- Unraid app template: [`unraid/templates/my-SuwayomiSourceMerge.xml`](unraid/templates/my-SuwayomiSourceMerge.xml)
