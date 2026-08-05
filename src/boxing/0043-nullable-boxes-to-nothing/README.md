---
id: "0043"
title: Boxing an empty Nullable
category: boxing
tags: [boxing, Nullable, NullReferenceException]
rule: "never expect a boxed **nullable** to still be nullable"
---

# #0043 - Boxing an Empty Nullable

## 💥 Symptom

A `NullReferenceException` from a line that dereferences nothing. The stack points
at `int retries = (int)settings["retries"];` - a cast, no `.` in sight - and the
value it is unboxing was put there by a *different* component, as an `int?`. A
value type. The one thing everyone knows about value types is that they cannot be
null, so this crash reads as impossible. It passes every test, because test setups
always configure the value; it dies in production the first time the setting is
genuinely absent, hours after deploy, in a file that never touched the setting.

## 🔍 The Offending Code

```csharp
int? configured = LoadSetting("retries");   // absent -> null
settings["retries"] = configured;           // boxing an EMPTY int? stores a plain null
// ...elsewhere...
int retries = (int)settings["retries"];     // 💥 NullReferenceException: the box is null, not an int
```

## 🧠 What's Actually Going On

`Nullable<T>` does not exist inside a box. Boxing a nullable is special-cased by the
runtime: if it `HasValue`, it boxes the underlying `T` - a boxed `int?` of 7 is
indistinguishable from a boxed `int`, and `GetType()` reports `Int32`. If it is
empty, it boxes to a **null reference** - not a box of "empty", an actual `null`.

So `(object)(int?)null` *is* `null`, plainly `== null`. Drop an unset `int?` into
an `object`-typed store and you have stored `null`. The wrapper that carried the
"no value" information evaporated at the boundary; the value type that "can't be
null" crossed into `object` as a bare null reference. Reading it back with `(int)`
unboxes `null`, and unboxing `null` throws `NullReferenceException`.

The tell that this is boxing and not some ordinary null is next door:
`empty.HasValue` returns `false` without complaint, but `empty.GetType()` throws
`NullReferenceException`. `HasValue` is a `Nullable<T>` member and needs no box;
`GetType` is non-virtual on `object`, so calling it boxes the `int?` first,
produces `null`, and then dereferences it. The same variable answers one question
calmly and the next with an NRE.

## ✅ The Fix

Unbox to the *nullable* type. `(int?)` accepts the `null` a missing value became
(as an empty `int?`) and a real boxed `int` alike, so you can default it instead of
crashing:

```csharp
int retries = (int?)settings["retries"] ?? 3; // null -> empty -> default; a real int comes through
```

Full version in [Good.cs](Good.cs). Choosing the approach:

| Approach | When it's the right call |
|---|---|
| `(int?)bag[key] ?? fallback` | The default at an `object` border where a nullable may have been stored - `null` becomes empty and defaults, a real value passes through |
| `bag[key] is int n ? n : fallback` | Pattern match - handle "present" and "absent" as explicit branches without `??` |
| Keep the type as `int?` end to end | You can avoid the `object` box entirely - then nothing evaporates |
| Do not store empties (skip the key, or store a real default) | You own the write side - a missing key is clearer than a stored `null` |

Unboxing to `int?` is safe in both directions: `(int?)(object)7` is `7` and
`(int?)(object)null` is empty. It is the nullable-tolerant door out of the
exact-type unboxing rule in
[0041-unbox-must-match-exact-type](../../boxing/0041-unbox-must-match-exact-type/).

## 😈 The Even Worse Sibling

The roundtrip is asymmetric, and the asymmetry is aimed straight at your test
suite. Store a *filled* `int?` and read it back and everything works - even the
buggy `(int)` cast succeeds, because a filled nullable boxes to a plain `int`. Only
the *empty* case boxes to `null` and detonates. And empty is precisely the case
test data never contains: tests set the retry count, the timeout, the discount;
nobody writes the fixture where the optional value is absent. So the bug sails
through CI green, ships, and waits for the first real request that leaves the
setting unset - store site and crash site in different components, a null nobody
believes could exist. The crash is bad; the crash arriving only in production,
never in a test, is the trap.

## 🎓 Advanced Nuance

The reverse conversions are all lenient, which is what makes the forward crash so
surprising: `(int?)(object)7` works, `(int?)(object)null` is empty, and a plain
`int` box unboxes into `int?` without complaint. The runtime bridges `T` box and
`T?` freely - it is *only* the `(int)` unbox of a stored-empty (i.e. `null`) that
throws, and it throws as an NRE rather than an `InvalidCastException`, because there
is no wrong-typed box to reject, just a null to dereference.

And `(object)(int?)null == null` being `true` is a real emptiness check, but a
misleading one: it is true because the box is a genuine null reference, not because
any `Nullable` comparison ran. Two different `int?` empties are both just `null`, so
they are even reference-equal - the one case in boxing where two "values" share an
identity, precisely because neither is a box at all.

## 🔎 How to Find It in Your Codebase

- The compiler already warns: unboxing an `object?` to a non-nullable value type
  raises **CS8605 "Unboxing a possibly null value."** If you have silenced it with
  `!` - `(int)bag[key]!` - you have silenced the exact warning for this bug. Do not
  suppress it; unbox to `int?` instead.
- Grep for `(int)`, `(T)` casts of values pulled from `object`-typed stores -
  `Dictionary<_, object>`, `object[]`, `DbParameter.Value`, logging scopes, view
  state - where a nullable could have been written on the other side.
- The habitat is a store site and a read site in different components: one puts an
  `int?`/`decimal?`/`DateTime?` into an object bag, the other casts it back to the
  non-nullable type. Audit both ends together.
- In tests, add the *absent* case explicitly. This bug is invisible to any fixture
  that always supplies the optional value, which is most of them.
