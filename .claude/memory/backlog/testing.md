# 🧪 testing

> Status: **opened** (2026-08-06, by #0048 collection-assert-is-ordered). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### static-state-leaks-between-tests (A6,5)

- **Twist:** the suite is green; run the same two tests in the other
  order and it's red - a static field written by one test is still there
  for the next, and the failure lands on the *innocent* test.
- **Mechanic:** statics are process-wide and runners execute many tests
  per process, so any static write without teardown becomes an input to
  whoever runs later. Order dependency is the deterministic face
  (A,B green; B,A red - both orders verified in one run); under
  parallel-by-default runners the same leak wears a schedule and reads
  as flakiness.
- **Who hits it:** static config knobs, caches, service locators touched
  by tests - stable for months until a new test, a rename, or a runner
  upgrade reshuffles discovery order.
- **Repro:** a mini-runner executes {A: expects clean state,
  B: sets a static surcharge and asserts it} in both orders in one
  process: A,B both pass; B,A leaves A failing. Deterministic, no
  packages - the mechanic belongs to the language, not a framework.
- **Damage:** the failure attaches to the wrong test: A fails, B leaked -
  so the investigation, quarantine, or deletion hits the innocent one
  and the leak survives its own cleanup.
- **😈 seed:** refactors that touch zero logic - renaming a class,
  adding an unrelated test - reshuffle execution order and "break the
  build": git blame pins it on the rename, which is technically true and
  completely wrong.
- **Verified:** ran on .NET 10 (2026-07-24): order A,B green; order B,A
  left A failing on the leaked surcharge.

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
