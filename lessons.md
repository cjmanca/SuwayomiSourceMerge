# Lessons

- 2026-02-17: When hardening `docker/entrypoint.sh` around merged ownership, never reintroduce recursive `chown` under `/ssm/merged`; stale FUSE endpoints must be handled as warning-only, best-effort operations.
- 2026-02-17: For non-root mergerfs startup failures (`fusermount ... Operation not permitted`), verify runtime identity access to `/dev/fuse` explicitly; directory ownership alone is not sufficient.
- 2026-02-18: When exception telemetry is needed for runtime crashes, log `exception.ToString()` (not just type/message) so stack traces and inner-exception chains are preserved in structured logs.
