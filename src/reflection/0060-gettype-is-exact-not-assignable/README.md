---
id: "0060"
title: GetType() is exact, not assignable
category: reflection
tags: [reflection, Type, polymorphism]
rule: "never check for a **derived type** with `GetType()` - use `IsAssignableFrom`"
---

# #0060 - GetType() Is Exact, Not Assignable

## 💥 Symptom

A check that filters or counts objects of a base type silently skips every subclass. A new
subclass compiles, its `is`-based tests pass, and in production the guard that was supposed to
catch it quietly does not: a payment audit undercounts because refunds slip past, a handler
processes the base type but drops derived ones. Nothing throws - the object simply isn't
matched, and the total looks plausible enough that nobody questions it.

## 🔍 The Offending Code

```csharp
foreach (var e in batch)
{
    if (e.GetType() == typeof(PaymentEvent)) // 💥 the EXACT runtime type, not "is a PaymentEvent"
        payments++;
}
// RefundEvent : PaymentEvent -> e.GetType() is RefundEvent -> not equal -> skipped
```

## 🧠 What's Actually Going On

`GetType()` returns the *exact* runtime type of an object - `RefundEvent`, never its base
`PaymentEvent` - and `==` compares that type for *identity*, not assignability. So
`e.GetType() == typeof(PaymentEvent)` is true only for objects whose runtime type is precisely
`PaymentEvent`; every subclass carries a different `Type` and fails the check. Meanwhile
`e is PaymentEvent` asks a *different* question - "is this assignable to PaymentEvent?" - and
answers `true`, which is exactly why tests written with `is` pass while the code written with
`GetType()` quietly fails.

The broken belief is "GetType() gives me the type, so I can compare it." It gives you *one*
type - the leaf - and identity-comparing a leaf type is blind to the whole hierarchy above it.

## ✅ The Fix

Ask whether the base type is assignable *from* the object's runtime type, so any subclass
counts:

```csharp
foreach (var e in batch)
{
    if (typeof(PaymentEvent).IsAssignableFrom(e.GetType())) // base type accepts any subclass
        payments++;
}
```

`IsAssignableFrom` is called on the *base* and takes the *candidate*:
`typeof(PaymentEvent).IsAssignableFrom(typeof(RefundEvent))` is `true`. Full version in
[Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `e is PaymentEvent` / type pattern | The target type is known in code - simplest of all, honors inheritance for free, checked by the compiler. |
| `baseType.IsAssignableFrom(e.GetType())` | The target is a runtime `Type` value (a registry, a config-driven type) - you can't write `is` against a variable. |
| `e.GetType().IsAssignableTo(baseType)` (.NET 5+) | The same test written in reading order - prefer it so you stop guessing the argument order. |
| Exact `GetType() ==` on purpose | You genuinely want *only* that concrete type and must exclude subclasses - rare, so comment it, because every reader will assume it's the bug. |

## 😈 The Even Worse Sibling

The natural fix is a worse bug. `IsAssignableFrom` reads like English exactly backwards, so the
"obvious" call is `e.GetType().IsAssignableFrom(typeof(PaymentEvent))` - which asks "can a
PaymentEvent be stored in a RefundEvent variable?", and that is `false` for RefundEvent and
`false` for essentially everything. The check that "missed subclasses" now misses *all* classes:
a filter counts zero, a plugin scan returns an empty list that reads as "nothing installed."
And the trap is self-confirming - the sanity check a developer reaches for,
`typeof(X).IsAssignableFrom(typeof(X))`, puts the *same type on both sides*, which is reflexively
`true` in either order, so the one test written to prove the call is correct cannot detect that
the arguments are swapped. The crash-free "matches nothing" is quieter, and worse, than the
original "matches the base type only."

## 🎓 Advanced Nuance

- **`IsAssignableTo` exists because of this.** .NET 5 added `Type.IsAssignableTo(Type)` precisely
  so the common check reads in the natural direction (`derived.IsAssignableTo(base)`); on a
  modern target, prefer it and delete the argument-order coin flip.
- **ORM and DI proxies sharpen it.** A lazy-loading ORM proxy or a generated interceptor has a
  runtime type that is a *subclass* of your entity - `GetType()` returns something like
  `PaymentEventProxy_a1b2`, so objects that passed every unit test fail a `GetType() ==` check
  the moment they come from the database.
- **`typeof` is compile-time, `GetType()` is runtime - and a variable's static type is neither.**
  `typeof(T)` in a generic uses the type argument; `GetType()` uses the object. Mixing them
  (`typeof(T) == obj.GetType()`) smuggles the exact-match trap back in whenever `T` is a base
  type.
- Same family as [0058-the-override-that-wasnt](../../inheritance/0058-the-override-that-wasnt/):
  which code runs is decided by a static, exact notion of type rather than the object's real
  place in the hierarchy.

## 🔎 How to Find It in Your Codebase

- Grep for `GetType() ==`, `GetType() !=`, and a `switch` on `GetType()` - any comparison against
  a type that has subclasses is the shape. If you meant "is a", it is a bug.
- Grep for `.IsAssignableFrom(` and check the argument order at every call: the receiver must be
  the *base*/target type, the argument the *candidate*. Reversed calls compile and return `false`
  silently.
- Symptom-side: a filter/guard/validator that works for a base type but "forgets" subclasses;
  behavior that breaks only for entities loaded through an ORM (proxy types) or resolved through a
  DI interceptor.
- Prefer `is` / type patterns when the type is known in code, and `IsAssignableTo` (.NET 5+) when
  it is a `Type` value; reserve exact `GetType() ==` for the rare case you mean to exclude
  subclasses, and comment it so the next reader knows it isn't this bug.
