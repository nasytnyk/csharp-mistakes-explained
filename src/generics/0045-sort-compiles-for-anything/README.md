---
id: "0045"
title: Sort compiles for anything
category: generics
tags: [generics, IComparable, OrderBy]
rule: "never `OrderBy` a type with no **ordering** defined"
---

# #0045 - Sort Compiles for Anything

## 💥 Symptom

A report that has passed every test dies in staging with
`InvalidOperationException: Failed to compare two elements in the array.`, its
inner exception reading `At least one object must implement IComparable.` The stack
trace points into `Array.Sort` / LINQ's sorter, not at any line you wrote. And it is
keyed to size: the unit tests, each with a single fixture row, are green; the first
multi-row batch is red. Nothing changed in the code between the passing test and the
failing run except the number of rows.

## 🔍 The Offending Code

```csharp
var sorted = rows.OrderBy(r => r.Period).ToList(); // 💥 Period is a record - no CompareTo
```

`Period` is a `record`. `OrderBy` accepted it without a murmur at compile time.

## 🧠 What's Actually Going On

`OrderBy`, `List<T>.Sort`, and `Array.Sort` put **no `IComparable` constraint** on
their element type. They defer to `Comparer<T>.Default`, which is resolved at
runtime: if `T` implements `IComparable<T>` or `IComparable`, it is used; otherwise
the *first actual comparison* throws. So the type check that could have been a
compile error is instead a runtime one, and it fires only when two elements are
compared.

That "only when two elements are compared" is the trap's timing. A zero- or
one-element sort performs no comparisons and completes happily, so a test with a
single row certifies code that cannot survive two. The crash waits for the first
input big enough to compare something.

And records walk straight into it. The compiler generates a record's `Equals`,
`GetHashCode`, `ToString`, and `with`-copy - value *equality* - but it does **not**
generate `CompareTo`. Equality and ordering are different contracts, and only the
first is synthesized. So ordering by a record, or by a record key, compiles (no
constraint), passes the one-row test (no comparison), and throws on the first real
comparison - exactly the members the record *did* get for free
([0028-with-copies-the-reference](../../records/0028-with-copies-the-reference/) is
the same "the record gave you some of it, not all of it" boundary).

## ✅ The Fix

Order by something the runtime knows how to compare. A tuple of the key's fields is
the smallest change - `ValueTuple` implements structural comparison, field by field:

```csharp
var sorted = rows.OrderBy(r => (r.Period.Year, r.Period.Month)).ToList();
```

Full version in [Good.cs](Good.cs). Choosing the approach:

| Approach | When it's the right call |
|---|---|
| `OrderBy(x => (a, b, ...))` - a tuple of the fields | The quickest fix - `ValueTuple` compares structurally, so you get field-by-field ordering for free |
| `OrderBy(x => a).ThenBy(x => b)...` | An explicit multi-key sort that reads clearly at the call site |
| Implement `IComparable<T>` on the type | The type has one natural order used in many places - define it once and every `Sort`/`OrderBy` works |
| Pass an `IComparer<T>` to `OrderBy`/`Sort` | The order is context-specific, not intrinsic to the type |

## 😈 The Even Worse Sibling

Records are the accelerant. They hand you `==`, `Equals`, `GetHashCode`,
`ToString`, and `with` for free, so assuming `CompareTo` rode along in the same gift
box is nearly reasonable - but the compiler generates equality, never ordering. And
the failure is size-gated in the cruelest way: the one-row fixture that every test
suite starts from does zero comparisons and passes, so the bug is invisible to
precisely the tests written to catch regressions. It ships green, then throws on the
first production batch with two rows to compare, from a stack trace deep in framework
sort internals that never names the record with no order. A crash you cannot
reproduce with your test data is worse than one you can.

## 🎓 Advanced Nuance

The tuple-versus-record split is the sharp edge. `ValueTuple<int, int>` and
`System.Tuple<...>` implement `IComparable`/`IStructuralComparable`, so
`OrderBy(x => (x.Year, x.Month))` sorts; a `record Period(int Year, int Month)` with
the identical fields does not, because the compiler synthesizes `CompareTo` for
tuples and not for records. Same-looking data, opposite orderability.

It is `Comparer<T>.Default` doing the runtime resolution, which is why implementing
`IComparable<T>` on the type fixes `OrderBy`, `List.Sort`, `Array.Sort`, and the
sorted collections all at once. Note those sorted collections fail *earlier* and
differently: `SortedSet<T>`, `SortedDictionary<TKey, _>`, and `SortedList<TKey, _>`
throw on the **second** `Add` - the first insertion needs no comparison, the second
does. Enums, by contrast, are comparable (by underlying value), and so are the
primitive types; it is records, anonymous types, and plain classes without
`IComparable` that fall through.

## 🔎 How to Find It in Your Codebase

- Grep for `OrderBy`, `OrderByDescending`, `.Sort(`, `SortedSet<`,
  `SortedDictionary<`, `SortedList<` where the key or element type is a `record`, an
  anonymous type, or a class with no `IComparable<T>`. Each compiles and waits.
- No analyzer flags it - the missing comparability is legal by design, resolved at
  runtime. This is a review-and-test rule.
- In tests, sort **two or more** rows, never one. A single-row fixture does no
  comparison and cannot fail; it is the exact shape that lets this ship.
- The production tell is `Failed to compare two elements` with an inner
  `At least one object must implement IComparable`, and a stack in `Array.Sort` /
  `EnumerableSorter` rather than your code - read it as "the thing I sorted by has no
  order", and look at the key type, not the sort call.
