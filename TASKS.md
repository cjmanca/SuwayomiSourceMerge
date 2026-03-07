# TASKS.md

- [x] 2026-03-06 - Prevent merge-pass canonical-title build failures when preferred override title directories are temporarily missing (for example after Unraid mover activity) by hardening branch-link staging target setup and adding regression coverage.

- [x] 2026-03-06 - Address unresolved PR #47 AI review threads by hard-failing findmnt snapshot capture on output-task faults, fixing `collect-mount-rca.sh` command logging redirection, and aligning README/Unraid merged-bind guidance to container `rw,shared` with host isolated `rshared`.

- [x] 2026-03-06 - Update README merged-root setup guidance to require isolated shared host bind semantics (`mount --make-private` then `mount --make-rshared`) to avoid duplicate host-visible mount entries.

- [x] 2026-03-06 - Clarify `docker/entrypoint.sh` sources/override root ownership comment to document parent-root child-bind intent and non-recursive ownership safety.

- [x] 2026-03-06 - Prevent Docker mount-root conflicts for source/override submount deployments by removing image-level `/ssm/sources` and `/ssm/override` VOLUME roots, ensuring entrypoint root directory creation, adding GHCR workflow volume-root guardrails, and aligning README/Unraid template guidance.

- [x] 2026-03-06 - Fix remount ENOTCONN recovery to retry via mount-only apply (avoid duplicate unmount), switch mountpoint-ensure failure severity to probe-based directory usability checks, and reset consecutive hard-failure fail-fast counters on soft failures.

- [x] 2026-03-06 - Stabilize mount reconciliation against duplicate-stack growth by streaming `findmnt` snapshot parsing, suppressing degraded-visibility mount/remount apply actions, iterating lifecycle cleanup to convergence, counting only hard mount failures for fail-fast, and auto-recovering ENOTCONN readiness failures with one inline retry cycle.

- [x] 2026-03-05 - Include the ambiguous requested title in `metadata.candidate.ambiguity` warning telemetry context so ambiguity logs are actionable.

- [x] 2026-03-05 - Stabilize cache-only metadata miss semantics with typed lookup-failure signaling so cooldown cache misses suppress details fallback without failing merge passes, and remove diagnostic-string control-flow dependence.

- [x] 2026-03-05 - Narrow FlareSolverr-enabled details fallback suppression so ComicInfo fallback is skipped only when required Comick lookups fail (including unresolved cache-only misses), while clean no-match completion still permits fallback.

- [x] 2026-03-05 - Extend FlareSolverr-enabled metadata safety gating so any unresolvable Comick lookup path suppresses `details.json` fallback generation (no ComicInfo-only details writes), not just cooldown cache-only misses.

- [x] 2026-03-05 - When FlareSolverr is configured and title cooldown enforces cache-only Comick lookup, suppress `details.json` generation (including ComicInfo fallback) on cache miss/no-match to avoid partial metadata writes.

- [x] 2026-03-05 - Fix metadata title-cooldown behavior to use cache-only Comick lookup mode (no live requests) so cached matches can still drive missing metadata artifacts and `manga_equivalents.yml` alias updates.

- [x] 2026-03-05 - Lock metadata delay-rename policy by adding regression tests that preserve intentional behavior: legacy `runtime.metadata_api_request_delay_ms` is ignored during self-heal, and self-heal does not auto-correct `runtime.metadata_api_max_request_delay_ms < runtime.metadata_api_min_request_delay_ms`.

- [x] 2026-03-05 - Validate AI review-note accuracy for metadata timing tests/throttle math, keep the valid `max < min` options test unchanged, and harden throttle full-range delay selection to preserve inclusive bounds with deterministic unit coverage.

- [x] 2026-03-05 - Rename metadata pacing setting `runtime.metadata_api_request_delay_ms` to `runtime.metadata_api_min_request_delay_ms`, add `runtime.metadata_api_max_request_delay_ms` (default `5000`), apply `max >= min` validation, and randomize per-request pacing delay selection between configured bounds.

- [x] 2026-03-05 - Enforce outage cache miss invalidation for Comick title attempts by short-circuiting candidate probes on first `FlaresolverrUnavailable`, prioritizing coordinator invalidation over matched payload acceptance, and aligning docs/tests for cache-hit-allowed cooldown behavior.

- [x] 2026-03-05 - Enforce FlareSolverr-only Comick routing with explicit `FlaresolverrUnavailable` outcome, outage-cooldown short-circuit behavior, coordinator metadata-write suppression for unavailable branches, runtime composition hardening against raw Comick calls, and aligned unit/integration coverage/docs updates.

- [x] 2026-03-05 - Review AI code-review follow-ups for Comick metadata hardening, remove redundant `[InlineData]` test-parameter validation in `ComickDirectApiClientTests.PayloadValidation`, keep internal converter property-name guards by intent, and close nullable-field "breaking-change" notes as inaccurate with citations.

