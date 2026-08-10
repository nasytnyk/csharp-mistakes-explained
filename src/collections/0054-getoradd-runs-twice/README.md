---
id: "0054"
title: GetOrAdd runs your factory more than once
category: collections
tags: [collections, concurrency, ConcurrentDictionary]
rule: "never put a side-effecting factory in `GetOrAdd` - it can run **more than once** per key"
---

# #0054 - GetOrAdd Runs Your Factory More Than Once

## 💥 Symptom

A per-tenant resource cache - connections, `HttpClient`s, compiled models - meant to build
each tenant's object once on first use and reuse it forever. Under load it builds some of
them two, three, four times. The extra copies are opened and then thrown away: connections
never closed, clients leaking sockets, a collector double-counting. Every lookup still
returns a valid object and the dictionary is a `ConcurrentDictionary`, so the cache is the
last thing anyone suspects - and it only misbehaves under the concurrency it exists to
serve.

## 🔍 The Offending Code

```csharp
var cache = new ConcurrentDictionary<int, Connection>();
...
var conn = cache.GetOrAdd(tenantId, id => OpenConnection(id)); // 💥 factory runs per racer, not per key
```

## 🧠 What's Actually Going On

`ConcurrentDictionary` guarantees the *dictionary* stays consistent - no corruption, no torn
reads, exactly one value stored per key. It does **not** guarantee your factory runs once.
When several threads call `GetOrAdd` for the same missing key at once, they each find the key
absent and each invoke the factory; then they race to insert, one wins, and the losers'
freshly-built values are discarded. The stored value is atomic and correct - but the *side
effects* of building the extras are not undone.

So `GetOrAdd(key, factory)` means "ensure a value is stored," not "run this factory at most
once." For a pure, cheap factory that is harmless. For the usual reason people cache - the
factory is *expensive* or *has side effects* (opens a connection, registers a callback,
allocates a pool) - every redundant run is a leaked resource or a duplicated effect. The
broken belief is "`ConcurrentDictionary` is thread-safe, so `GetOrAdd` is atomic." The
container is thread-safe; the delegate you hand it is your problem.

## ✅ The Fix

Store a `Lazy<T>` instead of the value. `GetOrAdd` may still construct several `Lazy`
wrappers, but a `Lazy` is cheap and side-effect-free to build; only the wrapper that wins the
insert ever has `.Value` read, and `Lazy<T>` (default `ExecutionAndPublication` mode) runs
its inner factory exactly once:

```csharp
var cache = new ConcurrentDictionary<int, Lazy<Connection>>();
...
var conn = cache.GetOrAdd(tenantId, id => new Lazy<Connection>(() => OpenConnection(id))).Value;
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `ConcurrentDictionary<K, Lazy<V>>` + `.Value` | The default fix - keeps GetOrAdd's lock-free reads, moves "run once" into `Lazy`. Best when the factory is expensive or side-effecting. |
| Leave the factory as-is | It is pure and cheap - a redundant `id => id * 2` costs nothing. The double-run only bites side effects. |
| `GetOrAdd(key, value)` - the non-delegate overload | You already hold the value; store it directly, with no factory to double-run. |
| A dedicated cache (`LazyCache`, `HybridCache`) | Async factories, expiry, and stampede protection together - don't hand-roll it with a `Task`-valued GetOrAdd (which caches faulted tasks; see below). |

## 😈 The Even Worse Sibling

Make the value a `Task<T>` - the natural "async cache" - and the double-run becomes a
double-*fetch* plus a poison trap. `GetOrAdd(key, id => FetchAsync(id))` starts two fetches on
a stampede; usually just wasteful, until the winning task **faults**. A
`ConcurrentDictionary<K, Task<V>>` happily caches the *faulted* task, so every future caller
for that key awaits the same stored exception forever - one transient failure at population
time becomes a permanently broken cache entry that only a restart clears. `Lazy<Task<T>>` (or
an async-aware cache) sidesteps both the redundant starts and the cached fault.

## 🎓 Advanced Nuance

- **`AddOrUpdate` shares the caveat.** It also takes delegates, and its update delegate can
  re-run on CAS retries under contention. Neither `GetOrAdd` nor `AddOrUpdate` is a critical
  section; if the delegate must be atomic with respect to the entry, it is the wrong tool.
- **The collection-shaped cousin of [0003-race-on-shared-counter](../../async/0003-race-on-shared-counter/).**
  There a `++` on a shared field looked atomic and was not; here a `GetOrAdd` factory looks
  single-shot and is not. Same root lesson: a thread-safe *type* does not make the *operation
  you compose with it* thread-safe.
- **`Lazy<T>` mode matters.** The default `ExecutionAndPublication` guarantees run-once;
  `LazyThreadSafetyMode.PublicationOnly` lets the factory run on multiple threads and
  publishes the first to finish - which reintroduces exactly this bug. Switch modes for
  latency and you are back to "the factory may run twice."

## 🔎 How to Find It in Your Codebase

- Grep for `GetOrAdd(` and `AddOrUpdate(` with a *delegate* argument, then read what the
  delegate does: any I/O, `new`-ing a disposable, registering a handler, or mutating shared
  state is a double-run hazard.
- Look for `ConcurrentDictionary<_, Task<_>>`, or values like `HttpClient` / `DbConnection` /
  `*Client` - caches of expensive or disposable objects are the classic victims.
- Symptom-side: handle or connection counts that exceed the number of distinct keys, "why do
  we have three clients for one tenant," resources leaking under load but never in tests.
- No analyzer flags a non-idempotent `GetOrAdd` factory. Make "is this factory safe to run
  more than once?" a review question wherever the value is expensive or disposable, and
  default to `Lazy<T>`.
