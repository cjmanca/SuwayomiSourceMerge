# SuwayomiSourceMerge

SuwayomiSourceMerge combines duplicate manga libraries from multiple Suwayomi sources into one merged view so chapter gaps are easier to avoid while reading.

In Suwayomi, set your local manga directory to use the merged output from SuwayomiSourceMerge.

If you're running Suwayomi in a docker container, you'll need to set the local source volume to rshared:
`--mount type=bind,source=/mnt/cache/appdata/ssm/merged,target=/home/suwayomi/.local/share/Tachidesk/local,bind-propagation=rshared`

If you're running SuwayomiSourceMerge in Docker, set the `/ssm/merged` bind to `rshared` as well.

It uses `mergerfs` under a .NET control plane to:

- combine equivalent manga titles from different sources
- allow custom files to override existing files from the downloads
- auto-handle chapter naming quirks that affect ordering
- auto-create details.json from the ComicInfo.xml files that sources provide

Of note if you already use local manga: Move anything from your current local manga directory into the overrides directory. It'll combine into the new merged directory, which you'll use for the new local manga directory.

## Unraid Note:

DON'T USE THE `/mnt/user` SHARE FOR ANY PATHS. SuwayomiSourceMerge uses mergerfs to create a FUSE mount in much the same way as Unraid does for it's array. Trying to use a FUSE mount on top of another FUSE mount is just going to give you headaches.

Use raw disks for everything and you'll save yourself a lot of pulled hair. You can still use the disks in the unraid array, just reference them directly (see volume examples below).

## Use with Docker

### Unraid Docker volume layout (important)

Use direct disk or pool mount paths for runtime data:

- `/mnt/disk*` for array disks (example: `/mnt/disk1/share/sources`)
- `/mnt/cache` or `/mnt/<pool-name>` for cache/pool data (example: `/mnt/cache/share/override`)

Avoid `/mnt/user/...` for these container paths:

- `/ssm/sources/*`
- `/ssm/override/*`
- `/ssm/merged`

Why: `/mnt/user` is Unraid's user-share FUSE layer. SuwayomiSourceMerge also depends on FUSE (`mergerfs`) plus `inotify` change detection. Stacking those on top of `/mnt/user` adds an extra virtualization layer that can cause slower scans, delayed/missed watcher behavior, and confusing write placement behavior.

Recommended host-to-container mapping layout:

- Config: `/mnt/cache/appdata/ssm/config` -> `/ssm/config`
- Sources disk 1: `/mnt/disk1/share/suwayomi-manga-downloads/mangas` -> `/ssm/sources/disk1`
- Sources disk 2: `/mnt/disk2/share/suwayomi-manga-downloads/mangas` -> `/ssm/sources/disk2`
- Sources disk 3: `/mnt/disk3/share/suwayomi-manga-downloads/mangas` -> `/ssm/sources/disk3`
- Preferred override: `/mnt/pool/share/override` -> `/ssm/override/priority`
- Additional override disk 1: `/mnt/disk1/share/override` -> `/ssm/override/disk1`
- Additional override disk 2: `/mnt/disk2/share/override` -> `/ssm/override/disk2`
- Additional override disk 3: `/mnt/disk3/share/override` -> `/ssm/override/disk3`
- Merged output root: `/mnt/cache/appdata/ssm/merged` -> `/ssm/merged`
- Runtime state: `/mnt/cache/appdata/ssm/state` -> `/ssm/state`

The override directory can be used to place your `details.json` and `cover.jpg` files, and any custom mangas that aren't found in the source downloads. If you currently have anything in your local manga directory, transfer it to the overrides directory instead. I'd recommend having an empty merged directory prior to running SuwayomiSourceMerge.

The `Preferred override` directory will be used for new file creations within the merged directory.

Each of the sources directories should contain suwayomi sources, such that if you use `/mnt/disk1/share/suwayomi-manga-downloads/mangas`, there should be a structure like this:
`/mnt/disk1/share/suwayomi-manga-downloads/mangas/SourceName (EN)/Manga Name/Official__Chapter 1/001.webp`

Note that the source name (`SourceName (EN)`) is a direct child to the entered volume path.

## Prep merge directory for sharing

The directory used for the merged output needs to be set to rshared in order for the two docker containers to properly interact with the fuse mounts.

Create a script to set the merged directory as an rshared mount:
```bash
#!/bin/bash
MERGED="/mnt/cache/appdata/ssm/merged"
mkdir -p "$MERGED"
mountpoint -q "$MERGED" || mount --bind "$MERGED" "$MERGED"
mount --make-rshared "$MERGED"
```

You'll want to set this script to run at startup. On unraid this can be done in the `/boot/config/go` file or using the userscripts plugin.

### Option A: pull from GHCR

```bash
docker pull ghcr.io/cjmanca/suwayomisourcemerge:latest
```

Branch builds are also published for testing with explicit branch tags:

