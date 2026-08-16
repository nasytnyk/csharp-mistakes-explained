# ⚙️ configuration

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### duplicate-key-crashes-config-load (A5)

- **Twist:** the same key twice in one `appsettings.json` does NOT "last wins" -
  the JSON provider throws `InvalidDataException` at startup and the app won't
  boot, and the message names the *file*, not the offending key.
- **Mechanic:** the JSON configuration provider rejects duplicate property names
  in a section; a merge-conflict artifact or a copy-pasted block that repeats a
  key makes `AddJsonFile(...).Build()` fail with "Failed to load configuration
  from file '...'", the real cause (the duplicate) buried in the inner exception.
- **Who hits it:** hand-edited appsettings after a merge, generated config, or a
  pasted section - especially large files where the two copies of the key are not
  on the same screen.
- **Repro:** a two-line JSON with a repeated key; `AddJsonFile(path).Build()`
  throws `InvalidDataException`. Deterministic, only needs Configuration.Json.
- **Damage:** total startup failure on deploy, with a message pointing at the
  file rather than the key - the investigation looks at paths/permissions, not
  the duplicate.
- **Verified:** ran on .NET 10 (2026-08-16): duplicate key -> `InvalidDataException`
  "Failed to load configuration from file '...'".

## Seeds

Not yet a full candidate - brainstorm before proposing.
