# TASKS.md


- [x] 2026-02-28 - Add runtime settings/schema/defaults/self-healing/validation support for `runtime.metadata_api_request_delay_ms` (default `1000`, `>= 0`) and `runtime.metadata_api_cache_ttl_hours` (default `24`, `> 0`), including strict-runtime and relaxed-tooling profile behavior.
- [ ] 2026-02-28 - Extend `MergeMountWorkflowOptions` and `MetadataOrchestrationOptions` to carry metadata API pacing and cache TTL settings with constructor/mapping guards and focused option-mapping unit coverage.
- [ ] 2026-02-28 - Introduce shared metadata request throttling abstractions (`IMetadataApiRequestThrottle`, implementation, and no-op implementation) with deterministic pacing/cancellation semantics and expected/edge/failure unit tests.
- [ ] 2026-02-28 - Wire a single shared metadata request throttle instance into runtime composition (`DefaultRuntimeSupervisorRunner`) and flow it through Comick gateway and override cover service dependencies.
- [ ] 2026-02-28 - Apply pacing to all live metadata HTTP requests (Comick direct, FlareSolverr-routed Comick calls, and cover download calls) while ensuring cache hits and artifact-exists short-circuit paths do not pause.
- [ ] 2026-02-28 - Extend persisted metadata state (`MetadataStateSnapshot` + `FileBackedMetadataStateStore`) with backward-compatible Comick API return cache storage and deterministic malformed-state handling.
- [ ] 2026-02-28 - Implement Comick gateway read-through/write-through response caching for search and comic endpoints with configurable TTL, caching stable outcomes (`Success`/`NotFound`/`HttpFailure`/`CloudflareBlocked`/`MalformedPayload`) and excluding `TransportFailure`/cooperative `Cancelled`.
- [ ] 2026-02-28 - Add structured cache telemetry in Comick gateway (`hit`/`miss`/`persisted`/`non-cacheable skip`/state-store failure) with context and logging regression tests.
- [ ] 2026-02-28 - Expand gateway, cover-service, and coordinator tests to verify: cache-hit no-delay behavior, cache-miss paced live calls, TTL expiry refresh behavior, malformed cache entry fallback, and non-regressive scan interruption semantics.
- [ ] 2026-02-28 - Update integration test fixtures/config YAML baselines to include the new metadata pacing/cache settings so runtime bootstrap remains valid in Docker test paths.
