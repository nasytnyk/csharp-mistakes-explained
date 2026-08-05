# ⚡ async

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

> All queued candidates were cleared 2026-08-05 at the curator's call
> ("викидай всі 7"), after a full review; see `rejected.md`. Only Seeds remain.
> The async hall stays open (10 exhibits shipped) - this just empties its
> proposal queue.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **async:** ValueTask must be awaited exactly once, immediately - a second
  await (or a stored one) over a pooled IValueTaskSource throws or returns
  another operation's data. Real and modern, but needs a deterministic
  pooled-source repro and a digestibility check before promoting.

- **async:** Monitor and Mutex are thread-affine - releasing after an await
  can throw SynchronizationLockException because the continuation changed
  threads. Deterministic via the inline-continuation technique; promote once
  framed below the ceiling.

- **async:** Task.Delay(0) completes synchronously and never yields, while
  Task.Yield always does - "give others a turn" written with Delay(0) does
  nothing. Probably a 😈 section inside another exhibit, not a standalone.

- **async:** Task.WhenAny leaves the losing tasks' exceptions unobserved -
  real, but needs a deterministic observation technique before promoting.
