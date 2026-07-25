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

### use-after-return (A2,5)

- **Twist:** Return an array to ArrayPool and keep writing through your old
  reference - the pool has already handed that array to the next renter,
  and your writes land inside their data. Use-after-free, managed edition.
- **Mechanic:** Return transfers ownership back to the pool; the next Rent
  from the same bucket hands out the same instance (the thread-local bucket
  makes that immediate and deterministic). Nothing invalidates the old
  reference: the array stays GC-alive the whole time, so no exception is
  even *possible* - the reference is valid, only the ownership is gone, and
  no type-system marker tracks ownership.
- **Who hits it:** perf-conscious code adopting ArrayPool: Return in a
  finally while a captured lambda or async continuation still holds the
  buffer; or a double Return on a retry path.
- **Repro:** Rent(100), Return, Rent(100): ReferenceEquals is true; write
  through the stale reference; the new owner's buffer shows the byte.
  Deterministic, no packages.
- **Damage:** cross-request data bleed - one request's bytes appear inside
  another's payload. A privacy incident, not just corruption, and there is
  no exception at any point to anchor an investigation.
- **😈 seed:** a double Return puts the same array into the pool twice -
  two future renters then share one buffer while each believes it is
  exclusively theirs.
- **Verified:** ran on .NET 10 (2026-07-22): same instance re-rented, stale
  write visible to the new owner.

### the-oversized-rental (A5)

- **Twist:** ArrayPool.Rent(100) returns 128 bytes - and they arrive still
  warm with the previous renter's data: two contract breaks in one call,
  both silent.
- **Mechanic:** Rent promises *at least* the requested length, rounding up
  to the bucket size; and it does not clear the array (clearing on Return
  is opt-in). Code migrated from `new byte[n]` keeps trusting
  `buffer.Length` - foreach, Array.Copy, Write(buffer, 0, buffer.Length) -
  and now processes the tail; and the tail is not zeros, it is another
  request's bytes.
- **Who hits it:** any `new T[n]` -> `Rent(n)` migration where the buffer's
  Length flows into a loop, a serializer, or a network write.
- **Repro:** Rent(100).Length == 128; plant a sentinel byte, Return without
  clearing, Rent again: the sentinel is still there for the "new" buffer.
  Deterministic, no packages.
- **Damage:** payloads padded with stale bytes from other requests go over
  the wire - corrupt at best, a data leak at worst; hashes computed over
  Length stop matching anything.
