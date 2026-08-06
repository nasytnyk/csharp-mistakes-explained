---
id: "0044"
title: Covariance skips value types
category: generics
tags: [generics, variance, IEnumerable]
rule: "never expect `IEnumerable<object>` to match a **value-type** list"
---

# #0044 - Covariance Skips Value Types

## 💥 Symptom

An "export anything" or "log any payload" pipeline works for years, then a report
comes back with a blank column. The code special-cases collections - one row per
item - and it does that correctly for a `List<string>` of tags but treats a
`List<int>` of scores as a single opaque value, exporting one row that reads
`System.Collections.Generic.List`1[System.Int32]`. The same `is` check that says
"yes, a sequence" for the text list says "no, a scalar" for the number list. It
passed every test, because the test payloads were words.

## 🔍 The Offending Code

```csharp
if (payload is IEnumerable<object> items) // 💥 true for List<string>, false for List<int>
    WriteRowPerItem(items);
else
    WriteSingleRow(payload);              // value-type collections land here
```

## 🧠 What's Actually Going On

`IEnumerable<out T>` is covariant, so `IEnumerable<string>` is an
`IEnumerable<object>` and a `List<string>` passes the check. But generic variance
is defined **only for reference conversions**. Converting `string` to `object` is a
reference conversion - the same reference, just viewed as a base type, no bits
change. Converting `int` to `object` is a **boxing** conversion: it allocates a box
and changes the representation, and variance explicitly excludes any conversion that
changes representation. So `IEnumerable<int>` is simply not an `IEnumerable<object>`,
and `List<int>` fails the `is`.

The two lists are indistinguishable at the call site - same `List<T>`, same shape,
same everything but the element's kind - yet one satisfies the interface and the
other does not, decided entirely by whether the element type is a class or a struct.
The broken belief is "a collection of anything is a collection of `object`". It is
true for reference elements and false for value elements, and nothing in the source
shows which side of the line a given list is on. Arrays split the same way:
`string[]` matches, `int[]` does not.

## ✅ The Fix

Probe with the **non-generic** `System.Collections.IEnumerable`, which every
`List<T>` and array implements regardless of element type, then box each element as
you read it:

```csharp
if (payload is IEnumerable items)     // every List<T>/array, value or reference
    WriteRowPerItem(items.Cast<object>());
else
    WriteSingleRow(payload);
```

Full version in [Good.cs](Good.cs). Choosing the approach:

| Approach | When it's the right call |
|---|---|
| `is IEnumerable` (non-generic) + `.Cast<object>()` | The default for "iterate any collection" - it matches value-element sequences too; boxing happens once, as you read |
| Make the method generic - `Handle<T>(IEnumerable<T>)` | You control the call and can carry the element type through - no `object`, no boxing at all |
| `is IEnumerable<object>` | Only when you *mean* to accept reference-element sequences and skip value-element ones |

Watch one edge: the non-generic `IEnumerable` also matches `string` (a string is an
`IEnumerable<char>`). If a bare string should not be exploded into characters,
exclude it: `payload is IEnumerable and not string`.

## 😈 The Even Worse Sibling

Nothing throws. The `List<int>` does not error - it quietly takes the scalar
branch, so the failure is a number column that silently goes blank, a log line that
collapses a hundred values into one `List`1[System.Int32]`, a metrics export missing
exactly its numeric series. And the trap is aimed at your test suite: people write
example payloads as words - names, tags, statuses - so the `is IEnumerable<object>`
check is exercised only against reference-element collections, passes green, and
ships. The first `List<int>`, `List<decimal>`, or `List<DateTime>` batch in
production is the first time the other branch ever runs. The crash in this exhibit
is a courtesy; the real bug loses data without a sound.

## 🎓 Advanced Nuance

The rule is exact: a variance conversion is valid only when there is an *implicit
reference conversion* (or identity) between the type arguments. `string`-to-`object`
qualifies; `int`-to-`object` is a *boxing* conversion, which is implicit but **not**
a reference conversion, so it is excluded. The same wall stops `IReadOnlyList<int>`
from being `IReadOnlyList<object>`, `Func<int>` from being `Func<object>`, and every
other `out`/`in` position over a value type.

Arrays are the cautionary cousin. Reference-type arrays get their own built-in
covariance - `string[]` *is* `object[]` - but it is the unsafe kind that throws at
runtime on a bad write, the subject of
[0030-array-covariance-betrayal](../../collections/0030-array-covariance-betrayal/).
Value-type arrays get none of it: `int[]` is not `object[]` at all. So value types
are the consistent outsiders - excluded from generic variance, and excluded from
array covariance too - while reference types get variance that is sometimes safe
(generics) and sometimes a runtime landmine (arrays).

## 🔎 How to Find It in Your Codebase

- Grep for `IEnumerable<object>`, `IReadOnlyList<object>`, `ICollection<object>` in
  `is`/`as`/cast positions inside "handle any payload" code. Each silently skips
  collections of value-type elements.
- Prefer the non-generic `System.Collections.IEnumerable` (plus an `and not string`
  guard) for "any sequence", or a generic method that keeps the element type. No
  analyzer flags the covariance miss - it is legal code that just answers `false`.
- In review, insist the tests include a `List<int>`/`List<decimal>` payload, not
  only `List<string>`. A string-only fixture is exactly what lets this ship.
- The production tell is a numeric column, series, or field that is blank or shows a
  type name where a list of values belongs - the value-element collection took the
  scalar path.
