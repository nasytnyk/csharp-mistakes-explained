# 💥 exceptions

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### activator-hides-the-real-error (A5)

- **Twist:** Reflection wraps the constructor's exception in
  TargetInvocationException - so the catch block written for the real
  exception type never fires, and the retry logic retries the unretryable.
- **Mechanic:** `Activator.CreateInstance` and `MethodInfo.Invoke` wrap any
  exception thrown inside the invoked member in TargetInvocationException;
  the real error is `.InnerException`. A `catch (ValidationException)` around
  the reflective call is dead code. (`BindingFlags.DoNotWrapExceptions`
  exists for Invoke precisely because of this.)
- **Who hits it:** plugin loaders, convention-based factories, serializers
  and test harnesses - any code that constructs types reflectively and wants
  typed error handling around it.
- **Repro:** a type whose constructor throws InvalidOperationException;
  `catch (InvalidOperationException)` around CreateInstance does not fire;
  the TargetInvocationException escapes. Deterministic, no packages.
- **Damage:** typed recovery paths never execute; generic handlers retry
  permanently-broken plugins forever, or log "TargetInvocationException"
  while the actionable error hides one level deeper.
- **Verified:** documented wrapping behavior; verify at build.

### the-swallowed-filter (A5)

- **Twist:** An exception thrown inside a `when` filter is silently discarded
  and the filter counts as false - your catch just doesn't match, and nothing
  anywhere records why.
- **Mechanic:** exception filters run during the first pass of exception
  handling; if the filter itself throws, the runtime swallows the secondary
  exception and treats the filter as false. The original exception continues
  to outer handlers. The filter's own bug is unobservable by design - no log,
  no fail-fast, nothing.
- **Who hits it:** `catch (ApiException e) when (e.Code == config.RetryCode)`
  styles - filters touching config or state that can be null. The filter's
  NRE fires exactly and only when the exception it filters is in flight, i.e.
  exactly when you needed the handler.
- **Repro:** inner `throw new InvalidOperationException("original")`; a catch
  whose filter dereferences null; an outer catch that receives the *original*
  exception; print that the filter's NRE was observed nowhere. Deterministic,
  no packages.
- **Damage:** the retry/fallback path silently never triggers, and the reason
  is invisible in every log - among the most expensive classes of bug to
  diagnose in production.
- **😈 seed:** fear-ladder inversion: here the *crash* is the silent case - a
  filter that merely computed the wrong boolean could at least be read and
  debugged.
- **Verified:** ran on .NET 10 (2026-07-22): throwing filter treated as
  false, original exception reached the outer catch, filter's NRE
  unobservable.

### result-wraps-await-unwraps (A4,5)

- **Twist:** the same faulted task throws two different exception *types*
  depending on how you wait: `catch (TimeoutException)` fires around
  `await task` and silently misses around `task.Result` - because blocking
  wraps the failure in AggregateException and awaiting unwraps it.
- **Mechanic:** `.Result` and `.Wait()` throw AggregateException (the real
  error is `.InnerException`); `await` and `GetAwaiter().GetResult()` throw
  the original exception directly. So a typed catch written against one
  waiting style is dead code around the other - and refactors routinely
  swap `await` for `.Result` (or back) without touching the catch.
- **Who hits it:** sync-over-async call sites - constructors, property
  getters, `ISomething` implementations that can't be async - reaching for
  `.Result`/`.Wait()` under a `catch (SpecificException)` copied from the
  async path.
- **Repro:** one `async Task FailAsync()` that throws TimeoutException,
  awaited four ways: `.Result` and `.Wait()` surface AggregateException
  (typed catch missed), `await` and `GetAwaiter().GetResult()` surface
  TimeoutException (caught). Deterministic, no packages.
- **Damage:** typed recovery - retry, fallback, user-facing message -
  silently skipped on the blocking path; the generic handler logs
  "AggregateException" while the actionable type hides one level deeper.
- **ADJACENCY (curator):** same wrapping family as
  activator-hides-the-real-error (TargetInvocationException) - different
  API, same lie; and distinct from #0021 whenall-hides-exceptions, which
  is about *await* dropping all-but-first of *many* exceptions. This one is
  the single-exception type divergence between blocking and awaiting.
  Cross-link both.
- **😈 seed:** `GetAwaiter().GetResult()` - the "unwrap the aggregate" fix
  people reach for - unwraps a single exception but, on a WhenAll-style
  task with several, still hands you only the first, quietly reintroducing
  #0021's loss.
