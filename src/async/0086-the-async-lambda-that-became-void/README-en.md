---
id: "0086"
title: the async lambda that became async void
category: async
tags: [async, async-void, lambdas]
rule: "never pass an `async` lambda where an `Action` is expected - it becomes **`async void`**, fire-and-forget"
---

# #0086 - The async Lambda That Became async void

## 💥 Symptom

The batch "saved every order and reported success" - and the orders are not in
the database. No exception surfaced, the log line printed the happy number, the
method returned. The `await` is right there inside the lambda, so it looks
awaited. It is not: the loop that ran the saves handed each one off and walked
away, and the program finished while the work was still pending.

## 🔍 The Offending Code

```csharp
orders.ForEach(async id => await SaveAsync(id)); // 💥 ForEach wants an Action -> async void

Console.WriteLine($"Saved {saved.Count} of {orders.Count} orders."); // "Saved 0 of 3"
```

## 🧠 What's Actually Going On

An `async` lambda is converted to whichever delegate type the context expects.
Where a `Func<Task>` is wanted, it returns the `Task` and the caller can await it;
but where an `Action` (or `Action<T>`) is wanted, there is nowhere to return a
`Task`, so the lambda compiles as **`async void`**. `List.ForEach` takes
`Action<T>`, so each `async id => ...` becomes an `async void` call: `ForEach`
invokes it, the method runs synchronously up to the first `await`, then returns a
*void* the moment it hits `await SaveAsync(id)` - handing control straight back to
`ForEach`, which starts the next one and, a moment later, returns. Nothing awaited
anything; the saves are in flight with no handle to wait on.

The broken belief is "there's an `await` in the lambda, so the loop waits for it."
The `await` suspends *that lambda*, not the loop that called it - and because the
delegate is `async void`, the loop never received a `Task` to await in the first
place. So control races past the `saved.Count` check while zero saves have
completed, and then top-level `Main` returns and the process exits with the
fire-and-forget work abandoned mid-flight. Worse than the wrong count: an
exception thrown inside an `async void` method has no `Task` to land on, so it is
raised on the synchronization context (or crashes the process) rather than being
caught by any `try` around the loop.

## ✅ The Fix

Await the `Task`-returning method in a real loop, so each call completes before
the next and failures propagate.

```csharp
foreach (var id in orders)
    await SaveAsync(id); // awaited - completes before we report
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| `foreach` + `await` | Sequential work - the default; each iteration finishes (and can throw) before the next begins, and a single shared list stays safe because nothing runs concurrently. |
| `await Task.WhenAll(items.Select(Async))` | The calls are independent and you want them concurrent - collect the `Task`s and await them together; guard shared state, since handlers now run in parallel. |
| A method that takes `Func<T, Task>` | You want a ForEach-shaped helper - `foreach` inside an `async` method, or an `await`-aware extension, so the delegate is `Func<T,Task>` and actually gets awaited. |
| `async void` only for event handlers | The genuine exception - a UI/event handler must match the `void`-returning delegate; everywhere else, return `Task` so the caller can await and observe failures. |

## 😈 The Even Worse Sibling

`List.ForEach` is the obvious trap; the same conversion hides in places that look
nothing like it. `Timer`, `Parallel.ForEach` (the non-async overload),
`Enumerable`-style callbacks, and any API taking `Action`/`EventHandler` will all
silently swallow an `async` lambda into `async void` - including
`Parallel.ForEach(items, async x => ...)`, which starts every iteration
unawaited and reports completion while the work runs on (its sibling
[0031-parallel-foreach-swallows-async](../0031-parallel-foreach-swallows-async/)).
And the failure is worse when there *is* an exception: an `async void` that throws
does not fault a `Task` anyone holds - it re-raises on the captured context, so in
ASP.NET or a desktop app it can tear down the process, and in a console app it may
vanish entirely because the app exits before the continuation runs.

## 🎓 Advanced Nuance

- **The delegate's return type decides everything.** The *same* `async` lambda is
  a safe `Func<Task>` in one call and a fire-and-forget `async void` in another -
  overload resolution picks by the target type, so the bug depends on which
  overload you hit, not on how the lambda is written. When both `Action` and
  `Func<Task>` overloads exist, the compiler prefers `Func<Task>` - it is the
  *absence* of a `Func<Task>` overload (like `List.ForEach`) that forces
  `async void`.
- **No `CS4014` warning fires here.** The "this call is not awaited" warning is
  for a `Task` you discard; an `async void` lambda returns no `Task`, so there is
  nothing for that warning to catch - the compiler is silent about the exact case
  that hurts most.
- **`async void` breaks `try`/`catch` around the call site.** Because the lambda
  returns before its body completes and never yields a `Task`, an exception it
  throws after the first `await` cannot be caught by a `try` around `ForEach`; the
  only place to catch it is *inside* the lambda.

## 🔎 How to Find It in Your Codebase

- Grep for `async` lambdas passed to `.ForEach(`, `Parallel.ForEach(`, timer
  callbacks, and any method whose parameter is `Action`/`Action<T>`/`EventHandler`
  - each is an `async void` in disguise.
- Look for "did the work but nothing happened" symptoms: counts that read zero (or
  partial) right after a loop, saves/sends that complete "later" or not at all,
  exceptions that crash the process instead of being caught by a surrounding
  `try`.
- Turn on the analyzers: `async void` and un-awaited async are flagged by
  `VSTHRD100`/`VSTHRD101` (the Threading analyzers) and by AsyncFixer - they catch
  the lambda conversion the base compiler stays quiet about.
- Await `Task`-returning work in a `foreach`, or gather it with `Task.WhenAll`, and
  reserve `async void` exclusively for event handlers.
