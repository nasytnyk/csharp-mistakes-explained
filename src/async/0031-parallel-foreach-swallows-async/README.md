---
id: "0031"
title: Handing an async lambda to Parallel.ForEach
category: async
tags: [async, Parallel.ForEach, async-void]
author: palkotnyk
rule: "never hand an **async** lambda to `Parallel.ForEach`"
---

# #0031 - Handing an Async Lambda to Parallel.ForEach

## 💥 Symptom

The nightly receipt job is suspiciously fast. The log says
`8 of 8 receipts sent`, the run finishes in milliseconds, the dashboard is
green - and the support queue fills with customers who never got a receipt. No
exception, no failed task, no retry: the batch reported total success while the
mailbox stayed empty. When you add up what actually reached the gateway, it is
zero, and the code that "sent" it has already moved on.

## 🔍 The Offending Code

```csharp
Parallel.ForEach(orders, async order =>
{
    await SendReceiptAsync(order);   // 💥 this lambda is async void
    sent.Enqueue(order);
});

// We are here now. Nothing has been sent yet.
Console.WriteLine($"{sent.Count} of {orders.Length} receipts sent.");
```

## 🧠 What's Actually Going On

`Parallel.ForEach` has no overload that takes `Func<T, Task>`. Its body
parameter is `Action<T>` - a **void-returning** delegate. When you pass an
`async` lambda to it, the lambda is compiled to satisfy that delegate, so it
becomes `async void`: the fire-and-forget shape that exists only for event
handlers.

An `async void` method returns to its caller at the first `await` that actually
suspends. So each body runs synchronously up to `await SendReceiptAsync(order)`,
hands back a task nobody holds, and returns. `Parallel.ForEach` only waits for
the body *invocations* to return - which they do, immediately, at that first
`await`. It has no task to observe and no way to know the real work is still in
flight, so it reports the loop complete while all eight sends are still parked
mid-flight.

The mental model that breaks is "`Parallel.ForEach` runs my body to completion".
It runs your body to its *first await* and calls that completion. The `await`
keyword makes the lambda look like it is being waited on; the delegate type
quietly decided otherwise, and nothing in the call site shows the seam. This is
the same `async void` hazard as
[0007-async-void](../../async/0007-async-void/), reached not by writing
`async void` yourself but by letting a `void` delegate infer it for you.

## ✅ The Fix

Use `Parallel.ForEachAsync` (.NET 6+). Its body is
`Func<T, CancellationToken, ValueTask>`, so it returns a `Task` you `await`, and
it does not complete until every body has:

```csharp
await Parallel.ForEachAsync(orders, async (order, ct) =>
{
    await SendReceiptAsync(order);
    sent.Enqueue(order);
});
```

Full version in [Good.cs](Good.cs). Picking the right parallel tool:

| Tool | When it's the right call |
|---|---|
| `await Parallel.ForEachAsync` | Async work per item (I/O, HTTP, DB) - it awaits each body and surfaces exceptions. The default for async batches |
| `Parallel.ForEach` | Genuinely **synchronous**, CPU-bound bodies with no `await` anywhere. If the body cannot be `async`, the trap cannot bite |
| `await Task.WhenAll(items.Select(SendAsync))` | Async work with no need to cap concurrency - fires them all at once and awaits the set |
| A plain `foreach` with `await` | Order matters or the work must be sequential - correctness over speed |

## 😈 The Even Worse Sibling

Here nothing throws, so the damage is "only" silent. Now let `SendReceiptAsync`
fail - a bad address, a 500 from the gateway. In a normal `await`, that
exception faults the task and `Parallel.ForEachAsync` reports it. Under
`async void` there is no task to fault: the exception is raised on a thread-pool
thread with no one waiting, becomes an unhandled exception, and by default
**tears down the whole process** - often minutes later, from a job that already
logged success. So the failure modes are a lost result that crashes nothing, or
a caught-nowhere exception that crashes everything, and which one you get depends
on nothing you can see at the call site. The silent "0 sent" in this exhibit is
the *lucky* outcome.

## 🎓 Advanced Nuance

`Parallel.ForEachAsync` arrived only in .NET 6. Before it, the honest options
were `Task.WhenAll` over a projection (all at once) or a `SemaphoreSlim` to
throttle - and a lot of code reached for `Parallel.ForEach` instead precisely
because it *looked* async-aware. It is not, and neither is `Parallel.For` or
`Parallel.Invoke`: all of them take `void`-returning delegates and will infer
`async void` from an `async` lambda without a peep from the compiler.

PLINQ hides the same edge: `orders.AsParallel().Select(async o => await
SendAsync(o))` produces an `IEnumerable<Task>`, and if you enumerate it for its
side effects you have started work you never awaited - a
[0018-tasks-are-not-results](../../async/0018-tasks-are-not-results/) in parallel
clothing.

One reason this survives code review: `Parallel.ForEach` returns
`ParallelLoopResult`, which people discard, so there is not even an unawaited
`Task` for an analyzer to flag on the result. The `async void` lives inside the
lambda, where it is easy to read right past.

## 🔎 How to Find It in Your Codebase

- Grep for `Parallel.ForEach(` and `Parallel.For(` with an `async` on the same
  or next line: `Parallel\.ForEach\([^)]*async`. Every hit is either the bug or
  one edit away from it.
- Analyzer **CA2021** flags `async` delegates passed to `Parallel.ForEach`; the
  Meziantou and Microsoft.VisualStudio.Threading analyzers (**VSTHRD101**,
  VSTHRD107) flag `async void` lambdas more broadly. Turn them on and treat them
  as errors.
- In review, watch for `async` lambdas handed to any API whose parameter is an
  `Action`/`Action<T>` rather than a `Func<..., Task>` - `Parallel.ForEach`,
  `List.ForEach`, `Timer` callbacks, `ThreadPool.QueueUserWorkItem`. The tell is
  an `await` inside a body that the surrounding call never `await`s.
