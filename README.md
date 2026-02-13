This application relies on .NET 9.0 and can be built using dotnet build. Unit tests can be run via dotnet test and it's important to run all tests in the solution in this way before considering any change complete.
Mutation testing for configuration code is configured at `tests/SuwayomiSourceMerge.UnitTests/stryker-config.json` (target threshold break: 80).
When adding new features, design them in a testable way, and create unit tests to accompany them.


Requirements baseline for the C# port:
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
- `details.json` auto-generation from `ComicInfo.xml` is included in v1.
- Runtime discovery in container mode treats all directories directly under `sources` as source volumes and under `override` as override volumes.
- Runtime settings come from `settings.yml`; missing settings must be auto-added with defaults.
- Logging should prioritize clarity and relevance and write to the config directory.
- Docker-based integration tests are acceptable for v1.
- All permissions on created files/directories will be set the same, based on either the docker container PUID/PGID environment variables or equivalent in command switches if not running in a container.

Configuration schema reference: `docs/config-schema.md`.
