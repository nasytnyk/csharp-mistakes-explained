---
id: "0042"
title: Comparing boxed values with ==
category: boxing
tags: [boxing, equality, reference-equality]
rule: "never compare **boxed** values with `==`"
---

# #0042 - Comparing Boxed Values with ==

## 💥 Symptom

Something is permanently "dirty". A settings screen fires `Changed` every time you
touch a field, even when you re-select the value it already had. A cache
invalidates on every write of the same data. An observable re-renders on
assignments that changed nothing, events cascade, and the giveaway is sitting in
the log: `value changed: 5 -> 5`. The change-detection guard that is supposed to
short-circuit "no real change" never fires the short-circuit - and it only started
misbehaving after the backing field was made `object` to "hold any value".

## 🔍 The Offending Code

```csharp
object? current;                 // object-typed backing store
void Set(object value)
{
    if (current != value)        // 💥 object != is reference comparison; two boxes of 5 are never ==
    {
        current = value;
        Raise("changed");        // fires on every set, even value -> same value
    }
}
```

## 🧠 What's Actually Going On

The C# compiler picks which `==`/`!=` to use from the **static** types of the
operands, at compile time. Between two `object` operands it emits *reference*
equality - is this the same heap object? - with no operator-overload lookup on the
runtime type and no value semantics. `int == int` is a value comparison;
`object == object` is an identity comparison; the source text looks identical.

Now add boxing. Every time a value type is stored in an `object`, the runtime
allocates a fresh box on the heap, and .NET **interns none of them**. Box `5`
twice and you get two distinct objects; the same is true of `true`, of `0`, of an
enum member, even of the same variable boxed twice. So `current != value` is asking
whether two *different boxes* are the same object - and they never are. The values
are equal (`current.Equals(value)` is `true`); the boxes are not the same
(`ReferenceEquals` is `false`); and `!=` reports the boxes, so it fires every time.

The broken belief is "`!=` compares values". It compares values for `int`, and for
`string` (which overloads `==` to compare text) - the two types everyone learns
first. For `object` it compares references, and boxing guarantees the references
differ. Same operator, opposite meaning, decided entirely by the declared type.

## ✅ The Fix

Compare by value. `object.Equals(a, b)` is the static, null-safe form - it handles
`null` on either side and dispatches to the values' own `Equals`:

```csharp
if (!Equals(current, value)) // value comparison, null-safe
{
    current = value;
    Raise("changed");
}
```

Full version in [Good.cs](Good.cs). Choosing the tool:

| Approach | When it's the right call |
|---|---|
| `!Equals(current, value)` (static `object.Equals`) | The default for object-typed storage - null-safe, value semantics, one call |
| Type the field/property concretely (`int`, the real type) | You know the type - then `==` *is* value equality and nothing boxes at all |
| `EqualityComparer<T>.Default.Equals(a, b)` | Generic code - the idiomatic value comparison; for a value-type `T` it avoids boxing entirely |
| A custom `IEqualityComparer` | Domain values whose "equal" is not their default `Equals` |

Note `object.Equals(a, b)` (static) over `a.Equals(b)` (instance): the static form
does not throw when `current` is `null`, which the very first set always is.

## 😈 The Even Worse Sibling

This bug heals and relapses on a diff that "does not touch the logic". Type the
field `int` and `!=` is value equality - correct. Later someone generalizes the
store to `object` so it can hold any setting, changes no comparison, and the guard
silently becomes reference equality - the bug is back, introduced by an edit that
never went near the `!=`. And because `string`'s value-comparing `==` has trained
everyone that `==` means "same value", the object-typed version reads as obviously
safe in review. It never crashes, so it does not announce itself: it just spends
CPU and fires events forever on data that never changed, which reads as a
performance problem, not the correctness bug it is - the same reference-equality
surprise that makes `Distinct` miss duplicates in
[0013-distinct-that-didnt](../../linq/0013-distinct-that-didnt/).

## 🎓 Advanced Nuance

.NET caches no boxes - not the way the JVM caches small `Integer`s (-128..127).
Every boxing conversion allocates, so you can never rely on "small values box to
the same object": `false`, `0`, and `DayOfWeek.Monday` each mint a new box on every
conversion (verified on .NET 10). There is no value of any type for which
`(object)x == (object)x` from two separate boxings is `true`.

The operator is resolved statically, which is the whole trap: `==` between `object`
operands is reference, between `int` operands is value, between `string` operands
is `string`'s overload. `CA2013` catches the related `ReferenceEquals(valueType,
...)` mistake, but not this one - `object != object` is a perfectly legal reference
comparison, so no analyzer objects. The only defense is knowing that an
`object`-typed `==`/`!=` means identity, and reaching for `Equals` at every
object-typed boundary.

## 🔎 How to Find It in Your Codebase

- Grep for `==` and `!=` where an operand is `object`-typed: `object` fields and
  properties, `object`-typed method parameters, `Dictionary<_, object>` values,
  `object[]` elements. Each is an identity comparison wearing a value-comparison's
  clothes.
- The prime habitat is a change-detection guard - `if (_field != value)`,
  `SetProperty`, `INotifyPropertyChanged` setters, dirty-flag checks, cache
  "did-it-change" tests - over an object-typed backing field. Replace with
  `Equals(a, b)` or a typed field.
- No analyzer flags `object != object`; treat it as a review rule. When a field is
  widened to `object` in a refactor, re-check every comparison against it - the
  meaning of `==` changed even though the line did not.
- Watch the log for "changed: X -> X" style messages and cache/observer traffic
  that scales with writes rather than with real changes; that pattern is this bug's
  signature.
