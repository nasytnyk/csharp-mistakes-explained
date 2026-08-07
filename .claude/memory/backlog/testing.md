# 🧪 testing

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### collection-assert-is-ordered (A4)

- **Twist:** Assert.Equal on two collections with the same members fails
  because the order differs - and whether "equal" even *means* ordered
  depends on the runtime type: the same items in HashSets pass the same
  assertion.
- **Mechanic:** Assert.Equal over sequences compares element-by-element
  in order; set types are special-cased to set semantics (verified:
  arrays fail, HashSets of the same items pass). Assert.Equivalent is
  the order-insensitive spelling. The sibling trap: Assert.Equal on two
  field-identical DTOs without Equals fails on reference equality while
  Equivalent passes - "equal" in one assert library is several different
  relations, selected by types.
- **Who hits it:** asserting results of GroupBy, Dictionary iteration,
  parallel processing, or SQL without ORDER BY - membership correct,
  order incidental - red the day a runtime upgrade or hash seed shuffles
  iteration order.
- **Repro:** three strings as arrays vs as HashSets through Assert.Equal
  (fail / pass), Equivalent (pass), plus the identical-DTO pair
  (fail / pass). xunit.assert in a console file, deterministic.
- **Damage:** red builds after infrastructure-only changes - and the
  observed fix is adding OrderBy to *production* code, a sort nobody
  asked for, to appease a test that asserted more than the requirement.
- **😈 seed:** a type refactor silently weakens the suite: change the
  production return from List to HashSet and the same assertion flips
  from ordered to set-wise - still green, now checking less.
- **Verified:** ran on .NET 10 (2026-07-24), xunit.assert 2.9.3: arrays
  failed, HashSets passed, Equivalent passed both, identical DTOs failed
  Equal and passed Equivalent.

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
