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
- Use Comick API data (`https://api.comick.dev/`) as the primary metadata source for `details.json`, then fallback to ComicInfo/source-derived behavior for missing fields. Information for every field in `details.json` should be able to be found in the comick API.
	- When populating `genre`, use both `md_comic_md_genres` (first) and `mu_comic_categories` (second). Both should be placed in the `details.json` `genre` array, but ordered so `md_comic_md_genres` is before `mu_comic_categories`.
	- Only include each `mu_comic_categories` entry if it has more positive than negative votes.
- Append a language-coded bullet-list block of the main title and alternate titles to the end of `details.json` description.
- Update `manga_equivalents.yml` from Comick alternate titles:
  - append only missing aliases when an equivalent group already exists;
  - create a new group when missing, choosing canonical title in this order:
    - first `md_titles` entry matching `runtime.preferred_language` among alternates with non-empty normalized keys;
    - fallback `md_titles` entry matching `en` among alternates with non-empty normalized keys;
    - fallback Comick main title.
- Apply equivalence updates immediately in-process after successful persistence so subsequent passes see the new mappings.

Planned runtime settings for this feature:

- `runtime.comick_metadata_cooldown_hours` (default `24`): per-title API cooldown window used to reduce repeated Comick requests.
- `runtime.flaresolverr_server_url` (default empty): optional FlareSolverr base URL used only when Cloudflare blocks direct API calls.
- `runtime.flaresolverr_direct_retry_minutes` (default `60`): sticky FlareSolverr mode retry interval for probing direct Comick access again.
- `runtime.preferred_language` (default `en`): preferred language code used for canonical-title selection from Comick alternate title lists. Canonical selection considers only alternate titles whose normalized title keys are non-empty. Allow any non-empty string. When matching, try to match exactly, but fall back to the first 2 characters (so `zh-CN` would fallback to just `zh` and match anything starting with that such as `zh`, `zh-TW`, `zh-HK`, etc). If none of that matches, still fall back to `en`, and then normal title if that also fails.

Planned Comick/Flaresolverr routing behavior:

- API routing is direct-first for Comick (`/v1.0/search/`, `/comic/{slug}/`).
- Check each search result via `/comic/{slug}/`, using the `md_titles` (normalized) it returns to check against entries in `manga_equivalents.yml` (or normalized title if no matching entry in `manga_equivalents.yml`).
- The first search result is often the correct one, so try that first. If that doesn't match, sort the remaining search results by most likely to least likely to reduce API hits on retrieving the full comic info. Short circuit out once a valid matching entry is found.
- If direct access is Cloudflare-blocked and FlareSolverr is configured, switch to sticky FlareSolverr mode.
- After `runtime.flaresolverr_direct_retry_minutes`, retry direct mode; if Cloudflare still blocks, return to sticky FlareSolverr mode.
- If FlareSolverr is not configured, Cloudflare-blocked requests fall back to existing ComicInfo/source-only metadata paths.
- If both `cover.jpg` and `details.json` already exist for a title, skip Comick API queries entirely for that title.

## Planned metadata API pacing and response caching (docs-only, not yet implemented)

Planned implementation scope for the next feature iteration:

- Add configurable pacing between actual metadata HTTP requests so all requests are still attempted, but spaced out.
- Keep current artifact/cooldown short-circuit behavior; when no outbound API call is needed, no pacing delay should be applied.
- Add persisted Comick API response caching for both search and comic-detail endpoints so repeated lookups can be served from cache without outbound requests or pacing delay.
- Keep scan behavior resilient: pacing delays and cache misses must not be treated as scan failures by themselves.

Implementation options considered:

- Option A: coordinator-only pacing/caching.
  - Rejected because it does not cover candidate detail probes and cover download HTTP calls.
- Option B: separate pacing/caching implementations per HTTP caller.
  - Rejected due to policy drift risk and inability to enforce global request spacing.
- Option C: shared metadata request throttle plus gateway-owned Comick response cache.
  - Selected for consistent behavior and centralized policy/testability.