- [x] 2026-03-05 - Harden Comick `/comic/{slug}/` payload tolerance by enforcing match-critical validation only (`comic` node + usable title/alias), adding tolerant DTO converters for non-critical scalar/list/object drift, and expanding malformed-schema regression coverage so non-used field shape drift no longer fails full comic parsing.

- [x] 2026-03-05 - Harden Comick search payload fault tolerance by adding tolerant `md_titles` converter normalization (array/object/string/mixed -> filtered alias list), removing typed `translation_completed` parsing from `ComickSearchComic`, and adding drift-focused payload regression coverage while keeping `slug`/`title` as the only strict search match fields.

- [x] 2026-03-05 - Replace hard-coded `ComickComicLinks` DTO fields with dynamic extension-data link entry parsing so unknown/new Comick link keys and value token shapes are preserved without malformed-payload failures.

- [x] 2026-03-05 - Harden Comick `links.mb` payload compatibility by treating empty/whitespace string tokens as null, widening MangaBuddy model/converter handling to `long?`, and adding regression coverage for Int64-range, overflow, and non-scalar token cases.

- [x] 2026-03-05 - Add focused Comick candidate malformed-payload debug telemetry (`metadata.candidate.malformed_payload`) including slug/candidate index/parser diagnostic so schema drift is visible without cache-event correlation.

- [x] 2026-03-05 - Fix Comick exact-match false negatives caused by polymorphic `links.mb` payload shape (`int` or numeric string) by adding targeted converter support and regression tests for direct payload parsing plus first-candidate short-circuit matching.

- [x] 2026-03-05 - Resolve CloudflareAwareComickGateway response-normalization review follow-ups by confirming partial-class fatal-exception helper coverage, ordering TryExtractJsonPreContent out parameters with diagnostic last for consistency, and documenting response-prefix truncation rationale.

