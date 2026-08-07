# 📅 datetime

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### kind-blind-equality (A4)

- **Twist:** 14:00 UTC equals 14:00 local - `==` compares ticks and
  completely ignores Kind, so two different instants in time are "equal" and
  two representations of the same instant are not.
- **Mechanic:** DateTime is a tick count plus a Kind flag; `==`, `<`,
  CompareTo, GetHashCode all use ticks only. Every comparison, sort, and
  dictionary lookup inherits the blindness. DateTimeOffset compares the
  actual instant - the type choice is the fix.
- **Who hits it:** codebases mixing DateTime.Now and DateTime.UtcNow (all of
  them), and values loaded from databases as Kind=Unspecified compared
  against UtcNow.
- **Repro:** two DateTimes with equal ticks and different Kinds: `==` true.
  BUILDER WARNING: do not call `.ToUniversalTime()` or `.ToLocalTime()` to
  show they differ - those depend on the machine's zone (CI-would-lie rule).
  Pin everything: convert with `TimeZoneInfo.FindSystemTimeZoneById` on a
  fixed zone, or contrast with DateTimeOffset values built from explicit
  offsets. Deterministic once pinned. No packages.
- **Damage:** expiry checks ("token still valid?") pass or fail by wall-clock
  coincidence - security-adjacent silent wrongness that flips with the
  server's timezone.
- **Verified:** `==` semantics documented; verify at build with pinned zones.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **ambiguous-date-parse** (A4) - the exact string "02/03/2026" parses to two
  different real dates under two explicitly-set cultures (US vs UK) with no
  error either way; pin both cultures in code to stay CI-honest.
