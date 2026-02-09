# AGENT_INDEX Prompt

Use this prompt whenever you update `AGENT_INDEX.yml` after code changes.

```text
You are maintaining docs/AGENT_INDEX.yml for this Unity + C# repository.

MISSION
Keep AGENT_INDEX.yml accurate and curated as the code evolves. Prevent duplicate implementations by pointing to canonical entrypoints and extension points.

HARD OUTPUT RULES
- Output ONLY valid YAML (no markdown fences, no commentary).
- Do NOT invent symbols, methods, or paths.
- Every added or modified symbol must be verified by opening the defining file and finding the declaration or signature.
- Use relative paths only.

DIFF-FRIENDLY FORMATTING (MUST)
- Sort modules by id alphabetically.
- Sort paths alphabetically.
- Sort entrypoints by symbol alphabetically.
- Sort extension_points by how alphabetically.
- Sort do_not_reimplement alphabetically (within each module).
- Prefer single-line YAML; avoid multiline blocks.
- Keep summary <= 120 chars.

CURATION / QUALITY BAR
- 8-20 modules total (do not expand into a type dump).
- 2-6 entrypoints per module.
- At least 10 total do_not_reimplement bullets across the file.
- Favor canonical roots: bootstraps, services/managers, registries/factories, pipelines, serializers, network bridges, mod loaders, editor tools.
- Only update modules impacted by the current change unless there is a major refactor.

UNITY/C# FOCUS AREAS (IF PRESENT)
- Bootstrapping: initial scene, entry MonoBehaviours, composition root, DI/service locator.
- Domain systems: top gameplay/business systems and canonical services/managers.
- Persistence: save/load pipeline, serializers, migrations, snapshot formats/versioning.
- Networking: transport, message types/registries, replication/sync, thread boundaries and main-thread bridge.
- Modding/content: assetbundle pipeline, registries, validation, mod loading.
- Editor tooling: custom windows, build scripts, importers, validators, menu items.
- UI: uGUI/UI Toolkit setup, navigation/state management, presenters/view-model patterns if any.
- Testing: test locations, harnesses/helpers, how tests run.
- Logging/diagnostics/config: central logging, config loading, feature flags.

TWO-PASS WORKFLOW (MANDATORY)
PASS 1 - IMPACT ANALYSIS + DRAFT UPDATE
1) Read the current docs/AGENT_INDEX.yml.
2) Inspect the current code change (git diff / changed files list / PR description if available).
3) Identify which modules are impacted:
   - If new folders or systems appear, add a new module (kebab-case id).
   - If symbols moved or renamed, update paths or symbols and remove stale references.
   - If a new extension point exists, add it (how + symbols + paths).
   - If a change increases duplication risk, add or update a do_not_reimplement bullet pointing to the canonical implementation.
4) Keep changes minimal and localized to impacted modules.

PASS 2 - VERIFY EVERYTHING YOU TOUCHED
For every entry you added or modified (and every symbol referenced in extension_points or do_not_reimplement that you touched):
- Open the defining file and confirm the symbol exists exactly as spelled.
- Confirm the path is correct and is the defining location.
- If referencing a method, confirm it exists in that type or file.
- If any verification fails, REMOVE that entry (do not guess a replacement).

FINAL CHECKS BEFORE OUTPUT
- Confirm sorting rules are applied.
- Confirm schema remains intact and required keys exist.
- Confirm no placeholders like <todo>, <unknown>, or <guess>.
- Confirm the result is still curated (8-20 modules, 2-6 entrypoints per module).
- Output the final YAML only.

SCHEMA (MUST MATCH EXACTLY; do not add new top-level keys)
version: 1
language: csharp
unity:
  unity_version: <string or omit if unknown>
  asmdef_paths:
    - <relative path to .asmdef>
maintenance:
  update_when:
    - <short bullets>
  generation_notes:
    - <short bullets about repo structure and verification approach>
conventions:
  - <short bullets: architecture/threading/layering constraints you detect>
modules:
  - id: <kebab-case, unique>
    summary: <1-2 lines, <=120 chars>
    tags: [<short tags>]
    paths:
      - <top-level folders or key subfolders for this module>
    entrypoints:
      - symbol: <TypeOrFunctionOrFileName>
        kind: <class|struct|interface|enum|method|function|file|scene|asset>
        path: <relative path where defined>
        purpose: <one line>
    extension_points:
      - how: <one line describing how to extend>
        symbols: [<symbol names involved>]
        paths: [<relative paths involved>]
    do_not_reimplement:
      - <specific capability + canonical symbol>
    related: [<module ids>]
```
