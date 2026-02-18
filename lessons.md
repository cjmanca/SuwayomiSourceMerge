# Lessons

- 2026-02-17: When hardening `docker/entrypoint.sh` around merged ownership, never reintroduce recursive `chown` under `/ssm/merged`; stale FUSE endpoints must be handled as warning-only, best-effort operations.
- 2026-02-17: For non-root mergerfs startup failures (`fusermount ... Operation not permitted`), verify runtime identity access to `/dev/fuse` explicitly; directory ownership alone is not sufficient.
- 2026-02-18: When exception telemetry is needed for runtime crashes, log `exception.ToString()` (not just type/message) so stack traces and inner-exception chains are preserved in structured logs.
- 2026-02-18: With default `logging.level: warning`, startup can appear idle unless failure/diagnostic warnings exist; emit warning-level diagnostics for zero-source and zero-desired-mount merge passes to avoid silent misconfiguration states.
- 2026-02-18: Container shutdown behavior must be validated with `SIGTERM` (not only `SIGINT`), because `docker stop` sends `SIGTERM` and can bypass cleanup unless explicitly intercepted cooperatively.
- 2026-02-18: Keep startup/shutdown and high-level workflow progress on a default-visible log band (`normal`) so long-running maintenance phases are observable without enabling verbose debug logs.
