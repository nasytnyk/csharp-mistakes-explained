---
id: "0054"
title: The twin singletons
category: di-lifetimes
tags: [di-lifetimes, singleton, dependency-injection]
rule: "never register one class under two interfaces as separate **singletons** - that is two instances"
---

# #0054 - The Twin Singletons

## 💥 Symptom

A cache that never invalidates. A value written through the writer comes back stale from
the reader, forever - and both objects are, by every definition you can find, singletons.
You registered one class with `AddSingleton`, once per interface, and the container swears
each is a singleton. It is: one instance *per interface*. Your "shared" state is split
cleanly in two, each half perfectly consistent with itself, jointly wrong.

## 🔍 The Offending Code

```csharp
services.AddSingleton<IReader, SettingsStore>();  // instance #1
services.AddSingleton<IWriter, SettingsStore>();  // instance #2 - same class, different singleton

var writer = provider.GetRequiredService<IWriter>();
var reader = provider.GetRequiredService<IReader>();
writer.Set("theme", "dark");
reader.Get("theme"); // <missing> - the write landed on the other instance
```

## 🧠 What's Actually Going On

`AddSingleton<TService, TImplementation>` caches **one instance per registration**, keyed
by the *service* type - not by the implementation class. `AddSingleton<IReader, SettingsStore>()`
and `AddSingleton<IWriter, SettingsStore>()` are two independent registrations, so the
container constructs `SettingsStore` twice and hands the `IReader` resolution one copy and
the `IWriter` resolution the other. "Singleton" means "one instance for this service type,"
which reads to a human as "one, period." The class really is instantiated once - *per
interface it is registered under*.

Nothing warns you. Both registrations are valid, both resolve, both return a
`SettingsStore` - they are simply not the *same* `SettingsStore`. The broken belief is
"singleton = one instance of this class." Singleton is a property of the *registration*,
and you made two of them.

## ✅ The Fix

Register the concrete type once, then forward each interface to that single registration:

```csharp
services.AddSingleton<SettingsStore>();
services.AddSingleton<IReader>(sp => sp.GetRequiredService<SettingsStore>());
services.AddSingleton<IWriter>(sp => sp.GetRequiredService<SettingsStore>());
```

Now all three resolutions return the one `SettingsStore`. Full version in [Good.cs](Good.cs);
the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| Register concrete once, forward interfaces with factory delegates | The default whenever one class must be shared behind several interfaces (reader/writer, cache/invalidator). |
| Depend on the concrete type where a consumer needs both roles | If one consumer legitimately needs `IReader` *and* `IWriter`, inject `SettingsStore` directly - one dependency, no forwarding. |
| Split the class - it really was two objects | If the "shared" state was an illusion and the roles never overlap, two instances is correct; make it explicit instead of accidental. |
| A single combined interface, or `AddKeyedSingleton` | When you want one instance behind distinct entry points, a combined facade or keyed registration states the intent outright. |

## 😈 The Even Worse Sibling

The two instances do not just start separate - they *diverge* with uptime, so the damage
grows the longer the process runs. The `IWriter` copy accumulates every write; the
`IReader` copy stays frozen at construction. A cache invalidator wired this way "runs" on
every change - you can watch it in the logs - while the read path serves stale data
indefinitely, so the fix everyone reaches for ("invalidate again, harder") keeps hitting
the object that was never the one being read. And it scales with your interface hygiene:
the cleaner your reader/writer segregation, the more interfaces one class implements, the
more copies the container quietly mints. Good design amplifies the bug.

## 🎓 Advanced Nuance

- **`Scoped` and `Transient` split the same way - singleton just makes it permanent.** Two
  `AddScoped` registrations of one class yield two instances *per scope*; two `AddTransient`
  yield two per resolution (which is "expected" for transient, so nobody notices the
  pattern until it becomes a singleton). The twin-instance rule is identical across
  lifetimes; only singleton turns the split into "shared state that isn't."
- **Same lifetime-model mismatch as [0022-the-captive-scoped](../../di-lifetimes/0022-the-captive-scoped/).**
  Both are the container's lifetime rules refusing to match the mental model - there, a
  scoped service captured by a singleton *outlives* its scope; here, one class behind two
  singleton registrations *under-shares* against your "one instance" assumption.
- **The forwarding factory must resolve, not re-register or hand in an instance.**
  `sp => sp.GetRequiredService<SettingsStore>()` shares the one container-built instance.
  A second `AddSingleton<IReader, SettingsStore>()` does not (that is the bug). And
  `AddSingleton<IReader>(new SettingsStore())` hands in an object the container did not
  build - losing any constructor dependencies `SettingsStore` would otherwise get injected.

## 🔎 How to Find It in Your Codebase

- Grep for one implementation class appearing in more than one `AddSingleton<IFoo, TheClass>()`
  registration (and the `AddScoped` / `AddTransient` equivalents). Each extra service type
  is another instance.
- Look for reader/writer or command/query interface pairs - `ICache`/`ICacheInvalidator`,
  `IReadStore`/`IWriteStore` - implemented by one class and registered separately. That is
  the classic shape.
- Symptom-side: "the cache never invalidates," "metrics are half-counted," "the flag I set
  isn't read" - anywhere in-memory state is written through one interface and read through
  another.
- Assert it in a test: resolve both interfaces from the built provider and
  `Assert.Same(reader, writer)`. If they are meant to be one object, that single line
  catches the split at build time.
