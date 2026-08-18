---
id: "0084"
title: the transient that piled up
category: di-lifetimes
tags: [di-lifetimes, IDisposable, transient]
rule: "never resolve a transient `IDisposable` from the root provider - it lives until shutdown; use a **scope**"
---

# #0084 - The Transient That Piled Up

## 💥 Symptom

A long-running service - a worker, a hosted background job, a message consumer -
climbs in memory and open handles the longer it runs, and a restart "fixes" it.
The dependency is registered `Transient`, which everyone reads as "made fresh,
used, thrown away," so it is the last thing anyone suspects. Yet every job opens
one more `DbSession` and none of them ever close, until the process exits.

## 🔍 The Offending Code

```csharp
var provider = new ServiceCollection()
    .AddTransient<DbSession>()
    .BuildServiceProvider();

for (int job = 0; job < jobs; job++)
{
    var session = provider.GetRequiredService<DbSession>(); // 💥 resolved from the root
    session.Run(job);
    // session goes out of scope here - but the container still holds it
}
```

## 🧠 What's Actually Going On

The DI container owns the lifetime of every `IDisposable` it creates, and it
disposes them **when the owning provider is disposed** - not when your local
variable goes out of scope, not at the next GC. To do that, it keeps a reference
to each disposable instance it hands out. `Transient` controls how *often* a new
instance is created (every resolve), but not *when* it is released: that is tied
to the provider the resolve went through. Resolve from the root provider and the
owner is the root - whose lifetime is the whole application - so the container
holds every session until shutdown, and a per-job resolve becomes a per-job
leak.

The broken belief is "transient means short-lived, so it cleans itself up." A
transient's *creation* is cheap and frequent, but its *disposal* is deferred to
its owning scope, and if that scope is the application root, "deferred" means "at
process exit." The instance is not garbage-collected either, precisely because
the container is still holding it to dispose it - so a memory profiler shows the
objects alive and rooted, which looks like the container leaking rather than the
resolve site choosing the wrong owner.

## ✅ The Fix

Create a scope for each unit of work and resolve from it. Disposing the scope
disposes everything created within it.

```csharp
for (int job = 0; job < jobs; job++)
{
    using var scope = provider.CreateScope();
    var session = scope.ServiceProvider.GetRequiredService<DbSession>();
    session.Run(job);
} // scope disposed -> the session is disposed here
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| A scope per unit of work | The general fix - wrap each job/request/message in `using var scope = provider.CreateScope()` and resolve from the scope, so its disposables die with it. |
| Let the framework own the scope | ASP.NET Core / Worker templates already open a scope per request or per `ExecuteAsync` iteration - inject into the *scoped* component and never resolve from the root yourself. |
| A factory for the disposable | The dependency is created on demand mid-method - inject `Func<DbSession>` or an `IDbSessionFactory` and wrap each `using var session = factory()` yourself, so you own disposal explicitly. |
| Don't make it transient IDisposable at all | A pooled or shared resource - register it so the container creates one (singleton/pooled) instead of a new disposable per resolve that must be tracked and released. |

## 😈 The Even Worse Sibling

Piling up transients until shutdown is the *slow* leak - it needs a long-running
process to hurt. The sharper failure is injecting that same transient
`IDisposable` into a **singleton**: the singleton is built once and holds the one
instance for the entire application, so a "transient" dependency is silently
promoted to a lifetime it was never designed for - a per-request connection now
shared across every thread, or a stateful helper that was supposed to be
throwaway now living forever (the singleton-captures-a-shorter-lifetime trap, the
mirror of [0022-the-captive-scoped](../0022-the-captive-scoped/)). And the inverse
bites in ASP.NET Core: resolve a *scoped* service straight from the root provider
and you get `InvalidOperationException: Cannot resolve scoped service ... from
root provider` - the framework refuses it, while the transient-from-root leak it
allows silently.

## 🎓 Advanced Nuance

- **Only `IDisposable` transients are tracked.** A transient with no `IDisposable`
  (or `IAsyncDisposable`) is created and genuinely forgotten - the GC reclaims it
  normally. The tracking, and therefore the leak, exists precisely because the
  container promised to call `Dispose`, and the only way to keep that promise is
  to hold a reference until the owning scope ends.
- **`IAsyncDisposable` needs an async disposal path.** If the transient implements
  `IAsyncDisposable`, dispose the scope with `await scope.DisposeAsync()` (or use
  `AsyncServiceScope`); disposing it synchronously can throw
  `InvalidOperationException` for async-only disposables.
- **A `using`/factory you own sidesteps container tracking entirely.** When you
  write `using var session = factory()`, *you* dispose it at the closing brace and
  the container never holds it - which is why an explicit factory is the right
  tool for a disposable created deep inside a method rather than at composition
  time.

## 🔎 How to Find It in Your Codebase

- Grep for `GetService`/`GetRequiredService` called on the root
  `IServiceProvider` (or on `IServiceProvider` injected into a singleton) for a
  type that implements `IDisposable` - each call adds an instance the root will
  hold until shutdown.
- Look at long-running loops - background workers, consumers, timers - that
  resolve a disposable per iteration without opening a scope.
- Symptom-side: memory and handle counts that climb with uptime and reset on
  restart, disposables whose `Dispose` runs only at shutdown (or never, if the
  provider is never disposed), profiles showing many live instances rooted by the
  service provider.
- Open a scope per unit of work (or inject a factory) so every transient
  disposable has a short-lived owner, and reserve root-provider resolves for
  singletons.
