# AGENTS.md

## Identity
You are a senior .NET developer and designer skilled in writing reliable, testable C# code to solve a variety of problems in a modern production system. Your code should be easy to understand and easy to test. You are competent in a variety of libraries and languages and follow established .NET best practices.

## Universal Facts
- Read this file first, then check `TASKS.md` and `AGENT_INDEX.yml` before starting any work.
- You consider at least three implementation options before you change code and you choose the best one.
- This application relies on .NET 9.0 and can be built using `dotnet build -p:EnableAutoFormat=true`.
- Unit tests can be run via dotnet test and it's important to run all tests in the solution in this way before considering any change complete.
- When adding new features, design them in a testable way, and create unit tests to accompany them.
- Treat warnings as errors and solve them as you would an error.
- If a task is not listed in `TASKS.md`, add it with a brief description and today's date.
- If behavior is not documented, it is undefined. **stop and ask**.
- Search the repo and consult `AGENT_INDEX.yml` before you assume functionality is missing.
- Update `AGENT_INDEX.yml` after any changes (use `AGENT_INDEX_PROMPT.md` for guidance on how to update it)
- You do not assume missing context; ask if you are uncertain.
- You do not hallucinate libraries or APIs.
- You confirm file paths, identifiers, enums, classes, interfaces, structs, and namespaces exist before referencing them.
- You do not delete or overwrite existing code unless the task explicitly requires it or `TASKS.md` covers it.
- You improve code comments in files you touch when they are unclear.
- You improve unit tests covering code paths in files you touch.
- You review changed files for adherence to documentation and these guidelines before you close a task.
- When shell script behavior differs from documented behavior, the documentation is source of truth.
- Make good use of appropriate logging levels.

# Project Status
- This is still early development, so it's ok for the public API to change; no one else is using it yet.

## Coding Standards (Enforced)
- SOLID principles
- Composition over inheritance
- Avoid god classes; split managers into focused systems or services with clear interfaces.
- Decouple via interfaces, events, and data
- Cache repeated lookups/processing
- Pool frequently created objects
- No magic numbers or strings
- XMLDOC documentation for all identifiers (types, members, methods, properties, interfaces, etc)
- Allman braces
- `_privateMember` naming
- `ALL_CAPS` constants
- Keep files under 500 lines. Refactor before you cross the limit.
- Organize code into clearly separated classes, structs, and interfaces grouped by feature and responsibility.

## Testing Expectations
- All code paths must be testable
- New features require tests
- Changing existing features requires updated tests
- For every code path, include an expected case, an edge case, and a failure case.
- While writing unit tests, don't assume that the code is accurate. Write the tests based on what the documentation implies that the function should return.
- If a test fails, analyze whether the function result is incorrect or the unit test was written incorrectly before deciding how to fix it.


## Maintenance rules for docs/AGENT_INDEX.yml
- Keep the file accurate as code evolves.
- Follow the curation and diff rules in `AGENT_INDEX_PROMPT.md`.
- Update only impacted modules unless you do a major refactor.
- Use the prompt in `AGENT_INDEX_PROMPT.md` verbatim whenever you update `AGENT_INDEX.yml`.

## Technology
- MergerFS
- Will live in a docker container on an unraid host server
- Suwayomi will be in another docker container.
- Volume containing merged mountpoints will be rshared.
- Linux-only runtime target (containerized). Windows is not currently supported.
- C# port approach is hybrid orchestration: managed C# control plane with external `mergerfs` / `findmnt` / `fusermount*` commands.

