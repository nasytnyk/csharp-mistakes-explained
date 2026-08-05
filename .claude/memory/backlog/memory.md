# 💾 memory

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-closure-that-held-everything (A6)

- **Twist:** A lambda that captured one small int keeps a 100 MB array alive
  - because the compiler put every captured variable of the scope into one
  shared closure object, and your little callback owns all of it.
- **Mechanic:** the compiler generates one display class per scope; all
  variables captured by *any* lambda in that scope live on it. A long-lived
  delegate that captured only `retryCount` also roots the giant buffer a
  neighboring lambda captured. Restructuring the scopes (or copying to
  locals in a nested block) breaks the tie.
- **Who hits it:** event handlers and callbacks registered inside methods
  that also touched large data - upload handlers, report generators.
- **Repro:** BUILDER DETAIL: the scope needs a second lambda that touches
  the big array (even one that is created and immediately dropped) - that
  is what forces the array into the shared display class; the kept "small"
  lambda then roots it. WeakReference to the big array; keep only the small
  lambda; forced GC: alive. Same code with the scopes split (big used
  without any lambda capturing it): collected. Both branches in one run,
  deterministic, no packages.
- **Damage:** memory leaks with no reference to the big object anywhere in
  user code - the retained path exists only in compiler-generated classes,
  where nobody looks.
- **Verified:** ran on .NET 10 (2026-07-22): shared-scope lambda kept the
  1 MB array alive, split-scope twin let it die.

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

### the-stack-that-only-grows (A6)

- **Twist:** stackalloc inside a loop never frees per iteration - stack
  memory dies at method exit, not scope exit - so the loop marches straight
  into an uncatchable StackOverflowException.
- **Mechanic:** stackalloc bumps the stack pointer; the language scopes the
  *span variable*, not the memory. Each iteration allocates fresh bytes
  below the last; nothing is reclaimed until the method returns.
  StackOverflowException cannot be caught: the process dies. Analyzer
  CA2014 warns about exactly this - as a warning, so the code compiles and
  ships.
- **Who hits it:** parsing/formatting loops adopting stackalloc for
  per-item buffers - the natural "fast version" refactor of
  `new byte[1024]` inside a loop.
- **Repro:** a method looping `Span<byte> b = stackalloc byte[1024]` 200k
  times dies with "Stack overflow." after roughly a thousand iterations
  (1 MB default stack). The demo IS the crash - make it the whole Bad.cs,
  nothing can run after it. Deterministic, no packages.
- **Damage:** process death invisible to try/catch, unhandled-exception
  hooks, and graceful shutdown - and since the fatal iteration count
  depends on stack size, small tests pass while production batches die.
- **😈 seed:** the fix - hoist the stackalloc above the loop - changes no
  call site and no visible behavior: the diff is unreviewable unless you
  already know the rule.
- **Verified:** ran on .NET 10 (2026-07-22): process died with "Stack
  overflow." mid-loop; CA2014 fired at compile time.

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
