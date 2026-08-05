# 💾 memory

> Status: **opened** (2026-08-05, by #0038 the-closure-that-held-everything). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-span-left-behind (A3)

- **Twist:** CollectionsMarshal.AsSpan hands you the list's buffer - then
  one Add grows the list onto a new buffer, and your span keeps reading and
  writing the abandoned one. Both sides stay perfectly happy.
- **Mechanic:** List&lt;T&gt; grows by allocating a bigger array and
  copying; a span taken earlier still points at the old array. The span
  accepts writes (into garbage), the list never sees them, and unlike
  modify-while-enumerating (#0001) no versioning guard exists on this path -
  the span API is the "I know what I'm doing" door.
- **Who hits it:** performance code using CollectionsMarshal spans over
  lists while anything else may append - the aliasing silently breaks at an
  unrelated line ("it was just one Add").
- **Repro:** `new List<int>(4) { 1, 2, 3, 4 }`; AsSpan; Add(5) forces the
  reallocation; `span[0] = 99`; `list[0]` is still 1 while `span[0]` reports
  99. Deterministic, no packages.
- **Damage:** lost writes and stale reads that begin exactly when data
  volume crosses the capacity threshold - correct in every small test,
  wrong at scale, and never an exception.
- **😈 seed:** the abandoned buffer cannot be collected while the span's
  holder lives - the "zero-allocation optimization" now retains two copies
  of the data.
- **Verified:** ran on .NET 10 (2026-07-22): write through the span
  invisible to the grown list.

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
