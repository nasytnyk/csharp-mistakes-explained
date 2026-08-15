---
id: "0073"
title: new List(n) is empty
category: collections
tags: [collections, List, capacity]
rule: "never treat `new List<T>(n)` as n slots - n is **capacity**, Count is 0"
---

# #0073 - new List&lt;T&gt;(n) Is Empty

## 💥 Symptom

A list you "sized" to hold N items throws `ArgumentOutOfRangeException` the moment you index into
it - `list[0]` on a brand-new `new List<int>(12)`. Or a per-slot running total silently records
nothing, because the slots were never there. The number in the constructor said 12; the list has
zero elements.

## 🔍 The Offending Code

```csharp
var monthly = new List<decimal>(12); // 💥 12 is the capacity; Count is 0 - there are no slots
monthly[month] += amount;            // ArgumentOutOfRangeException - monthly[0] does not exist
```

## 🧠 What's Actually Going On

`new List<T>(n)` takes `n` as the initial **capacity** - how much backing storage to allocate up
front so the list can grow toward `n` without reallocating - not the number of elements. `Count` is
still `0`: there are no items, nothing to index, nothing to enumerate. The capacity is a
performance hint, invisible through the public API except via `.Capacity`; it never creates
elements.

The broken belief is "`new List<int>(12)` is like `new int[12]`." It is not. `new int[12]` is a
fixed-size array of twelve zeros you can index `[0..11]` immediately; `new List<int>(12)` is an
empty, growable list that has merely pre-reserved room for twelve. The two look like twins - both
say "twelve" - and behave oppositely: the array is full of defaults, the list is empty.

## ✅ The Fix

If you want N elements, create N elements - populate the list, or use the tool that is actually
pre-sized:

```csharp
var monthly = Enumerable.Repeat(0m, 12).ToList(); // 12 real, zero-valued elements
// or, for fixed indexed slots, an array is the natural fit:
var monthly = new decimal[12];                    // 12 zeros, indexable at once
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `Enumerable.Repeat(value, n).ToList()` | You want a `List<T>` of N elements pre-filled with a default - still a growable list you can `Add` to later. |
| `new T[n]` (an array) | A fixed number of indexed slots you fill by position (months, buckets, a board) - arrays are pre-sized and index from zero at once. |
| `new List<T>(n)` on purpose | You know roughly how many you'll `Add` and want to avoid regrowth - capacity is exactly the right hint; just don't index before you `Add`. |
| `new List<T>(existingCollection)` | Copy an existing sequence - this overload *fills* the list with those elements (Count = source count), unlike the `int`-capacity overload. |

## 😈 The Even Worse Sibling

The crash is the honest outcome - `[0]` throws and you find it at once. The quiet version is code
that only ever `Add`s: growing the list with `Add` works fine, but anything that trusts the "12" -
a `for (i = 0; i < 12; i++) list[i] = ...`, a `list.Count` check that expected 12, a preallocation
that reserves 12 rows - is off by the gap between capacity and count, and nothing throws until an
index finally lands past `Count`. And the overload right beside it does the opposite:
`new List<int>(someArray)` fills the list from the array (Count = its length), so `new List<int>(12)`
(an `int`) and `new List<int>(new[]{ 12 })` (a collection) - the same constructor name - produce an
empty list and a one-element list, chosen entirely by the argument's type.

## 🎓 Advanced Nuance

- **Capacity is a hint; the list still grows on its own.** Exceeding the capacity does not throw -
  the list reallocates a larger backing array. The number only affects *when* it regrows, never what
  you can index.
- **The two `List<T>` constructors overload on `int` vs `IEnumerable<T>`.** `new List<int>(5)` picks
  the capacity ctor; `new List<int>(new[]{ 5 })` picks the fill-from-collection ctor and gives you a
  one-element list containing `5`. Same syntax shape, opposite result.
- **`Count` and `Capacity` are different properties for a reason.** `Count` is how many elements
  exist; `Capacity` is how many fit before regrowth (`Capacity >= Count`). Reading `Capacity` as
  "how many are in the list" is the same mistake one level down.

## 🔎 How to Find It in Your Codebase

- Grep for `new List<` immediately followed by indexing (`list[...]`) or a `for` loop over a fixed
  count with no `Add` in between - a capacity mistaken for a count.
- Symptom-side: `ArgumentOutOfRangeException` on a freshly-constructed list; per-slot accumulators
  (`totals[i] += ...`) that throw or record nothing; a `Count` that reads `0` right after "sizing."
- For fixed indexed slots, prefer an array (`new T[n]`) or `Enumerable.Repeat(default, n).ToList()`;
  reserve `new List<T>(n)` for the capacity hint before a series of `Add`s.
- Watch the sibling overload: `new List<T>(collection)` fills, `new List<T>(int)` reserves - make
  sure the one you wrote is the one you meant.
