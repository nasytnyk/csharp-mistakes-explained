# 💾 memory

> Status: **opened** (2026-08-05, by #0038 the-closure-that-held-everything). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

> Candidate queue exhausted 2026-08-05: 3 shipped (#0038 closure, #0039 stack,
> #0040 span), 5 rejected (see `rejected.md`). Only Seeds remain.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **memory:** a blocking finalizer stalls the process's single finalizer
  thread, freezing ALL finalization - real, but the in-file proof needs a
  timing-free observation technique before promoting.

- **memory:** List.Clear keeps the backing buffer (Capacity unchanged;
  TrimExcess is the fix) - deterministic, but the title spoils the finale;
  needs a genuine twist before it clears the bar.

- **memory:** the JIT may collect an object while its own instance method
  is still running (GC.KeepAlive exists for exactly this) - a genuine
  "wait, WHAT?", but it reproduces only under Release codegen; find a
  pinned-configuration technique for file-based dotnet run first.

- **memory:** a WeakReference checked and then used after a collection -
  race-shaped; promote only with a hard deterministic assertion.
