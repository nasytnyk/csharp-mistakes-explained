# ⚡ async

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-timeout-that-stopped-nothing (A1,5)

- **Twist:** The classic WhenAny timeout pattern reports "timed out" and
  walks away - the abandoned work keeps running, its charge lands a second
  later, and its exception has no one left to crash.
- **Mechanic:** `Task.WhenAny(work, Task.Delay(t))` completes when the
  first task does; the loser is not cancelled - nothing even tries to stop
  it. The caller logs a timeout and usually retries, while the original
  work finishes anyway (double side effect) or faults (unobserved
  exception). "Timeout" in this pattern means "I stopped watching", not
  "it stopped happening".
- **Who hits it:** the standard timeout idiom around payments, HTTP calls,
  and "if it takes more than 5s, retry" logic - one of the most-pasted
  async snippets in existence.
- **Repro:** gate the work with a TaskCompletionSource; let
  Task.CompletedTask play the Delay that already expired; WhenAny declares
  timeout while the side-effect counter is 0; open the gate - the counter
  hits 1 *after* "timed out" was reported. Deterministic, no packages.
- **Damage:** retry-after-timeout doubles the charge: the "timed out"
  operation succeeded too, so reconciliation finds one order paid twice -
  silent money damage from a snippet everyone trusts.
