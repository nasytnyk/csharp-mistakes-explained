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