- **Verified:** ran on .NET 10 (2026-07-24): `.Result` and `.Wait()` threw
  AggregateException (inner TimeoutException); `await` and
  `GetAwaiter().GetResult()` threw TimeoutException.

### filter-side-effects-fire-anyway (A5)

- **Twist:** `catch (Exception e) when (Audit(e))` looks like it audits the
  exceptions this block handles - but the filter runs for exceptions it
  *doesn't* handle too: it fires even when it returns false, and before any
  `finally` unwinds, so the "audit on handling" logs handling that never
  happens.
- **Mechanic:** exception filters evaluate in the first pass of exception
  dispatch, before the stack unwinds and before finally blocks run - and
  they evaluate whether or not they ultimately match. A filter with a side
  effect (audit, increment, log, mutate) therefore executes for every
  exception that reaches the catch clause, including ones it rejects and
  ones an outer handler will take. Verified ordering: the filter ran, then
  the finally ran.
- **Who hits it:** filters doing real work - `when (LogAndCheck(e))`,
  `when (Metrics.Count(e) && e.Code == retryable)`, `when
  (Audit(e))` - written on the belief that the guard only runs for handled
  exceptions.
- **Repro:** a filter returning false whose body increments an audit
  counter: the counter advances though the catch body never runs and an
  outer catch takes the exception; a second case prints the filter running
  before the inner `finally`. Deterministic, no packages.
- **Damage:** double-counted metrics, audit entries for exceptions that
  were never handled here, and side effects ordered before cleanup that
  the author assumed ran after - a quietly wrong observability and control
  layer built on a mis-timed guard.
- **ADJACENCY:** the-swallowed-filter above is the other `when` lie (a
  *throwing* filter is swallowed and counts as false); this is the
  *side-effecting* filter firing when it shouldn't. Two exhibits, one
  feature - cross-link, keep distinct.
- **😈 seed:** it makes the filter a covert probe: put a side effect in a
  `when` that always returns false and it runs on every matching-type
  exception in the program while never handling one - observable behavior
  from a catch that, by its own verdict, does nothing.
- **Verified:** ran on .NET 10 (2026-07-24): filter side effect fired on a
  false verdict (outer catch took the exception), and ran before the inner
  finally.

### catch-all-hides-your-own-bug (A5)

- **Twist:** a broad `catch (Exception) { return fallback; }` swallows a
  `NullReferenceException` / `IndexOutOfRangeException` that is a *bug in your own
  code*, so a coding error silently becomes a "handled" business fallback -
  wrong result, green logs, no stack trace anywhere.
- **Mechanic:** `catch (Exception)` matches every exception, including the
  runtime ones your bugs throw (NRE, IndexOutOfRange, InvalidCast, your own
  ArgumentException). The handler cannot tell "the dependency failed" from "I
  have a typo," so a programming error is indistinguishable from an expected
  failure and takes the same fallback path (return 0 / empty / false).
- **Who hits it:** resilience-flavored try/catch wrapped around whole methods or
  requests - "if anything goes wrong, return default." The catch that was meant
  for a flaky dependency also eats the author's own bugs.
- **Repro:** a try block with a real bug (index past the end / null deref) under
  `catch (Exception)` returning a fallback; the fallback runs, the expected
  result never appears, and nothing logs the actual exception. Deterministic, no
  packages.
- **Damage:** bugs ship as silent fallbacks - a mispriced order returns 0, a
  permission check returns false, a parse returns default - and because the NRE
  never surfaced, it never gets fixed; only the wrong output remains.
- **😈 seed:** catch the *specific* exceptions the dependency documents, and let
  programming bugs crash (or at least log at Error with the stack); a catch-all
  that returns a fallback is a bug-hider, not resilience.
- **Verified:** language behavior (catch Exception matches NRE/IndexOutOfRange);
  verify the self-audit at build.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **Dead end (verified 2026-07-24, do not re-derive):** "a throw inside
  `finally` replaces the in-flight exception" IS shipped #0017
  (finally-that-lied) - its exact mechanic, confirmed against the README.
  Not a separate candidate.

- **exceptions:** rethrow across an await boundary (the stack is already
  rebuilt). Its former sibling here - "using swallows the body's exception
  when Dispose also throws" - is #0017's finally-replacement mechanic in
  sugar form (re-verified 2026-07-22); dropped as a duplicate of a shipped
  exhibit.
