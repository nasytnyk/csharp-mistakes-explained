# ⚡ async

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-collected-timer (A6)

- **Twist:** A Timer with no stored reference gets collected mid-run; the
  "every minute" job stops with no error, no exception, no log line.
- **Mechanic:** `System.Threading.Timer` does not root itself. If the only
  reference is a local in the method that created it, the timer is garbage as
  soon as that reference dies, and its callbacks simply stop after the GC
  runs. Nothing observable fails at the moment it happens.
- **Who hits it:** "schedule a heartbeat/cleanup in Main or a constructor and
  ignore the return value". Survives all day on a dev machine (little GC
  pressure), dies quietly in production.
- **Repro:** create the timer inside a `[MethodImpl(MethodImplOptions.NoInlining)]`
  helper and do not store the returned reference (this matters: file-based
  `dotnet run` builds Debug, where locals stay alive to end of method - the
  helper method's return is what frees the timer). Then `GC.Collect()` +
  `GC.WaitForPendingFinalizers()`, wait two ticks' worth via a
  CountdownEvent on a *stored* control timer, and show the abandoned timer's
  counter stopped while the stored one kept counting. Forced GC keeps it
  deterministic. No packages.
- **Damage:** the recurring job - billing sweep, queue pump, heartbeat -
  silently stops. Discovered days later by absence, the hardest kind of
  evidence.
- **Verified:** documented GC-root behavior; the forced-GC repro is standard.
  Verify at build.

### the-cached-failure (A1,5)

- **Twist:** Lazy&lt;Task&gt; caches the task, not the value: one transient
  failure and every caller after it receives the same stale exception until
  the process restarts.
- **Mechanic:** the Lazy factory runs once; its return value - the Task
  *object* - is cached forever. If that task faults, the fault is now the
  cached "value": every subsequent await observes the same exception, and the
  factory never runs again. Identical trap with `ConcurrentDictionary<K,
  Task<V>>` caches.
- **Who hits it:** async caches - `Lazy<Task<Config>>`, task-per-key
  dictionaries - the textbook "share one flight between concurrent callers"
  pattern, minus failure eviction.
- **Repro:** a `Lazy<Task<string>>` whose factory counts invocations and
  throws; await it three times; the factory ran once and all three awaits got
  the same exception. Deterministic, no packages.
- **Damage:** a 2-second network blip at 09:00 becomes an outage lasting until
  someone restarts the process. The dependency is healthy; your cache
  re-serves the corpse of its one failure.
- **😈 seed:** health checks stay green - they probe the dependency, not your
  cache.
- **Verified:** ran on .NET 10 (2026-07-22): 3 awaits, 1 factory call, same
  cached exception each time.

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

### the-double-wrapped-task (A4,1)

- **Twist:** Task.Factory.StartNew with an async lambda returns
  Task&lt;Task&gt;: awaiting it waits for the work to *start*, not finish -
  the await completes instantly, the work is unfinished, the exceptions are
  nobody's.
- **Mechanic:** StartNew knows nothing about async delegates: it runs the
  lambda, and an async lambda "returns" at its first await - handing back
  the real Task as a *result*. Awaiting the outer shell observes only "the
  lambda started". `Task.Run` exists precisely because of this: it unwraps
  automatically. One method name apart, opposite meaning.
- **Who hits it:** code cargo-culting StartNew "because it takes options",
  or predating Task.Run; someone adds `async` to the lambda during a
  refactor and every await of the result quietly stops meaning anything.
- **Repro:** `Task.Factory.StartNew(async () => { await gate; flag = true; })`;
  await the outer - flag is still false; only `Unwrap()`/awaiting the inner
  task observes the real work. Gate makes it deterministic, no packages.
- **Damage:** "completed" batches with zero work done and inner-task
  exceptions unobserved - the same lie as exhibit #0031
  (parallel-foreach-swallows-async) wearing a more respectable API.
- **Verified:** ran on .NET 10 (2026-07-22): outer task completed with the
  work provably not done.

### threadlocal-doesnt-follow (A6)

- **Twist:** ThreadLocal state does not follow the code across an await:
  the method resumes on another thread and the "per-request" cache is
  suddenly empty - or, worse, holds a different request's data.
- **Mechanic:** in a console/server app an await captures no thread
  affinity; the continuation runs wherever the scheduler puts it.
  ThreadLocal and [ThreadStatic] belong to the physical thread, so the
  async flow walks away from its own state - and the next unrelated work
  item scheduled onto the OLD thread inherits it. AsyncLocal is the
  flow-following twin; its own trap is `asynclocal-never-flows-up` below.
- **Who hits it:** per-request ambient state written pre-async and still
  running: current user, current tenant, thread-keyed caches and buffers.
- **Repro:** BUILDER WARNING - the naive `await Task.Yield()` demo does NOT
  guarantee a thread hop (verified live: it happily resumed on the same
  pool thread). Deterministic technique: set the ThreadLocal on a dedicated
  `new Thread` which starts the async method (it runs synchronously to the
  first await, then the thread *exits*); complete the gate from the main
  thread. The continuation cannot run on a dead thread, so the hop is
  guaranteed, and the ThreadLocal reads its default after the await. No
  packages.
- **Damage:** the empty-state case is the lucky one; the unlucky one is
  cross-request bleed - tenant A resumes on a pool thread still warm with
  tenant B's ThreadLocal. Correctness and privacy, fully silent.
- **Verified:** ran on .NET 10 (2026-07-22) with the dedicated-thread
  technique; the Task.Yield version was tried and rejected by that same
  run.

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

### the-linked-leak (A6)

- **Twist:** CreateLinkedTokenSource hooks your per-request token to the
  app-lifetime token - forget Dispose and the app token holds that hook
  forever: the request's object graph outlives the request, by design.
- **Mechanic:** a linked CTS registers a callback on its parent token; that
  registration roots the linked CTS - and everything its own registrations
  capture - until the parent dies or the linked CTS is disposed. With a
  process-lifetime parent (shutdown/app token), "forgot Dispose" means
  "leaks until restart", one request at a time.
- **Who hits it:** the per-request timeout pattern -
  `CreateLinkedTokenSource(appStoppingToken)` + CancelAfter - dropped
  without `using`: a famous slow-leak shape in long-running services.
- **Repro:** a NoInlining helper creates a linked CTS over a long-lived
  parent, registers a callback capturing a payload, returns a WeakReference
  to the payload; forced GC: payload alive without Dispose, collected with
  it - both branches in one run. Deterministic, no packages.
- **Damage:** memory grows request by request through a retained path that
  runs entirely inside framework registration lists - the profiler shows no
  reference from user code; the "restart cures it" leak.
- **😈 seed:** those forgotten registrations all *fire* at shutdown -
  thousands of stale per-request callbacks executing at the worst possible
  moment.
- **Verified:** ran on .NET 10 (2026-07-22): payload rooted while
  undisposed, collected once disposed.

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
- **COORDINATION:** threadlocal-doesnt-follow (above) is the
  physical-thread twin and links here. Two broken models: the thread
  doesn't follow the flow / the flow doesn't report back up.
- **😈 seed:** the workaround people find - return the value and reassign
  in the caller - dies the day one more async layer appears in between;
  the only stable pattern is writing the AsyncLocal at the top of the
  flow, and nothing enforces that.
- **Verified:** ran on .NET 10 (2026-07-24): sync write persisted; async
  write visible inside, gone in the caller; sync-completing async write
  also gone.

### the-uncancellable-stream (A5)

- **Twist:** `await foreach (... in Stream().WithCancellation(token))`
  reads like a cancellable loop - and the token goes precisely nowhere:
  without an [EnumeratorCancellation] parameter on the iterator,
  WithCancellation is a silent no-op.
- **Mechanic:** WithCancellation hands the token to GetAsyncEnumerator;
  the compiler-generated enumerator forwards it only into a parameter
  marked [EnumeratorCancellation] - and the body must still consume it
  (ThrowIfCancellationRequested or pass it to awaits). A parameterless
  iterator discards the token with no warning and no exception: the loop
  runs to natural completion however hard the caller cancels. (A token
  parameter *without* the attribute at least draws CS8425; the
  parameterless shape draws nothing at all - verified clean build.)
- **Who hits it:** consumers of IAsyncEnumerable APIs - streaming
  queries, event feeds, paging wrappers - adding WithCancellation for
  shutdown or timeout and trusting the name; and library authors who
  forget the attribute, shipping uncancellable streams to every caller.
- **Repro:** parameterless 10-item iterator, cancel after item 2: all 10
  items arrive, no exception. Same loop over an
  [EnumeratorCancellation] + ThrowIfCancellationRequested iterator:
  OperationCanceledException after 3. Deterministic, no packages.
- **Damage:** graceful shutdown that isn't - the drain loop keeps
  consuming a stream it "cancelled", holds the process past its deadline,
  and gets killed hard mid-item; stream timeouts that never fire.
- **😈 seed:** the API splits the contract so both sides are right and
  the pair is still wrong: the caller correctly wrote WithCancellation,
  the author correctly shipped a warning-free iterator - and the piece
  that connects them is one attribute whose absence no call site can
  see.
- **Verified:** ran on .NET 10 (2026-07-24): 10 of 10 items after cancel
  with the parameterless iterator, cancelled after 3 with the attributed
  one.

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
