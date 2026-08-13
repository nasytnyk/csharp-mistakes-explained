# 🏷️ enums

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **the-alias-that-never-prints** - `enum Status { Active = 1,
  Enabled = 1 }`: Enabled.ToString() prints "Active" (verified
  2026-07-24) - an alias member exists for source compatibility but
  never appears in logs, string-serialized JSON, or GetValues-built
  dropdowns (GetValues returns the duplicate). Which name wins is
  documented as unspecified. Needs a damage framing (audit logs,
  external API contracts?) before promoting.
