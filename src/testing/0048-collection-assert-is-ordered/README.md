---
id: "0048"
title: Assert.Equal compares collections in order
category: testing
tags: [testing, xunit, Assert.Equivalent]
rule: "never assert collection equality with `Assert.Equal` when **order** is incidental"
---

# #0048 - Assert.Equal Compares Collections in Order

## 💥 Symptom

A green test suite goes red with no change to the code it tests. What changed was
underneath: a runtime upgrade, a new hash seed, a different LINQ provider, a
`GroupBy` that now enumerates its groups in another order. The failing assertion
compares two collections that hold the *same* items - the only difference is their
order - and the requirement never cared about order in the first place. The test
asserted it anyway, and now an implementation detail nobody promised is a broken
build.

## 🔍 The Offending Code

```csharp
string[] expected = ["billing", "shipping", "support"];
string[] actual   = ActiveTags();     // same three tags, order not guaranteed

Assert.Equal(expected, actual);       // 💥 compares element-by-element, in order
```

## 🧠 What's Actually Going On

`Assert.Equal`, handed two sequences, does a **positional** comparison: element 0
against element 0, and so on. Same members in a different order are not equal, and
it throws `EqualException: Collections differ` pointing at the first position that
disagrees. That is correct behaviour for a *sequence* - and wrong for a result
whose order was always incidental.

The deeper trap is that "equal" in the assertion library is not one relation. Pass
two `HashSet`s of the same items and `Assert.Equal` switches to **set** semantics
and passes - order no longer matters. Pass two field-identical objects of a plain
class with no `Equals` override and it falls back to **reference** equality and
fails. So `Assert.Equal(a, b)` means "same sequence", "same set", "same value", or
"same reference" depending entirely on the runtime types of the arguments - and
nothing at the call site tells you which. When the requirement is membership, an
`Assert.Equal` over an ordered collection quietly asserts *more* than the spec, and
any reordering the code was always free to do breaks the test. `Assert.Equivalent`
is the order-insensitive spelling that matches the requirement you actually have.

## ✅ The Fix

Assert what the requirement is. If order is incidental, use `Assert.Equivalent`;
keep `Assert.Equal` for when order genuinely is the requirement:

```csharp
Assert.Equivalent(expected, actual); // order-insensitive membership
```

Full version in [Good.cs](Good.cs). Choosing the assertion:

| Approach | When it's the right call |
|---|---|
| `Assert.Equivalent(expected, actual)` | Membership matters, order is incidental - `GroupBy`, `Dictionary` keys, set-like results. Passes on any ordering with the right elements |
| `Assert.Equal(expected, actual)` | Order **is** the requirement - a sorted result, a sequence, a pipeline's output order. Assert it deliberately |
| `Assert.Equal(expected.OrderBy(x), actual.OrderBy(x))` | You want an ordered comparison over a canonical sort - sort both sides **in the test**, never in production |
| `Assert.Equivalent(expected, actual, strict: true)` | Membership and count must match exactly (no extra elements), still order-insensitive |

## 😈 The Even Worse Sibling

The fix people actually reach for is worse than the bug: they add `OrderBy` to the
*production* code to make the test pass - a sort nobody asked for, with its cost,
shipped to satisfy an assertion that over-specified. The test was wrong and they
changed the code. The other direction is quieter: a type refactor silently weakens
the suite. Change a production return from `List<T>` to `HashSet<T>` and the same
`Assert.Equal` flips from ordered to set-wise - still green, now checking strictly
less. It no longer notices an order regression, and it no longer notices a
duplicate. The assertion's strength changed with a type, and the test still reads
exactly as it did.

## 🎓 Advanced Nuance

The one-method-many-relations behaviour is the thing to internalize. `Assert.Equal`
is a sequence check over `IEnumerable`, a set check over `ISet`, a value check when
the type implements `IEquatable`/overrides `Equals`, and a **reference** check when
it does not - so two field-identical DTOs of a plain class *fail* `Assert.Equal`
while `Assert.Equivalent` passes, because Equivalent compares members structurally
rather than asking the type. It is the same "you never defined equality" gap that
makes `Distinct` keep duplicates in
[0013-distinct-that-didnt](../../linq/0013-distinct-that-didnt/) - here it surfaces
as a test that fails on values that are, by every field, identical.

Records paper over the DTO half of this (they generate `Equals`, so record DTOs
pass `Assert.Equal` by value) but not the collection-order half. And asserting on
`Dictionary`/`GroupBy` iteration order is asserting an implementation detail the
BCL explicitly does not guarantee and has changed between runtimes.

## 🔎 How to Find It in Your Codebase

- Grep for `Assert.Equal(` (and `CollectionAssert.AreEqual` in MSTest, `Assert.That(
  ..., Is.EqualTo(...))` in NUnit) where the arguments are collections whose order
  is not the requirement - `GroupBy`, `ToDictionary`, `Keys`/`Values`, parallel
  results, SQL without `ORDER BY`. Switch those to `Assert.Equivalent` (or
  `CollectionAssert.AreEquivalent` / `Is.EquivalentTo`).
- The strongest tell is `OrderBy` in **production** code whose only purpose is to
  make a test pass. That sort is a symptom of an over-specified assertion; the fix
  belongs in the test, not the code.
- Watch `Assert.Equal` on a plain-class DTO - it is a reference check unless the
  class overrides `Equals`. Use a `record`, an explicit comparer, or
  `Assert.Equivalent`.
- No analyzer flags this; it is a test-design review point. Ask of every collection
  assertion: does the requirement care about order, or only about membership?
