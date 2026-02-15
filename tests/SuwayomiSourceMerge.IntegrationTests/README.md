# Integration Test Project

- Run all tests (includes integration tests): `dotnet test`
- Run integration tests only: `dotnet test tests/SuwayomiSourceMerge.IntegrationTests/SuwayomiSourceMerge.IntegrationTests.csproj`

These tests build and run the repository Docker image and require a reachable Docker daemon.
The suite intentionally fails when Docker is unavailable.

The container runtime path is validated using mocked external tools mounted under `/ssm/mock-bin`,
so CI does not require privileged FUSE mounts for mergerfs behavior.