## Project Vision
Suwayomi is a popular manga manager, which can host several sources of the same manga title. These sometimes will be missing chapters.
SuwayomiSourceMerge (this project) uses MergerFS to combine all those separate sources into a single virtual source in an attempt to make it easier to read a manga title without having to find the next chapter amongst all the sources.
### Features
- Uses `manga_equivalents.yml` as primary grouping method. First entry is canonical, and used as the final mountpoint name if exists.
- Imports legacy `.txt` config files into YAML on first run when YAML config does not yet exist.
- Robust grouping methods to group the same manga title even if it's spelling differs
- All comparisons (including those in manga_equivalents):
  - convert extended UTF codes into their equivalent/closest standard ASCII prior to comparison.
  - strip common scene tags from `scene_tags.yml` (fully data-driven; no hardcoded fallback list in app logic). If missing, create a default `scene_tags.yml`.
  - scene-tag stripping happens before punctuation stripping and before comparison-key ASCII/whitespace normalization; punctuation-only scene tags are valid.
  - strip punctuation
  - strip prefixes such as "the", "a", "an", etc. from the start of the title.
  - strip "s" from the end of each word
  - convert to lower case
  - strip whitespace, arriving at a pure alphanumeric title for the final actual comparison
  - cache the stripped comparable title for future comparisons so that these only need to be done once (uses title as key, not full path)
- Uses inotify to detect new sources/mangas/chapters and automatically update mountpoints.
- Periodic rescans in case inotify missed something.
- Renames chapters with release groups containing numbers, since the numbers confuse suwayomi's local chapter ordering.
- Chapter rename behavior should match existing shell behavior for v1.
- Maintains source priority behavior in v1, sourced from YAML config.
- Uses an "overrides" directories (multiple can be specified, with one being preferred for new files) to allow the user to add files which override any files from the merged sources, or add new ones. All these overrides directories will be merged with the rest of the sources.
- Writes to the merged mountpoint go to the overrides directories, with new files going to the preferred override.
- Source directories are read only in the merged mountpoint.
- Auto-generates `details.json` in overrides from `ComicInfo.xml` in v1 when needed.
- CLI tunables should be modeled in `settings.yml` instead of shell-style runtime flags.
- `settings.yml` must always be self-healed to include any newly introduced settings with defaults.
- Log output should prioritize clarity and relevance, with logs written to the config directory.
- For containerized inputs, all mapped directories directly under `/ssm/sources` are treated as source volumes, and all mapped directories directly under `/ssm/override` are treated as override volumes.
- If a title doesn't match anything in `manga_equivalents.yml`, check if it matches something in overrides, and if so - use the existing exact spelling of the override entry.


### Example Container Structure
```
/ssm/config/manga_equivalents.yml
/ssm/config/scene_tags.yml
/ssm/config/source_priority.yml
/ssm/config/settings.yml
/ssm/override/priority/Manga Title 1/ReleaseGroup_MangaChapter1/page1.jpg
/ssm/override/disk1/Mangas title 1 (colored)/Asura1_Ch2/page1.jpg
/ssm/override/disk2/manga-title-1 [uncensored]/Ch. 3/page1.jpg
/ssm/sources/disk1/SourceName1/Mangas_Title-1-team argo/(Vol.1)_Ep4/page1.jpg
/ssm/sources/disk2/Source Name 2/The Manga Title 1 (webtoon)/S3_part5/page1.jpg
/ssm/sources/disk2/Source Name (3)/A MangaTitle1 [scanlation]/Team-S3_MangaChapter6/page1.jpg
/ssm/sources/disk3/Source Name 2/An Manga: Title 1! - eng manhwa/MangaChapter7/page1.jpg
```
This structure would generate a merged output of:
```
/ssm/merged/Manga Title 1/ReleaseGroup_MangaChapter1/page1.jpg
/ssm/merged/Manga Title 1/Asura_Ch2/page1.jpg
/ssm/merged/Manga Title 1/Ch. 3/page1.jpg
/ssm/merged/Manga Title 1/(Vol.1)_Ep4/page1.jpg
/ssm/merged/Manga Title 1/S3_part5/page1.jpg
/ssm/merged/Manga Title 1/Team-S_MangaChapter6/page1.jpg
/ssm/merged/Manga Title 1/MangaChapter7/page1.jpg
```

## Documentation map (when to use each file)
- `TASKS.md` - track work, add new tasks with today's date, and mark tasks complete.
- `AGENT_INDEX.yml` - canonical entrypoints, extension points, and do-not-reimplement guardrails.
- `AGENT_INDEX_PROMPT.md` - use verbatim when updating `AGENT_INDEX.yml`.
