# Development Notes

This document is contributor-focused. Public usage and quickstart guidance is in `README.md`.

## Development baseline

- Runtime target: `.NET 9.0`
- Mutation config for settings/validation code: `tests/SuwayomiSourceMerge.UnitTests/stryker-config.json` (threshold break: `80`)
- New features should be designed for testability and include tests

## C# port requirements baseline

- Documentation is source of truth when it conflicts with legacy shell behavior.
- Linux-only runtime target inside Docker with required FUSE permissions.
- Implementation style is hybrid: C# orchestration with external `mergerfs`, `findmnt`, and `fusermount*` commands.
- Canonical config is YAML (`settings.yml`, `manga_equivalents.yml`, `scene_tags.yml`, and `source_priority.yml`).
- On first run, if legacy `.txt` config exists and YAML does not, import and convert to YAML.
- `scene_tags.yml` is fully data-driven and should be auto-created with defaults if missing.
- Scene-tag stripping happens before punctuation/whitespace stripping and ASCII folding; the latter transforms are comparison-only.
- Final merged display directory names should preserve punctuation except for removed scene-tag suffixes.
- Punctuation-only scene tags are valid and must be supported for stripping.
- Scene-tag detection ignores punctuation differences for text/mixed tags (for example, `asura scan` matches `Asura-Scan`), while punctuation-only tags use exact punctuation-sequence matching.
- Normalization follows docs strictly, including leading-article stripping and trailing-`s` stripping per word.
- Source priority matching is normalized-only (case/punctuation-insensitive token comparison).
- Chapter rename behavior stays aligned with the existing shell script for v1.
- For chapter rename rules, shell parity is the baseline; when docs/examples conflict, documented behavior wins (for example, embedded `MangaChapter6` chapter-token matching).
- `details.json` auto-generation from `ComicInfo.xml` is included in v1.
- Runtime discovery in container mode treats all directories directly under `sources` as source volumes and under `override` as override volumes.
- Runtime settings come from `settings.yml`; missing settings must be auto-added with defaults.
- Logging should prioritize clarity and relevance and write to the config directory.
- Docker-based integration tests are acceptable for v1.
- All permissions on created files/directories are set from container `PUID`/`PGID` (or equivalent bare-metal switches when not containerized).

Configuration schema reference: `docs/config-schema.md`.

Startup mount safeguards:

- Command-reported mount success is validated by checking for a live mergerfs mount entry and probing directory access.
- Mount-readiness directory access probing is timeout-bounded via command execution (`ls -A`) using the existing unmount command timeout settings.
- Consecutive mount/remount failures abort remaining actions for the pass after `runtime.max_consecutive_mount_failures` (default `5`).
- `Transport endpoint is not connected` conditions are treated as failed mount readiness checks and surfaced as mount failures.
- Mount command composition applies `threads=1` when `runtime.mergerfs_options_base` does not explicitly set a `threads` value to reduce per-mount process/thread pressure on high mount-count startups.

## Planned metadata enrichment (docs-only, not yet implemented)

Planned implementation scope for the next feature iteration:

- Enrich per-title metadata during merge passes by generating missing `cover.jpg` and missing `details.json` without overwriting existing artifacts.
- Use Comick API data as the primary metadata source for `details.json`, then fallback to ComicInfo/source-derived behavior for missing fields.
- Append a language-coded bullet-list block of the main title and alternate titles to the end of `details.json` description.
- Update `manga_equivalents.yml` from Comick alternate titles:
  - append only missing aliases when an equivalent group already exists;
  - create a new group when missing, choosing canonical title in this order:
    - first `md_titles` entry matching `runtime.preferred_language`;
    - fallback `md_titles` entry matching `en`;
    - fallback Comick main title.
- Apply equivalence updates immediately in-process after successful persistence so subsequent passes see the new mappings.

Planned runtime settings for this feature:

- `runtime.comick_metadata_cooldown_hours` (default `24`): per-title API cooldown window used to reduce repeated Comick requests.
- `runtime.flaresolverr_server_url` (default empty): optional FlareSolverr base URL used only when Cloudflare blocks direct API calls.
- `runtime.flaresolverr_direct_retry_minutes` (default `60`): sticky FlareSolverr mode retry interval for probing direct Comick access again.
- `runtime.preferred_language` (default `en`): preferred language code used for canonical-title selection from Comick alternate title lists. Allow any non-empty string. When matching, try to match exactly, but fall back to the first 2 characters (so `zh-CN` would fallback to just `zh` and match anything starting with that such as `zh`, `zh-TW`, `zh-HK`, etc). If none of that matches, still fall back to `en`, and then normal title if that also fails.

Planned Comick/Flaresolverr routing behavior:

- API routing is direct-first for Comick (`/v1.0/search/`, `/comic/{slug}/`).
- If direct access is Cloudflare-blocked and FlareSolverr is configured, switch to sticky FlareSolverr mode.
- After `runtime.flaresolverr_direct_retry_minutes`, retry direct mode; if Cloudflare still blocks, return to sticky FlareSolverr mode.
- If FlareSolverr is not configured, Cloudflare-blocked requests fall back to existing ComicInfo/source-only metadata paths.
- If both `cover.jpg` and `details.json` already exist for a title, skip Comick API queries entirely for that title.

## Container runtime assets

Build image:

```bash
docker build -t suwayomi-source-merge:local -f Dockerfile .
```

Run image with host bind mounts:

```bash
docker run --rm \
  -e PUID=99 \
  -e PGID=100 \
  -v /host/ssm/config:/ssm/config \
  -v /host/ssm/sources:/ssm/sources \
  -v /host/ssm/override:/ssm/override \
  -v /host/ssm/merged:/ssm/merged \
  -v /host/ssm/state:/ssm/state \
  suwayomi-source-merge:local
```

Container defaults and paths:

- `PUID` default: `99`
- `PGID` default: `100`
- Required mount paths: `/ssm/config`, `/ssm/sources`, `/ssm/override`, `/ssm/merged`, `/ssm/state`
- If default `ssm` user/group names already map to different IDs, the entrypoint uses deterministic fallback names while honoring requested `PUID`/`PGID`.

For real FUSE/mergerfs runtime behavior (not mocked test mode), the host/runtime must provide `/dev/fuse` and required capabilities (commonly `SYS_ADMIN` plus relaxed seccomp/apparmor constraints appropriate for FUSE).

## Docker end-to-end integration tests

Run all tests (includes Docker E2E):

```bash
dotnet test
```

Run only Docker E2E suite:

```bash
dotnet test tests/SuwayomiSourceMerge.IntegrationTests/SuwayomiSourceMerge.IntegrationTests.csproj
```

The Docker E2E suite requires a reachable Docker daemon and intentionally fails when Docker is unavailable.
The suite uses a mocked tool profile inside Docker (`/ssm/mock-bin`) so CI does not require privileged FUSE mounts.
