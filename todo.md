# Docker Publish CI Plan

- [x] Evaluate implementation options and choose approach.
- [x] Add task tracking entry in `TASKS.md`.
- [x] Add GitHub Actions workflow to build/publish Docker image to GHCR.
- [x] Update `AGENT_INDEX.yml` for impacted module docs.
- [x] Run full test verification (`dotnet test`).
- [x] Add review notes and completion status.

## Implementation Options Considered

1. Use raw `docker build` and `docker push` shell commands in GitHub Actions.
2. Use `docker/build-push-action` with `docker/metadata-action` for managed tags/labels.
3. Use a reusable workflow or package/release-driven pipeline for image publication.

## Selected Approach

Selected option 2 for minimal maintenance overhead, standard GHCR best practices, and deterministic tagging behavior without custom scripting.

## Review

- Added a new GitHub Actions workflow at `.github/workflows/docker-publish.yml` with GHCR login, metadata tagging, buildx caching, and publish on push/tag/manual dispatch.
- Updated repo tracking/docs artifacts (`TASKS.md` and `AGENT_INDEX.yml`) for the new workflow.
- Verification: `dotnet test` passed (`809` unit tests and `11` integration tests).

## Branch Publish Follow-up Plan

- [x] Evaluate options for branch-wide build triggers and branch tag naming.
- [x] Update workflow to run for all branches and emit clear branch-prefixed tags.
- [x] Re-run full verification (`dotnet test`) and record result.

### Branch Options Considered

1. Keep main/master-only publishing and rely on manual dispatch for branch tests.
2. Publish all branch pushes with default branch-name tags from metadata action.
3. Publish all branch pushes with explicit `branch-` prefixed tags for clarity.

### Follow-up Selection

Selected option 3 to make branch images unambiguous in GHCR while still retaining tag/sha/latest tagging behavior.

### Follow-up Review

- Updated `.github/workflows/docker-publish.yml` to trigger on all branch pushes (`"**"`).
- Updated branch image tags to use `branch-` prefix via metadata action branch ref tags.
- Verification rerun: `dotnet test` passed (`809` unit tests and `11` integration tests).