- [x] 2026-03-05 - Refine CloudflareAwareComickGateway FlareSolverr normalization diagnostics with tri-state HTML wrapper detection (`detected`/`not_detected`/`unknown`), retain derived `is_html_wrapped` compatibility logging, and add regression coverage for failed `<pre>` detection and parser-failure unknown state.
- [x] 2026-03-05 - Align CloudflareAwareComickGateway parser exception handling with fatal-exception policy by restricting FlareSolverr HTML pre-node parse catches to non-fatal exceptions and adding regression coverage for non-fatal malformed mapping plus fatal passthrough rethrow behavior.
- [x] 2026-03-05 - Eliminate false `<pre` prefix matches in FlareSolverr response normalization by switching `CloudflareAwareComickGateway` HTML extraction to parser-backed `<pre>` node selection (HtmlAgilityPack), and add regression coverage for `<preload>` + valid `<pre>` success and malformed-wrapper no-throw behavior.
- [x] 2026-03-05 - Harden FlareSolverr upstream response normalization in CloudflareAwareComickGateway by stripping optional UTF BOM prefixes for raw/pre-extracted JSON candidates, scanning multiple `<pre>` blocks for the first JSON-root-compatible payload, and adding regression coverage for BOM success, multi-`<pre>` selection, and missing `</pre>` malformed diagnostics.
- [x] 2026-03-05 - Restore FlareSolverr wrapper status-precedence in CloudflareAwareComickGateway so 403/503 challenge and non-2xx HttpFailure classification occur before response normalization, and add regression coverage for 500/403 precedence paths.
- [x] 2026-03-05 - Normalize FlareSolverr HTML-wrapped Comick upstream responses (`<pre>...</pre>`) before Comick payload parsing, add response-normalization diagnostics, and extend metadata gateway regression coverage.
- [x] 2026-03-05 - Address unresolved PR #45 AI review threads by centralizing metadata endpoint-path normalization helpers, fixing workflow-options test indentation, isolating OverrideCoverService lock-lifecycle tests from parallel execution, clarifying comick_search_max_results task chronology, and posting thread resolutions.
- [x] 2026-03-04 - Centralize metadata base-URI trailing-slash normalization via a shared helper and adopt it across MetadataOrchestrationOptions, ComickDirectApiClientOptions, FlaresolverrClientOptions, and OverrideCoverService URI resolution.
- [x] 2026-03-05 - Fix `OverrideCoverService` keyed-lock dispose race by synchronizing per-entry acquire/release/removal with `SyncRoot`/`IsRemoved` state and add repeated contention regression coverage to prevent semaphore disposal while in use.
- [x] 2026-03-05 - Prevent unbounded `OverrideCoverService` keyed cover-write lock growth by introducing ref-counted lock-entry reclamation with regression coverage for cleanup, cancellation-wait, and write-failure release paths.
- [x] 2026-03-05 - Change default `runtime.comick_search_max_results` from `100` to `4` and update default-dependent docs/test expectations.
- [x] 2026-03-05 - Add runtime setting `runtime.comick_search_max_results` (introduced as default `100`, `> 0`, later changed to `4`) and thread it through settings/defaults/self-heal/validation/runtime mapping into Comick search request URI `limit=` query composition with updated tests/docs.
- [x] 2026-03-05 - Stabilize integration test determinism by fixing entrypoint symlink-ownership fixtures to use container-visible symlink targets and widening transient polling-recovery timeout in Docker assertions behavior coverage.
- [x] 2026-03-04 - Add mandatory `tachiyomi=true` query parameter to all Comick API search/comic request URIs (direct + FlareSolverr-routed) and update metadata URL assertion coverage.
- [x] 2026-03-04 - Fix unit-test determinism by using temp mount paths for fsname assertion coverage and enforce per-destination serialized `cover.jpg` writes in `OverrideCoverService` with concurrent-race regression updates.
- [x] 2026-03-04 - Harden Comick endpoint path handling by enforcing relative non-root paths without query/fragment components across options + settings validation, and add regression tests for absolute/query/fragment/root path rejection.
- [x] 2026-03-04 - Add runtime-configurable Comick API and image hosts plus configurable search/comic endpoint paths, and wire those settings through defaults/self-heal/validation/options/runtime composition with metadata test updates.
- [x] 2026-03-01 - Address unresolved PR #44 AI review threads by updating metadata pacing/cache docs to implemented status, replacing state-store cache-prefix inference with explicit operation classification, splitting malformed-cache tests for intent clarity, and applying related style/comment cleanups with full-suite validation.
- [x] 2026-02-28 - Add runtime settings/schema/defaults/self-healing/validation support for `runtime.metadata_api_request_delay_ms` (default `1000`, `>= 0`) and `runtime.metadata_api_cache_ttl_hours` (default `24`, `> 0`), including strict-runtime and relaxed-tooling profile behavior.
- [x] 2026-02-28 - Extend `MergeMountWorkflowOptions` and `MetadataOrchestrationOptions` to carry metadata API pacing and cache TTL settings with constructor/mapping guards and focused option-mapping unit coverage.
- [x] 2026-02-28 - Normalize `FilesystemEventTriggerOptions.FromSettings` invalid `scan.watch_startup_mode` guard contract to field-specific `ParamName` (`watchStartupMode`) and align unit assertions.
- [x] 2026-02-28 - Introduce shared metadata request throttling abstractions (`IMetadataApiRequestThrottle`, implementation, and no-op implementation) with deterministic pacing/cancellation semantics and expected/edge/failure unit tests.
- [x] 2026-02-28 - Wire a single shared metadata request throttle instance into runtime composition (`DefaultRuntimeSupervisorRunner`) and flow it through Comick gateway and override cover service dependencies.
- [x] 2026-02-28 - Apply pacing to all live metadata HTTP requests (Comick direct, FlareSolverr-routed Comick calls, and cover download calls) while ensuring cache hits and artifact-exists short-circuit paths do not pause.
- [x] 2026-02-28 - Extend persisted metadata state (`MetadataStateSnapshot` + `FileBackedMetadataStateStore`) with backward-compatible Comick API return cache storage and deterministic malformed-state handling.
- [x] 2026-02-28 - Implement Comick gateway read-through/write-through response caching for search and comic endpoints with configurable TTL, caching stable outcomes (`Success`/`NotFound`) and excluding non-cacheable/transient outcomes.
- [x] 2026-02-28 - Add structured cache telemetry in Comick gateway (`hit`/`miss`/`persisted`/`non-cacheable skip`/state-store failure) with context and logging regression tests.
- [x] 2026-02-28 - Expand gateway, cover-service, and coordinator tests to verify: cache-hit no-delay behavior, cache-miss paced live calls, TTL expiry refresh behavior, malformed cache entry fallback, and non-regressive scan interruption semantics.
- [x] 2026-02-28 - Update integration test fixtures/config YAML baselines to include the new metadata pacing/cache settings so runtime bootstrap remains valid in Docker test paths.
- [x] 2026-03-01 - Align metadata cache-policy docs to implemented `Success`/`NotFound` behavior and harden tests with comic-endpoint cache parity plus deterministic concurrent-throttle serialization coverage.
- [x] 2026-03-01 - Remove timing-window flake risk from `MetadataApiRequestThrottleTests.ExecuteAsync_Expected_ShouldSerializeConcurrentOperations` by using deterministic synchronization assertions instead of fixed `Task.Delay` race checks.
- [x] 2026-03-01 - Apply metadata review follow-ups: atomic concurrent cover-handler call counting, explicit synchronization assertions in throttle serialization tests, and cache/style readability touchups.
