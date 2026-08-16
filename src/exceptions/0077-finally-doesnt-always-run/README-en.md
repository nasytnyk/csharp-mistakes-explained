---
id: "0077"
title: finally doesn't always run
category: exceptions
tags: [exceptions, finally, Environment.Exit]
rule: "never put must-run cleanup only in `finally` - `Environment.Exit` and `FailFast` **skip it**"
---

# #0077 - finally Doesn't Always Run

## 💥 Symptom

The one guarantee everyone leans on - "no matter how this block exits, the
`finally` runs" - quietly is not absolute. A batch flushes its audit log, closes
its file, releases its lock in a `finally`, and one day the cleanup simply does
not happen: no exception, no error, and an exit code of `0` that reads as
success. The run left no trace, and nothing points at why.

## 🔍 The Offending Code

```csharp
void RunBatch()
{
    try
    {
        // ... process records ...
        Environment.Exit(0); // 💥 terminates the process here - the finally never runs
    }
    finally
    {
        FlushAuditLog(); // "guaranteed" cleanup - skipped
    }
}
```

## 🧠 What's Actually Going On

`finally` is guaranteed only against the exits the language controls: falling off
the end, `return`, `break`, `continue`, and an exception unwinding the stack. For
all of those, the runtime walks the stack and runs each pending `finally` on the
way out. `Environment.Exit`, `Environment.FailFast`, an unhandled
`StackOverflowException`, a fast-fail from the runtime, or the OS killing the
process are **not** stack unwinds - they tear the process down where it stands,
and pending `finally` blocks are simply abandoned. The stack is never walked, so
nothing on it runs.

The broken belief is "`finally` always runs, so cleanup there is safe." It always
runs for *managed control flow*. `Environment.Exit` looks like a tidy "stop the
program" call, but it is not a `return` - it is an immediate process teardown, and
every `finally` between it and `Main` is skipped. What makes it sting is the
contrast: process-exit hooks like `AppDomain.ProcessExit` *do* fire on
`Environment.Exit` (this exhibit uses one as its auditor), so the mechanisms you
trusted least run while the `finally` you trusted most does not.

## ✅ The Fix

Abort through managed control flow - `return` or throw - so the stack actually
unwinds and the `finally` runs.

```csharp
void RunBatch()
{
    try
    {
        // ... process records ...
        return; // unwinds normally - the finally below still runs
    }
    finally
    {
        FlushAuditLog(); // runs on every managed exit path
    }
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| `return` / throw instead of `Environment.Exit` | You are deep in the call stack and want to stop - unwind normally so every `finally` on the way out runs; set the exit code once at the top. |
| Flush *before* you exit | You genuinely must call `Environment.Exit` (a CLI abort path) - do the durable work first, then exit, never after. |
| `AppDomain.ProcessExit` for last-resort cleanup | The cleanup must survive even `Environment.Exit` - a ProcessExit handler runs on it, but it is best-effort, time-boxed, and does not fire on a hard crash. |
| Don't make `finally` the only copy | The state matters (money, audit) - make the operation restartable or transactional, so a skipped flush is recoverable rather than lost. |

## 😈 The Even Worse Sibling

`Environment.Exit` at least is a call you wrote and can grep for. The silent
siblings are the ones no line of your code names. An unhandled
`StackOverflowException` is uncatchable *and* skips every `finally` as the runtime
fails fast - deep or accidental recursion wipes out your cleanup with no exception
you could have caught. `Environment.FailFast` (and the runtime's own fail-fast on
heap corruption or a failed `ExecutionEngine` invariant) does the same by design,
because the whole point of fail-fast is to *not* run more managed code. And a
process killed by the OS, an OOM-killer, or a power loss never gets the courtesy
of unwinding at all. Every one of them leaves your "guaranteed" `finally`
unexecuted, and the ones that hurt most are exactly the ones you cannot see in the
source.

## 🎓 Advanced Nuance

- **`ProcessExit` is not a `finally` replacement.** It fires on `Environment.Exit`
  and on normal termination, but it is time-limited (the runtime caps how long
  handlers may run), it does *not* fire on `FailFast` or a hard crash, and an
  exception thrown inside it is swallowed. It is a best-effort backstop, not a
  guarantee.
- **`finally` runs for an *unhandled* exception, but not always visibly.** When an
  exception is unhandled, the runtime still unwinds and runs `finally` blocks
  before terminating - so a plain `throw` is safe for cleanup, unlike
  `FailFast`/`Exit`. The distinction is unwind (runs `finally`) versus fail-fast
  (does not).
- **A `finally` that itself throws or exits is another way to lose cleanup.** If
  the `finally` body calls `Environment.Exit`, or throws (see
  [0017-finally-that-lied](../0017-finally-that-lied/)), later `finally` blocks
  further out are skipped too - the same "the stack stopped unwinding" failure,
  one level up.

## 🔎 How to Find It in Your Codebase

- Grep for `Environment.Exit`, `Environment.FailFast`, and `Process.Kill` inside
  or beneath a `try` that owns a `finally` - any cleanup pending on that stack is
  skipped when the call fires.
- Audit "fail fast" and "graceful shutdown" helpers: if they call
  `Environment.Exit` while requests, flushes, or transactions are in flight, the
  `finally` blocks meant to close them out do not run.
- Symptom-side: missing audit records, unflushed logs, or unreleased locks that
  correlate with the process ending on an *error* path, while the happy path
  cleans up fine; exit codes that say success on runs that left work half-done.
- Put durable, must-happen work (commit, flush, release) before the exit call or
  in a restartable/transactional form - never solely in a `finally` that a
  process teardown can walk past.