Selected behavior for this planned feature:

- Pacing scope:
  - apply to all metadata HTTP requests:
    - Comick direct API requests (`/v1.0/search/`, `/comic/{slug}/`)
    - FlareSolverr-routed Comick requests
    - cover-image download HTTP requests
- Pacing policy:
  - global request spacing across metadata HTTP calls
  - delay is applied only before actual outbound requests
  - cache hits and existing short-circuit paths do not pause
- Cache scope:
  - cache Comick search and comic-detail API returns only
  - do not cache cover-image payload downloads in this iteration
- Cache outcomes:
  - cache stable outcomes: `Success`, `NotFound`, `HttpFailure`, `CloudflareBlocked`, `MalformedPayload`
  - do not cache `TransportFailure` or cooperative `Cancelled`
- Cache expiry:
  - TTL-based expiry (configurable), default 24 hours

Planned runtime settings additions:

- `runtime.metadata_api_request_delay_ms` (default `1000`):
  - non-negative integer milliseconds
  - `0` disables pacing
- `runtime.metadata_api_cache_ttl_hours` (default `24`):
  - positive integer hour TTL for persisted Comick response cache entries

Planned settings/schema behavior:

- strict runtime parsing requires both new runtime keys
- tooling/relaxed parsing may omit both keys, but validates numeric ranges when provided
- settings self-heal should add both keys with defaults when missing
- `docs/config-schema.md` should be updated with both keys, defaults, and range constraints

Planned metadata state-store extension:

- Extend persisted metadata state to include Comick API cache entries in addition to existing cooldown and sticky FlareSolverr fields.
- Keep backward compatibility for existing state files by treating missing cache sections as empty cache.
- Persist cache entries with explicit expiry timestamps.

Planned cache entry model:

- `endpoint_kind`: `search` or `comic`
- `request_key`: exact trimmed query/slug key
- `outcome`: serialized `ComickDirectApiOutcome`
- `status_code`: optional integer HTTP status
- `diagnostic`: optional diagnostic string
- `payload_json`: serialized payload for `Success` outcomes only
- `expires_at_unix_seconds`: UTC expiry timestamp

Planned gateway/cache flow:

- On `SearchAsync` and `GetComicAsync`:
  - first attempt cache read for unexpired valid entries
  - on cache hit, return cached result immediately (no throttle, no outbound request)
  - on cache miss, execute existing routing behavior (direct-first + sticky FlareSolverr fallback)
  - after live request, persist cache entry only for cacheable outcomes
  - keep state-store operations best-effort with warning telemetry on non-fatal failures

Planned pacing integration points:

- Introduce one shared metadata request-throttle service instance for runtime composition.
- Use that same throttle instance in:
  - Cloudflare-aware Comick gateway for live Comick/FlareSolverr requests
  - override cover service for live cover download requests

Planned non-goals for this iteration:

- no cover payload caching
- no broad generic HTTP cache layer outside Comick metadata endpoints
- no behavior change to existing details/cover artifact existence gates beyond pacing/cache integration

Planned testing and acceptance criteria:

- configuration/default/self-heal/validation tests for new runtime settings
- metadata option mapping tests from settings to runtime options
- request-throttle tests:
  - first request no wait
  - subsequent requests wait expected duration
  - `0` delay disables wait
  - cancellation while waiting propagates cooperatively
  - failed requests still advance pacing window
- gateway cache tests:
  - cache hit bypasses outbound request and throttle
  - cache miss executes live request then persists cache entry when eligible
  - expired entries miss and refresh
  - malformed cache payload entries are treated as cache miss
  - non-cacheable outcomes are not persisted
- cover-service pacing tests:
  - existing-cover short-circuit avoids throttle and HTTP
  - live download path uses throttle
- metadata state-store tests:
  - cache-field persistence round-trip
  - backward-compatible load when cache field is missing
  - deterministic handling of malformed cache-state content

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