- **😈 seed:** `Return(buffer, clearArray: true)` exists and almost nobody
  passes it - safety is opt-in, silence is the default (the encoding
  exhibits' recurring moral).
- **Verified:** ran on .NET 10 (2026-07-22): 128 bytes for Rent(100), the
  previous renter's sentinel visible.

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

### the-cache-that-owns-its-keys (A6)

- **Twist:** A Dictionary that attaches metadata to objects owns those
  objects forever - "I only stored notes *about* the entity", and every
  annotated entity just became immortal.
- **Mechanic:** dictionary keys are strong references: as long as the
  annotation store lives (and static caches live forever), every key object
  is rooted, plus everything it references. The entity's logical lifetime
  ends; its memory never does. ConditionalWeakTable exists precisely for
  attach-metadata scenarios - its entries die with their keys.
- **Who hits it:** static annotation/metadata/lookup stores keyed by domain
  objects - "remember validation state for this entity" - a shape that
  reads as caching and behaves as a leak.
- **Repro:** a helper annotates a fresh object in a `Dictionary<object,
  string>` and returns only a WeakReference: after forced GC it is alive;
  the identical helper over a ConditionalWeakTable: collected. Both
  branches in one run, deterministic, no packages.
- **Damage:** memory grows with every entity ever annotated; the "cache"
  retains the full object graphs of dead requests - the leak is the cache
  working exactly as written.
- **😈 seed:** the memory profiler shows objects retained by a Dictionary -
  which looks precisely like a healthy cache, so the leak survives the
  investigation too.
- **Verified:** ran on .NET 10 (2026-07-22): Dictionary key immortal, CWT
  key collected.

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

### large-array-born-in-gen2 (A6)

- **Twist:** `new byte[100_000]` is born in generation 2 - a fresh, just-now
  allocation the GC treats as old - so it survives the gen-0 collections that
  reclaim everything else, and a short-lived big buffer lingers until the next
  rare full GC.
- **Mechanic:** allocations >= 85,000 bytes go on the Large Object Heap, which
  is collected only during a gen-2 (full) GC. `GC.GetGeneration` reports 2 for
  the large array and 0 for a small one at the same age. So a dead large array
  is *not* freed by the frequent gen-0/gen-1 collections; only a full GC
  reclaims it - the opposite of the "allocate, drop, it's gone" intuition that
  holds for small objects.
- **Who hits it:** short-lived large buffers churned in a request/loop - image
  and file byte[], large `List`/`StringBuilder` backing stores, big
  serialization buffers - allocated and discarded fast, expecting gen-0 to
  sweep them; instead they pile up between full GCs (and the LOH is not
  compacted by default, so the space fragments).
- **Repro:** `GC.GetGeneration(new byte[100_000])` is 2, `new byte[1_000]` is
  0; weak-ref both from a NoInlining helper, drop the strong refs,
  `GC.Collect(0)` - the small one is dead, the large one is still alive;
  a full `GC.Collect()` finally frees it. Deterministic, no packages.
- **Damage:** memory that the profiler shows "leaking" between full GCs even
  though every large buffer is dead - allocation rate looks fine, working set
  climbs, and the "leak" vanishes each time a full GC happens, so it reads as
  a phantom.
- **😈 seed:** the size threshold is invisible in code - one row wider, one
  field longer, and a buffer that lived in gen 0 for years silently crosses
  85,000 bytes into the LOH, changing its GC lifetime with no code change at
  the allocation site.
- **Verified:** ran on .NET 10 (2026-07-24): gen 2 vs gen 0 for the two
  arrays; after GC.Collect(0) the small array was reclaimed and the large one
  survived; a full GC.Collect() reclaimed it.

### finalizer-delays-gc (A6)

- **Twist:** giving a class a finalizer *keeps its objects alive longer*: the
  finalizer does not run during the collection that finds the object dead - it
  is queued and run later - so the object, and everything it references,
  survives that GC and needs a second one to actually free.
- **Mechanic:** an object with a finalizer is put on the finalization queue
  when detected unreachable; the finalizer thread runs it afterwards, and only
  a subsequent GC reclaims the memory. Proof via a finalizer flag: after the
  first `GC.Collect()` the flag is still false (finalizer not yet run, object
  survived); after `GC.WaitForPendingFinalizers()` it is true; a second GC
  reclaims it. A *plain* object of the same shape is gone after the first GC.
  BUILDER NOTE: a *short* WeakReference reports the finalizable object dead
  immediately (it tracks root-reachability, not the queue) - use a
  `trackResurrection: true` (long) WeakReference or the finalizer flag to
  observe the survival honestly. NoInlining helper so the local doesn't pin it.
- **Who hits it:** classes that add a finalizer "to be safe" (or inherit one
  through a SafeHandle/unmanaged wrapper) and hold large managed graphs -
  every such object costs two GC cycles and a finalizer-thread hop to reclaim,
  and a slow finalizer backs the whole queue up.
- **Repro:** a `~Tracked()` sets a static flag; after one `GC.Collect()` the
  flag is false (survived), after `WaitForPendingFinalizers` true; a long
  WeakReference is alive after the 1st GC and dead after finalize + 2nd GC,
  while a plain object dies on the 1st. Deterministic, no packages.
- **Damage:** memory pressure and promotion from objects that "should be
  gone" - finalizable objects get promoted to the next generation while they
  wait, so they live longer and collect in rarer, more expensive GCs; a
  finalizer that blocks stalls finalization for everyone.
- **NOTE (curator):** the two-collect + WeakReference technique also appears
  in disposal's the-cleanup-that-never-came (which proves the *opposite* -
  no finalizer means Dispose never runs). Same instrument, opposite lesson;
  cross-link, keep distinct.
- **😈 seed:** `GC.SuppressFinalize(this)` in Dispose exists precisely to undo
  this cost - so forgetting it in a Dispose pattern silently keeps the
  two-GC penalty even for objects you disposed promptly.
- **Verified:** ran on .NET 10 (2026-07-24): finalizer flag false after the
  1st GC and true after WaitForPendingFinalizers; long WeakReference alive
  after 1st GC, dead after finalize + 2nd GC; plain object dead after 1st GC.

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
