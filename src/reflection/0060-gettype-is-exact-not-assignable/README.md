---
id: "0060"
title: GetType() is exact, not assignable
category: reflection
tags: [reflection, Type, polymorphism]
rule: "never key a type map by `GetType()` - it's the **exact** runtime type, so subclasses never match"
---

# #0060 - GetType() Is Exact, Not Assignable

## 💥 Symptom

A new event subclass compiles, its `is`-based tests pass, and in production it silently goes
unhandled. A dispatcher keyed by type - a `Dictionary<Type, Handler>`, a `switch` on
`GetType()`, a registry of `typeof(X)` - routes the base type fine but drops every subclass
into the fallback. Nothing throws; the message just isn't processed, and the audit shows it
"arrived" with no handler recorded.

## 🔍 The Offending Code

```csharp
var handlers = new Dictionary<Type, Action<PaymentEvent>> { [typeof(PaymentEvent)] = Handle };
// ...
if (handlers.TryGetValue(evt.GetType(), out var handler)) // 💥 GetType() is the EXACT runtime type
    handler(evt);
// RefundEvent : PaymentEvent -> GetType() is RefundEvent -> not in the map -> dropped
```

## 🧠 What's Actually Going On

`GetType()` returns the *exact* runtime type of an object - `RefundEvent`, never its base
`PaymentEvent` - and a dictionary key (or `==`, or a `switch`) compares that type for
*identity*, not assignability. So a table keyed by `typeof(PaymentEvent)` matches only objects
whose runtime type is precisely `PaymentEvent`; every subclass carries a different `Type` and
misses. Meanwhile `evt is PaymentEvent` asks a *different* question - "is this assignable to
PaymentEvent?" - and answers `true`, which is exactly why the tests written with `is` all pass
while the dispatcher written with `GetType()` quietly fails.

The broken belief is "GetType() gives me the type, so I can match on it." It gives you *one*
type - the leaf - and identity-matching a leaf type is blind to the entire hierarchy above it.

## ✅ The Fix

Match by assignability, not identity - ask whether the registered type can hold the runtime
type:

```csharp
var handler = handlers.FirstOrDefault(reg => reg.Key.IsAssignableFrom(evt.GetType())).Value;
if (handler is not null) handler(evt);
```

`IsAssignableFrom` is called on the *base* and takes the *candidate*:
`typeof(PaymentEvent).IsAssignableFrom(typeof(RefundEvent))` is `true`. Since .NET 5 you can
write it in reading order with `IsAssignableTo`: `evt.GetType().IsAssignableTo(typeof(PaymentEvent))`.
Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `is` / type pattern (`evt switch { PaymentEvent p => ... }`) | A fixed, known set of types in code - the compiler checks it and pattern matching honors inheritance for free. |
| `registeredType.IsAssignableFrom(actual)` | A dynamic registry (`Dictionary<Type, Handler>`) that must respect subclasses - scan for the first assignable key. |
| `actual.IsAssignableTo(registeredType)` (.NET 5+) | The same test written in the order you read it - prefer it so you stop guessing the argument order. |
| Exact `GetType() ==` on purpose | You genuinely want *only* that concrete type and must exclude subclasses - rare, so comment it, because every reader will assume it's the bug. |

## 😈 The Even Worse Sibling

The natural fix is a worse bug. `IsAssignableFrom` reads like English exactly backwards, so the
"obvious" call is `evt.GetType().IsAssignableFrom(typeof(PaymentEvent))` - which asks "can a
PaymentEvent be stored in a RefundEvent variable?", and that is `false` for RefundEvent and
`false` for essentially everything. The dispatcher that "missed subclasses" now misses *all*
classes, and a plugin scan built the same way returns an empty list that reads as "nothing
installed." Worse, the trap is self-confirming: the sanity check a developer reaches for -
"does `typeof(X).IsAssignableFrom(typeof(X))` work?" - puts the *same type on both sides*, which
is reflexively `true` in either order, so the one test written to prove the call is correct
cannot detect that the arguments are swapped. The crash-free "handles nothing" is quieter, and
worse, than the original "handles the base type only."

## 🎓 Advanced Nuance

- **`IsAssignableTo` exists because of this.** .NET 5 added `Type.IsAssignableTo(Type)` precisely
  so the common check reads in the natural direction (`derived.IsAssignableTo(base)`); on a
  modern target, prefer it and delete the argument-order coin flip.
- **ORM and DI proxies sharpen it.** A lazy-loading ORM proxy or a dynamically-generated
  interceptor has a runtime type that is a *generated subclass* of your entity - `GetType()`
  returns something like `PaymentEventProxy_a1b2`, so entities that behaved in every unit test
  fall out of a `GetType()`-keyed map the moment they come from the database.
- **`typeof` is compile-time, `GetType()` is runtime - and a variable's static type is neither.**
  `typeof(T)` in a generic uses the type argument; `GetType()` uses the object. Mixing them
  (`typeof(T) == obj.GetType()`) smuggles the exact-match trap back in whenever `T` is a base
  type.
- Same family as [0058-the-override-that-wasnt](../../inheritance/0058-the-override-that-wasnt/):
  which code runs is decided by a static, exact notion of type rather than the object's real
  place in the hierarchy.

## 🔎 How to Find It in Your Codebase

- Grep for `GetType() ==`, `GetType() !=`, a `switch` on `GetType()`, and `Dictionary<Type,` /
  `[typeof(` registries - any keyed by a type that has subclasses is the shape.
- Grep for `.IsAssignableFrom(` and check the argument order at every call: the receiver must be
  the *base*/target type, the argument the *candidate*. Reversed calls compile and return
  `false` silently.
- Symptom-side: a handler/validator/serializer that works for a base type but "forgets"
  subclasses; behavior that breaks only for entities loaded through an ORM (proxy types) or
  resolved through a DI interceptor.
- Prefer `is` / type patterns for fixed type sets and `IsAssignableTo` (.NET 5+) for dynamic
  registries; reserve exact `GetType() ==` for the rare case you mean to exclude subclasses, and
  comment it so the next reader knows it isn't this bug.
