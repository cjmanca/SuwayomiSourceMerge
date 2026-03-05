# TASKS.md


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
