---
id: "0035"
title: Blocking on async code under a busy thread pool
category: async
tags: [async, ThreadPool, sync-over-async]
rule: "never block on **async** code with `.Result` or `.Wait()`"
---

# #0035 - Blocking on Async Code Under a Busy Thread Pool

## 💥 Symptom

The service is fine in dev, fine in staging, fine at 3 PM. Then the lunchtime
traffic peak arrives and it just... stops. Requests pile up, latency climbs to
the timeout, health checks go red - and the CPU graph is flat at zero. No
exception in the logs, no failed dependency, nothing to point at. You restart
the process and it recovers instantly, right up until the next peak. The one
line everyone walked past in review was a synchronous method that "just needed a
value" from an async call.

## 🔍 The Offending Code

```csharp
// The reporting layer is synchronous, so it blocks on the async FX call.
public decimal GetUsdRate() => GetUsdRateAsync().GetAwaiter().GetResult();

// A burst of requests, each grabbing the rate through that facade.
var reports = ids.Select(id => Task.Run(() =>
{
    decimal rate = rates.GetUsdRate(); // 💥 parks a pool thread on async work
    done.Enqueue(id);
})).ToArray();

Task.WaitAll(reports); // never returns under load
```

## 🧠 What's Actually Going On

`GetUsdRateAsync` awaits a network call. When it hits the `await`, the method
returns a `Task` and the *rest of it* - the `return 41.75m` - becomes a
continuation that has to be scheduled somewhere. In a console, worker, or
ASP.NET Core app, "somewhere" is the thread pool.

`GetAwaiter().GetResult()` blocks the current thread until that `Task`
completes. So a pool thread is now sitting still, holding its slot, waiting for a
continuation that itself needs a free pool thread to run. One such call is
harmless - there are plenty of threads. But under load, when a burst of requests
each does the same thing, every worker becomes a blocker: all of them frozen
inside `GetResult`, and all of their continuations queued behind them, waiting
for a thread that will never come free. It is a circular wait through the
scheduler, a deadlock with no `lock` anywhere in your code.

The reproduction pins the pool small
(`SetMaxThreads(4, 4)`) because that is what a production pool *looks like* at
its ceiling under a burst: every worker busy. On a real box the pool is huge,
but the shape is identical - saturate it and the blockers eat it alive. That is
why this is the museum's rare "the code fixes the environment" exhibit: pinning
the pool is not cheating, it is compressing the at-scale condition onto one
laptop so the hang is deterministic instead of "sometimes, at peak".

## ✅ The Fix

Await it. An `await` does not hold the thread while the network call is in
flight - it hands the worker back to the pool and only queues a continuation
when the result is ready. The eight reports then overlap on four threads and
finish in about 100 ms, the same pool that deadlocked before:

```csharp
var reports = ids.Select(id => Task.Run(async () =>
{
    decimal rate = await rates.GetUsdRateAsync(); // frees the pool thread
    done.Enqueue(id);
})).ToArray();

await Task.WhenAll(reports);
```

Full version in [Good.cs](Good.cs). "Async all the way" is the rule; the blocking
calls are what break the chain, the same way a `void`-returning delegate breaks
it in
[0031-parallel-foreach-swallows-async](../../async/0031-parallel-foreach-swallows-async/).
When you think you have to bridge sync and async, here is the honest menu:

| Approach | When it's the right call |
|---|---|
| `await` the call, async all the way up | The default. Anywhere the caller can be `async` - which, transitively, is almost everywhere |
| Call a real synchronous API | The library ships both paths (e.g. `stream.Read` next to `stream.ReadAsync`). Use the sync one instead of blocking the async one |
| `Task.Run(() => AsyncMethod()).GetAwaiter().GetResult()` | **Never** - offloading to the pool still blocks a pool thread; it moves the starvation, it does not remove it |
| Block on a dedicated non-pool thread | Last resort at a hard sync boundary you cannot make async (a legacy interface, `Main` on an old framework). It survives starvation because the blocked thread is not one the continuation needs |

## 😈 The Even Worse Sibling

This exhibit hangs cleanly in three seconds because the pool is pinned. A real
pool does something crueller: when it sees work queued and no free thread, the
hill-climbing heuristic injects roughly one new thread per second. So production
does not deadlock - it *limps*. Latency climbs as requests wait seconds for an
injected thread, a few time out, the pool slowly claws width back, traffic dips,
and it recovers - leaving a graph that looks like "the network was slow" and a
stack trace that never comes. It reproduces only under real concurrency, so
staging with two test users never sees it. The clean, diagnosable hang in this
exhibit is the *lucky* outcome; the production version is a slow-motion collapse
that blames everything except the `.Result` that caused it.

## 🎓 Advanced Nuance

There are two different "`.Result` deadlocks", and conflating them is why the fix
folklore misfires:

- **The SynchronizationContext deadlock** (classic WinForms/WPF/legacy ASP.NET):
  the captured context runs continuations on *one specific* thread; `.Result`
  blocks that thread while the continuation waits to run on it. `ConfigureAwait(false)`
  cures this one by telling the continuation it does not need the captured
  context. It cannot happen in a console or ASP.NET Core app, because there is no
  SynchronizationContext to capture.

- **The thread-pool starvation deadlock** (this exhibit): no context involved,
  just every worker blocked waiting for a continuation that needs a worker.
  `ConfigureAwait(false)` does **not** save you here - the continuation still
  needs a pool thread, and there are none. This is the form that bites modern
  ASP.NET Core, worker services, and background jobs, precisely the places people
  assume they are safe because "there's no SynchronizationContext anymore".

So the advice "just add `ConfigureAwait(false)`" fixes the deadlock people stopped
having and does nothing for the one they now have. The only real cure is to stop
blocking.

## 🔎 How to Find It in Your Codebase

- Grep for the three faces of the same mistake: `.Result`, `.Wait(`, and
  `.GetAwaiter().GetResult()`. Regex `\.(Result|Wait\(|GetAwaiter\(\)\.GetResult)`
  catches all three. Each hit over a `Task` is a suspect.
- The **Microsoft.VisualStudio.Threading** analyzers flag it directly:
  **VSTHRD002** (avoid problematic synchronous waits) and **VSTHRD103** (call the
  async version when you are already in an async method). **AsyncFixer01** does
  the same. Turn them on and treat them as errors.
- Watch the places that *cannot* be `async` and so tempt a blocking bridge:
  property getters, constructors, `IDisposable.Dispose`, `ToString`, and
  synchronous interface implementations wrapping async work. A sync facade over
  an async method - the `GetUsdRate` in this exhibit - is the classic tell.
- In review, red-flag any `ConfigureAwait(false)` offered as the *reason* a
  blocking call is safe. On the thread pool it changes nothing; its presence next
  to a `.Result` is a sign someone diagnosed the wrong deadlock.
