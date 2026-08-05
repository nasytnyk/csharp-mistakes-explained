---
id: "0037"
title: Task.Factory.StartNew with an async lambda
category: async
tags: [async, Task.Factory.StartNew, Task.Run]
rule: "never launch an **async** lambda with `Task.Factory.StartNew` - use `Task.Run`"
---

# #0037 - Task.Factory.StartNew with an Async Lambda

## 💥 Symptom

Startup logs `Cache warmed. Serving traffic.` and the service goes live - onto an
empty cache. Every request is a cache miss, or worse, reads a price that is not
there yet and hands the customer a `0.00` or a `NullReferenceException`. The
warmup task completed, was awaited, returned - and did nothing. There is no
error, no failed task, no warning; the code even looks careful, because it
*awaited* the background work before serving. It just awaited the wrong thing.

## 🔍 The Offending Code

```csharp
Task warmup = Task.Factory.StartNew(async () => await WarmCacheAsync());
await warmup;  // 💥 this returns the instant the lambda *starts*, not when it finishes

Console.WriteLine("Cache warmed. Serving traffic.");   // cache is still empty
```

## 🧠 What's Actually Going On

`Task.Factory.StartNew` predates `async`/`await` and knows nothing about async
delegates. Handed an `async` lambda, it sees a `Func<Task>`: a delegate that
*returns a `Task`*. It runs the lambda and completes as soon as the delegate
returns a value - and an async lambda "returns" at its first suspending `await`,
handing back the still-running inner `Task` as its result. So `StartNew` gives
you a `Task<Task>`: the outer task represents "the lambda started"; the inner
task, buried as the outer's `Result`, is the work you actually care about.

Awaiting the outer task therefore waits only for the lambda to *reach its first
await* - microseconds - not for `WarmCacheAsync` to finish. `await warmup`
returns immediately, the cache is empty, and the log cheerfully says otherwise.

The tell is hidden by a legal upcast. `Task.Factory.StartNew(async () => ...)` is
typed `Task<Task>`, but assigning it to a `Task` variable is a fine widening
conversion, and awaiting a `Task<Task>` as a `Task` is perfectly legal - so the
compiler says nothing. Write `var warmup = ...` and the type `Task<Task>` is
right there; the moment you write `Task warmup = ...`, the second `Task`
disappears and takes the bug with it. This is the same lie
[0031-parallel-foreach-swallows-async](../../async/0031-parallel-foreach-swallows-async/)
tells through `async void`, wearing a more respectable API.

## ✅ The Fix

Use `Task.Run`. It exists precisely for this: its `Func<Task>` overload *unwraps*,
returning a `Task` that completes when the inner work does. One method name apart,
opposite meaning:

```csharp
Task warmup = Task.Run(async () => await WarmCacheAsync()); // unwraps: awaits the real work
await warmup;
```

Full version in [Good.cs](Good.cs). Choosing the launcher:

| Approach | When it's the right call |
|---|---|
| `Task.Run(asyncLambda)` | The default for async work - it unwraps the `Task<Task>` for you and awaits the real thing |
| `Task.Factory.StartNew(asyncLambda).Unwrap()` | Only when you genuinely need a `StartNew` option (a custom `TaskScheduler`, `TaskCreationOptions.LongRunning`); `.Unwrap()` flattens the `Task<Task>` back to the work |
| `Task.Factory.StartNew(syncDelegate)` | Fine for genuinely **synchronous**, CPU-bound work with no `await` - there is no inner task to lose |

## 😈 The Even Worse Sibling

Here the warmup merely fails to finish. Now let `WarmCacheAsync` *throw* - a bad
feed response, a parse error. In a normal `await`, that faults the task and you
see it. But the exception lives in the *inner* task, and nobody is holding the
inner task - you awaited the outer shell, which completed successfully the moment
the lambda started. The fault has no observer, so it becomes a
[0019-forgotten-task](../../async/0019-forgotten-task/): swallowed, or surfacing
minutes later as `TaskScheduler.UnobservedTaskException` with a stack pointing at
warmup code that "succeeded" at boot. The empty cache is the loud, lucky outcome;
the swallowed exception is the one that costs you an afternoon.

## 🎓 Advanced Nuance

`StartNew` is not deprecated and not always wrong - it is the only way to pass a
custom scheduler or `LongRunning`. The rule is narrower: it is the wrong *default*
for async delegates, because it cannot await them. Stephen Toub's "StartNew is
Dangerous" is the canonical write-up; the one-line summary is "reach for
`Task.Run` unless you have a specific reason not to, and if you do, add
`.Unwrap()`."

Two more faces of the same seam. `Task.Run(WarmCacheAsync)` - a method group, no
lambda - is cleaner still and unwraps identically. And the mirror trap is awaiting
a *collection* of these: `tasks.Select(t => Task.Factory.StartNew(t))` gives you
`Task<Task>[]`, and `await Task.WhenAll(...)` over it waits for all of them to
*start*, a parallel [0018-tasks-are-not-results](../../async/0018-tasks-are-not-results/).

## 🔎 How to Find It in Your Codebase

- Grep for `Task.Factory.StartNew(` with an `async` on the same line:
  `Task\.Factory\.StartNew\([^)]*async`. Every hit is either this bug or one
  `.Unwrap()` away from being correct.
- Hover the result, or write `var`: if the inferred type is `Task<Task>` (or
  `Task<Task<T>>`), you are awaiting the outer shell. Awaiting anything typed
  `Task<Task>` without `.Unwrap()` is the smell.
- Roslyn has no built-in rule for the double-wrap itself, so treat it as a review
  check. The **Microsoft.VisualStudio.Threading** analyzers do flag `StartNew` for
  a related reason - **VSTHRD105**, the `TaskScheduler.Current` ambiguity - which
  is a second, independent reason to prefer `Task.Run`.
- In review, watch for `StartNew` chosen "for the options" when no option is
  actually passed - the common cargo-cult that drags an async lambda into the
  trap.
