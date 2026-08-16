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
