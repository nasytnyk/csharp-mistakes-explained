---
id: "0056"
title: one null poisons the whole sum
category: nullability
tags: [nullability, decimal, arithmetic]
rule: "never sum nullables with raw `+=` - a single **null** turns the whole total null; use `?? 0` or `.Sum()`"
---

# #0056 - One Null Poisons the Whole Sum

## 💥 Symptom

A report shows a total of `0` - or blank - for an account that clearly has charges. The
line items are right there, most with real amounts, and yet the sum comes out empty. Nobody
wrote `total = 0`. One line had a null amount - an unpriced fee, an optional adjustment, a
column that allows NULL - and that single null did not just skip itself: it erased the
entire total.

## 🔍 The Offending Code

```csharp
decimal? total = 0m;
foreach (var amount in lineAmounts) // amount is decimal?
    total += amount;                // 💥 total + null == null, and it never recovers
```

## 🧠 What's Actually Going On

Nullable arithmetic **propagates null**. For `Nullable<T>`, every lifted operator - `+`, `-`,
`*`, `/` - returns null the moment *either* operand is null: `total + null` is `null`, not
`total`. So a running sum built with `+=` over `decimal?` (or `int?`, `double?`) is poisoned
by the first null it meets, and because `null + x` is also null, it can never recover - the
whole total is null even if only one line out of a thousand was.

The broken belief is "adding a null just skips it, like adding zero." It does not: null is
not zero, and a lifted `+` has no notion of "skip." It faithfully computes "an unknown value
plus 10 is still unknown." Then the null total renders as an empty string or, after a `?? 0`
at the *display* layer, as a clean and wrong `0` - so the bug arrives dressed as a legitimate
zero.

## ✅ The Fix

Coalesce each nullable term to a real value before adding it - decide what a null *means*
(usually 0) at the point of summation:

```csharp
decimal total = 0m;
foreach (var amount in lineAmounts)
    total += amount ?? 0m;   // an unpriced line contributes 0, not null
```

Or let LINQ do it - `Sum` over a nullable sequence skips nulls and returns a non-null result:

```csharp
decimal total = lineAmounts.Sum(a => a ?? 0m);
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| Coalesce each term - `total += amount ?? 0m` | The default when null means "0 / not applicable." Decide the meaning at the add, not at display. |
| `Enumerable.Sum` over the nullable sequence | `nullables.Sum(x => x ?? 0)` (or `.Sum()`, which skips nulls) - idiomatic, no manual accumulator. |
| Keep the total nullable *on purpose* | Null genuinely means "unknown, cannot total yet" - then propagation is correct, and you must handle the null total explicitly, never `?? 0` it away at the end. |
| Filter first - `.Where(a => a.HasValue)` | You want the sum of only the known values plus a separate count of the unknown ones. |

## 😈 The Even Worse Sibling

`Sum` doing the *opposite* is its own trap. `lineAmounts.Sum()` over `IEnumerable<decimal?>`
**ignores** nulls and returns the total as if they were zero - often right, but it means a
column that is *entirely* null sums to `0`, not null, so "no data" and "data that totals
zero" become indistinguishable. And `Average` over the same sequence divides by the
**non-null** count, so the more values go missing the *better* the average looks. The manual
`+=` loop errs toward null - the total vanishes; `Sum`/`Average` err toward a confident wrong
number - missing data flatters the result. Same nullable column, opposite lies, chosen by
which tool you reached for.

## 🎓 Advanced Nuance

- **It is not only `+`.** Every lifted operator propagates: `a * b`, `a - b`, `a / b`, and
  comparisons run in three-valued logic - `null > 5` is *false*, and so is `null <= 5`. A
  `decimal?` running maximum written as `if (x > max) max = x;` silently stops updating once a
  null enters the comparison.
- **The null total usually hides behind a late `?? 0`.** `return total ?? 0;` at the boundary
  converts the poisoned null into a plausible `0`, moving the evidence far from the loop that
  caused it - so the investigation finds a zero, not a null, and the arithmetic looks
  innocent.
- **Nullable-value propagation mirrors `?.` on references.** `obj?.Prop` yields null if `obj`
  is null; `x + y` yields null if either is null. Both are "an unknown in, an unknown out" -
  convenient for guarding, dangerous for accumulating.

## 🔎 How to Find It in Your Codebase

- Grep for `+=`, `-=`, `*=` whose right-hand side is a nullable (`decimal?`, `int?`,
  `double?`) - a running accumulator over nullable values is the shape.
- Look for totals declared `decimal?` / `int?` that get a `?? 0` only at the very end
  (display, return, DTO mapping) - that trailing `?? 0` is treating a symptom the loop
  created.
- Symptom-side: reports showing `0` or blank totals for entities that clearly have data; a
  "sum" that flips to null the instant one optional field is unset.
- Prefer `.Sum(x => x ?? 0)` or an explicit per-term coalesce over a raw nullable `+=`, and
  keep a total nullable only when "unknown" is a real, deliberately handled outcome.
