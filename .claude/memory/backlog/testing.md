# 🧪 testing

> Status: **opened** (2026-08-06, by #0048 collection-assert-is-ordered). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

> Chosen candidate queue exhausted 2026-08-06: 2 shipped (#0048 collection-assert-is-ordered,
> #0049 static-state-leaks-between-tests), 2 rejected (assert-equal-floats-no-tolerance
> "redundant with numbers", async-void-test-always-passes "async void already taboo";
> see `rejected.md`). Only the dead-end Seed remains below.

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