- `ghcr.io/cjmanca/suwayomisourcemerge:branch-<branch-name>`
- `ghcr.io/cjmanca/suwayomisourcemerge:sha-<short-sha>`
- `ghcr.io/cjmanca/suwayomisourcemerge:v<version>` (tag pushes)

Published images already bake file capabilities required for non-root mergerfs mounts:

- `/usr/bin/fusermount3` -> `cap_sys_admin+ep`
- `$(command -v mergerfs)` -> `cap_sys_admin+ep`

If you build a custom or derived image and replace packages, re-apply and verify capabilities:

```bash
apt-get update && apt-get install -y libcap2-bin
setcap cap_sys_admin+ep /usr/bin/fusermount3
setcap cap_sys_admin+ep "$(command -v mergerfs)"
getcap /usr/bin/fusermount3
getcap "$(command -v mergerfs)"
```

These file capabilities are in addition to runtime container requirements like `/dev/fuse`, `SYS_ADMIN`, and relaxed seccomp/apparmor options shown below.

#### Run the container

```bash
docker run --rm \
  --device /dev/fuse \
  --cap-add SYS_ADMIN \
  --security-opt apparmor:unconfined \
  --security-opt seccomp=unconfined \
  -e PUID=99 \
  -e PGID=100 \
  -v /mnt/cache/appdata/ssm/config:/ssm/config \
  -v /mnt/cache/appdata/ssm/state:/ssm/state \
  -v /mnt/disk1/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk1 \
  -v /mnt/disk2/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk2 \
  -v /mnt/disk3/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk3 \
  -v /mnt/pool/share/override:/ssm/override/priority \
  -v /mnt/disk1/share/override:/ssm/override/disk1 \
  -v /mnt/disk2/share/override:/ssm/override/disk2 \
  -v /mnt/disk3/share/override:/ssm/override/disk3 \
  --mount type=bind,source=/mnt/cache/appdata/ssm/merged,target=/ssm/merged,bind-propagation=rshared \
  ghcr.io/cjmanca/suwayomisourcemerge:latest
```

Required container paths:

- `/ssm/config`
- `/ssm/sources`
- `/ssm/override`
- `/ssm/merged`
- `/ssm/state`

`/ssm/merged` must be configured with `bind-propagation=rshared`.

### Option B: deploy with Docker Compose

Use the compose example in the "Docker Compose and CI examples" section below.

#### `compose.yaml` example

```yaml
services:
  suwayomi-source-merge:
    image: ghcr.io/cjmanca/suwayomisourcemerge:latest
    # Optional local build instead of pulling:
    # build:
    #   context: .
    #   dockerfile: Dockerfile
    container_name: suwayomi-source-merge
    environment:
      PUID: "99"
      PGID: "100"
    volumes:
      - /mnt/cache/appdata/ssm/config:/ssm/config
      - /mnt/cache/appdata/ssm/state:/ssm/state
      - /mnt/disk1/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk1
      - /mnt/disk2/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk2
      - /mnt/disk3/share/suwayomi-manga-downloads/mangas:/ssm/sources/disk3
      - /mnt/pool/share/override:/ssm/override/priority
      - /mnt/disk1/share/override:/ssm/override/disk1
      - /mnt/disk2/share/override:/ssm/override/disk2
      - /mnt/disk3/share/override:/ssm/override/disk3
      - type: bind
        source: /mnt/cache/appdata/ssm/merged
        target: /ssm/merged
        bind:
          propagation: rshared
    devices:
      - /dev/fuse:/dev/fuse
    cap_add:
      - SYS_ADMIN
    security_opt:
      - apparmor:unconfined
      - seccomp:unconfined
    restart: unless-stopped
```

Run with compose:

```bash
docker compose up -d
docker compose logs -f suwayomi-source-merge
docker compose down
```

## Bare metal use (Linux)

Bare metal is supported, but current runtime defaults are container-oriented and expect `/ssm/...` paths.

### Prerequisites

- .NET SDK 9.0
- `mergerfs`
- `inotify-tools`
- `util-linux` (`findmnt`)
- `fuse3`

Example on Debian/Ubuntu:

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-9.0 mergerfs inotify-tools util-linux fuse3
```

### Prepare runtime directories

```bash
sudo mkdir -p /ssm/config /ssm/sources /ssm/override /ssm/merged /ssm/state
sudo chown -R "$USER":"$USER" /ssm
```

At minimum, place your YAML config files under `/ssm/config` (`settings.yml`, `manga_equivalents.yml`, `scene_tags.yml`, `source_priority.yml`).
Missing files/settings are bootstrapped where supported.

You can use symlinks or mounts to populate /ssm/sources and /ssm/override, as described above.

### Run from source

```bash
dotnet run --project SuwayomiSourceMerge/SuwayomiSourceMerge.csproj
```

## AI Disclaimer

This was largely ported by AI (ChatGPT-5.3-Codex) from an existing shell script, after the shell script became too unwieldy to continue maintaining.

## Development

For contributor rules, validation expectations, and project implementation baseline, see `AGENTS.md` and `DEVELOPMENT.md`.