- **😈 seed:** the loser's exception surfaces minutes later as
  TaskScheduler.UnobservedTaskException - a crash report pointing at
  nothing (cross-link #0019, #0021).
- **Verified:** ran on .NET 10 (2026-07-22): charge landed after the
  timeout verdict was already printed.

### the-self-deadlock (A4)

- **Twist:** SemaphoreSlim is the async replacement for lock - minus
  reentrancy: the method takes the "lock", calls a helper that takes it
  again, and the code waits forever for the permit it is holding.
- **Mechanic:** `lock`/Monitor are reentrant per thread; SemaphoreSlim(1,1)
  - the standard async mutex - has no ownership concept at all, so a nested
  WaitAsync in the same logical flow blocks on itself. Migrating locked
  code to async silently deletes reentrancy from the contract, and the
  compiler forbidding `await` inside `lock` is what pushes everyone onto
  SemaphoreSlim in the first place.
- **Who hits it:** codebases converting synchronized code to async: a
  guarded public method calls another guarded public method - a call graph
  that was legal for years under lock.
- **Repro:** SemaphoreSlim(1,1); WaitAsync; a nested
  `WaitAsync(TimeSpan.FromMilliseconds(200))` returns false - the
  self-deadlock, proven without hanging the demo (same technique as
  semaphore-never-released). Deterministic, no packages.
- **Damage:** a production hang with zero CPU, no exception, no log entry -
  the process just stops answering on the one code path where the nested
  call occurs.
- **😈 seed:** the reentrant path can hide behind a feature flag or a rare
  branch for months - the deadlock ships long before it fires.
- **Verified:** ran on .NET 10 (2026-07-22): nested WaitAsync timed out
  while the permit was held.

### the-hijacked-completion (A6,5)

- **Twist:** TaskCompletionSource.SetResult is not a notification - it
  synchronously runs every waiting continuation on YOUR thread before
  returning: the "signal" line just executed foreign code inside your
  critical section.
- **Mechanic:** by default, completing a TCS runs attached continuations
  inline on the completing thread. An innocuous `tcs.SetResult(value)`
  while holding a lock (or any mid-flight invariant) reenters arbitrary
  awaiter code right there: reentrancy, deadlocks, and stack dives under
  completion chains. `TaskCreationOptions.RunContinuationsAsynchronously`
  is the one-argument axiom fix.
- **Who hits it:** infrastructure code - hand-rolled async queues, caches,
  pub-sub - anywhere a producer completes a TCS that consumers await.
- **Repro:** an async consumer awaits tcs.Task and records its thread id;
  SetResult from the main flow; the recorded id equals the setter's, and
  print ordering shows the consumer ran *inside* the SetResult call.
  Deterministic, no packages.
- **Damage:** the producer "signals" while holding a lock; the awakened
  consumer takes the same lock - reentrancy corrupting state (same
  thread), or instant deadlock (SemaphoreSlim). Production hangs traced to
  a line that looks incapable of blocking.
- **😈 seed:** CancellationToken.Register callbacks are the same trap -
  Cancel() runs them inline too.
- **Verified:** ran on .NET 10 (2026-07-22): continuation executed inside
  SetResult, on the setter's thread.

### the-eager-throw (A4)

- **Twist:** Delete the "pointless" async keyword from a one-line method
  and exceptions change their address: validation now throws at the call
  site, not at the await - and the tasks you had already collected are
  abandoned mid-flight.
- **Mechanic:** an async method routes *every* exception - including one
  thrown before the first await - into the returned task. A non-async
  Task-returning method throws synchronously at the call. Identical
  success-path behavior, different failure address; nothing in the
  signature reveals which one you are calling.
- **Who hits it:** `var tasks = items.Select(x => client.SendAsync(x)).ToList();
  await Task.WhenAll(tasks);` - if SendAsync validates eagerly (elided
  form), one bad item throws during ToList: the try/catch around WhenAll
  never runs, and the requests already started are left running unobserved
  (#0019's damage, reached through a different broken model).
- **Repro:** two methods with identical bodies, one `async`, one not; call
  both with a bad argument: the elided one throws at the call, the
  keyworded one returns a task with IsFaulted true. Then the Select/WhenAll
  shape to show the abandoned in-flight work. Deterministic, no packages.
- **Damage:** error handling sits in the reviewed-and-approved wrong place;
  half a batch runs unobserved after the "handled" crash.
- **Verified:** ran on .NET 10 (2026-07-22): call-site throw vs faulted
  task, exactly as described.

### asynclocal-never-flows-up (A3,4)

- **Twist:** the helper sets the AsyncLocal "current tenant" and returns -
  and the caller still sees the old value: ambient async state flows down
  the call tree, never up. Delete the `async` keyword from the same method
  and the write suddenly sticks.
- **Mechanic:** AsyncLocal lives in the ExecutionContext; an async method
  runs under a captured copy and the caller's context is restored when it
  returns, so writes inside are edits to a private copy. A sync callee
  runs on the caller's context and its write persists. The verified
  triple: sync callee sticks; async-with-await evaporates; and - the evil
  one - an async method that completes *synchronously* (await
  Task.CompletedTask, no thread hop anywhere) ALSO evaporates. The
  keyword alone, not any actual concurrency, decides the write's fate.
- **Who hits it:** ambient-context infrastructure - tenant, correlation
  id, current-user setters called as helpers: `await SetTenantAsync(id)`
  compiles, runs, logs the new value inside, and changes nothing for the
  next line of the caller. Also the refactor direction: making a sync
  context-setter async "for consistency" silently breaks every caller.
- **Repro:** one AsyncLocal, three callees with the same body (sync,
  async + Yield, async + CompletedTask); print the caller's view after
  each: sticks, evaporates, evaporates. Deterministic, no packages.
- **Damage:** requests processed under the wrong tenant or correlation
  id - the setter provably ran, so the investigation trusts it; the write
  worked everywhere except where anyone looks.
- **COORDINATION:** its physical-thread twin threadlocal-doesnt-follow was
  rejected (see `rejected.md`, "doesn't happen in real code"), so this
  AsyncLocal candidate now stands alone.
- **😈 seed:** the workaround people find - return the value and reassign
  in the caller - dies the day one more async layer appears in between;
  the only stable pattern is writing the AsyncLocal at the top of the
  flow, and nothing enforces that.
- **Verified:** ran on .NET 10 (2026-07-24): sync write persisted; async
  write visible inside, gone in the caller; sync-completing async write
  also gone.

### trywrite-drops-silently (A7,5)

- **Twist:** the producer ported ConcurrentQueue.Enqueue to
  Channel.Writer.TryWrite - same one-liner shape, except Enqueue was void
  and TryWrite returns the bool that says "I did not take your message":
  on a full bounded channel, every ignored false is a lost order.
- **Mechanic:** a bounded channel must do something at capacity; TryWrite
  never waits - it returns false and the *caller* drops the message by
  ignoring the return. Only WriteAsync waits for room. Muscle memory from
  void Enqueue/Add makes the return value invisible, and nothing warns
  about discarding it. The BoundedChannelFullMode options decide who
  loses on overflow; TryWrite-with-ignored-return loses the newest,
  invisibly.
- **Who hits it:** producers feeding bounded pipelines - log shippers,
  telemetry, order queues - refactored from unbounded collections; the
  bound was usually added "for safety" during a load incident, quietly
  converting backpressure into loss.
- **Repro:** CreateBounded&lt;string&gt;(1); three TryWrites with ignored
  returns - the consumer receives exactly one item; the returns printed
  are True/False/False; the WriteAsync loop delivers 3 of 3.
  Deterministic, no packages (System.Threading.Channels is in-box).
- **Damage:** loss that only happens under load: the channel is full
  precisely when traffic peaks, so the busiest minutes are the ones with
  holes - producer logs and consumer effects diverge with no error on
  either side.
- **😈 seed:** adding the bound is what armed it: the unbounded original
  never dropped anything (it just grew), so the "pure hardening" PR that
  added capacity traded a visible OOM someday for invisible loss today.
- **Verified:** ran on .NET 10 (2026-07-24): 1 of 3 delivered with
  ignored TryWrite returns (True/False/False), 3 of 3 with WriteAsync.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **async:** ValueTask must be awaited exactly once, immediately - a second
  await (or a stored one) over a pooled IValueTaskSource throws or returns
  another operation's data. Real and modern, but needs a deterministic
  pooled-source repro and a digestibility check before promoting.

- **async:** Monitor and Mutex are thread-affine - releasing after an await
  can throw SynchronizationLockException because the continuation changed
  threads. Deterministic via the inline-continuation technique proven in
  the-hijacked-completion; promote once framed below the ceiling.

- **async:** Task.Delay(0) completes synchronously and never yields, while
  Task.Yield always does - "give others a turn" written with Delay(0) does
  nothing. Probably a 😈 section inside another exhibit, not a standalone.

- **async:** Task.WhenAny leaves the losing tasks' exceptions unobserved -
  real, but needs a deterministic observation technique before promoting.
