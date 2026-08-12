---
id: "0057"
title: Except silently dedups
category: linq
tags: [LINQ, set-operations, Distinct]
rule: "never use `Except` to filter a list - set operators return **distinct** results, dropping duplicates"
---

# #0057 - Except Silently Dedups

## 💥 Symptom

A batch comes out short. You filter a list to drop a handful of excluded items -
`list.Except(excluded)` - and the result has fewer rows than "input minus the few you
removed." Units disappear that were never on the exclusion list: a pick list loses a
duplicate, a payment batch drops one of two identical amounts, a "send to everyone except
the unsubscribed" run silently skips repeated recipients. The count does not add up, and the
exclusion set does not explain the gap.

## 🔍 The Offending Code

```csharp
string[] pickList   = { "widget", "widget", "gadget", "gizmo" }; // two widgets
string[] outOfStock = { "gizmo" };

var toPull = pickList.Except(outOfStock); // 💥 -> { "widget", "gadget" } - one widget vanished
```

## 🧠 What's Actually Going On

`Except`, `Intersect`, `Union`, and `Distinct` are **set** operations, and a set holds no
duplicates. Each runs its input through distinct-semantics: `Except` yields the *distinct*
elements of the first sequence that are not in the second. So `pickList.Except(outOfStock)`
does two things, not one - it removes the out-of-stock items **and** collapses every
remaining duplicate to a single occurrence. The second `"widget"` was excluded by nothing;
it is dropped because a set cannot hold it twice.

The broken belief is "`a.Except(b)` is `a` minus `b`, leaving `a` otherwise intact" - a
filter. It is set *difference*. The gap only appears when the input has legitimate duplicates
- units, amounts, repeated keys - which is exactly the data where losing them matters and
where a quick test over unique sample values never reveals it.

## ✅ The Fix

When you mean "keep every element of the list except these," filter - do not set-difference:

```csharp
var toPull = pickList.Where(x => !outOfStock.Contains(x));   // preserves duplicates and order

// for a large exclusion set, make lookups O(1):
var excluded = outOfStock.ToHashSet();
var toPull = pickList.Where(x => !excluded.Contains(x));
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `Where(x => !excluded.Contains(x))` | You are filtering a *list* (a multiset) - duplicates and order are data. Wrap the exclusions in a `HashSet` for O(1) lookups. |
| `Except` / `Intersect` / `Union` | You genuinely want *set* semantics - distinct membership, duplicates irrelevant. The dedup is the feature. |
| `GroupBy` / carry an explicit count | Duplicates mean quantities you want to keep or aggregate - model the count instead of relying on repetition. |
| `ExceptBy` / `IntersectBy` (.NET 6+) | You want set semantics on a *key*, not the whole element - still distinct, just by a projection. |

## 😈 The Even Worse Sibling

`Union` is the same trap with a friendlier face: `a.Union(b)` is not "a followed by b" - it is
the *distinct* union, so it silently drops duplicates inside `a`, inside `b`, and across them.
Reach for it to merge two lists ("last month's orders plus this month's") and any legitimately
repeated row disappears; the concatenation you actually wanted is `a.Concat(b)`, which keeps
everything. And `Distinct` is the mirror-image failure: it dedups by the element's *default*
equality, which for a plain class is **reference** equality - so `Distinct` on a list of
freshly-built objects removes nothing at all. The set operators dedup too much here and too
little there, from the same machinery - see [0013-distinct-that-didnt](../../linq/0013-distinct-that-didnt/).

## 🎓 Advanced Nuance

- **All four are equality-comparer driven and distinct by result.**
  `Except`/`Intersect`/`Union`/`Distinct` compare with `EqualityComparer<T>.Default` (or one
  you pass) and emit each distinct element once, in first-seen order. `Concat` and `Where` do
  not dedup - reach for those whenever duplicates are data.
- **A zero-item `Except` is a hidden `Distinct`.** `list.Except(Array.Empty<T>())` removes
  nothing yet still returns `list.Distinct()` - so even a "no-op exclusion" quietly
  deduplicates. Any `Except` over duplicate-bearing data is a `Distinct` in disguise.
- **Exclusions are membership-only, which hides the cost.** Because it is set difference,
  excluding `"gizmo"` once or a hundred times is identical - convenient, and the very same
  distinct-machinery that eats the first sequence's duplicates. One behavior, two
  consequences.

## 🔎 How to Find It in Your Codebase

- Grep for `.Except(`, `.Union(`, `.Intersect(`, `.Distinct(` applied to lists that can hold
  duplicates - orders, line items, amounts, recipients, log rows - anywhere a repeated value
  is legitimate data.
- The tell: a set operator used to *filter* or *merge* a list where the intent was
  `Where(x => !...Contains(x))` or `Concat`. "List minus a few" is `Where`; "both lists" is
  `Concat`.
- Symptom-side: post-filter counts lower than `input - excluded`, batches that lose duplicate
  units, merged lists shorter than the sum of their parts.
- No analyzer flags "set op on a multiset." Treat any `Except`/`Union`/`Intersect` on a list
  whose duplicates matter as a review question: did you mean a set, or a filtered list?
