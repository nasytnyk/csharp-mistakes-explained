# 🧪 testing

> Status: **opened** (2026-08-06, by #0048 collection-assert-is-ordered). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

> Queue closed 2026-08-06: 1 shipped (#0048 collection-assert-is-ordered),
> 3 rejected (assert-equal-floats-no-tolerance, async-void-test-always-passes,
> static-state-leaks-between-tests; see `rejected.md`). Only the dead-end Seed remains.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **Dead end (verified 2026-07-24, do not re-derive):** "Assert.Throws
  with an async lambda silently passes" - not in xunit 2.9:
  Assert.Throws&lt;T&gt;(Func&lt;Task&gt;) is marked [Obsolete] as a
  compile *error* (CS0619 pointing at ThrowsAsync), so both
  `() => FailAsync()` and `async () => ...` shapes fail the build. The
  surviving flavor of the trap was the Action-bound async-void shape,
  which was itself rejected 2026-08-05 (async-void-test-always-passes -
  "async void is already taboo"); see `rejected.md`.
