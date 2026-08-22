# 🔔 events

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### invoke-race-on-null-check (A1)

- **Twist:** `if (E != null) E(...)` - the last subscriber unsubscribes
  between the check and the call: NullReferenceException from an event that
  "was just checked".
- **Mechanic:** delegates are immutable; subscribe/unsubscribe swap the field.
  Check-then-invoke reads the field twice, and the value can change between
  the reads. `E?.Invoke(...)` (or copy-to-local) reads once - that single
  read is the entire fix.
- **Who hits it:** any event raised while subscribers come and go: UI
  teardown, service shutdown, plugin unload.
- **Repro:** determinism note for the builder - do NOT race two threads in a
  loop (nondeterministic per-iteration; the timing ban applies). Stage the
  interleaving single-threaded instead: perform the null check, then
  unsubscribe the last handler (this is the other thread's step, frozen in
  time), then perform the invoke from the field - NRE, 100% reproducible.
  It is honest because the staged interleaving is exactly what the two-thread
  version does when it loses.
- **Damage:** shutdown-time crashes that reproduce monthly in production and
  never on a dev machine.
- **Verified:** language-level; staged repro chosen to satisfy determinism.
  Verify at build.

### the-handler-that-subscribed-twice (A5)

- **Twist:** subscribing the same handler more than once makes the event fire
  it once *per subscription* - a `+=` in a method that runs twice (Init,
  reconnect, navigation) silently doubles every side effect: two emails, two
  charges, two audit rows.
- **Mechanic:** an event is a multicast delegate; `+=` appends the handler each
  time with no dedup, so the invocation list holds N copies and one `Raise`
  calls it N times. `-=` removes only ONE matching entry, so a single
  unsubscribe does not undo the duplicates. No compiler warning.
- **Who hits it:** components re-initialized or re-shown - WPF/WinForms controls,
  Blazor `OnInitialized`, reconnect/refresh handlers, DI transients that
  subscribe in their constructor - where the subscribe line runs more than once.
- **Repro:** subscribe the same handler twice, raise once, count the runs == 2.
  Deterministic, packageless.
- **Damage:** duplicated side effects (double notify/charge/write) that are hard
  to spot because each handler is individually correct - only the *count* is
  wrong, and it grows with each re-init.
- **ADJACENCY:** cross-links shipped #0023 unremovable-lambda (a `-=` with a
  fresh lambda removes nothing) and #0083 raising-an-event-with-no-subscribers;
  distinct - here the handler runs too *many* times, not too few.
- **😈 seed:** the "fix" of one `-=` still leaks if subscribed 3x; and
  unsubscribing a *different* lambda instance removes nothing (#0023).
- **Verified:** ran on .NET 10 (2026-08-16): two `+=`, one Raise -> handler ran
  twice.
