---
id: "0041"
title: Unboxing demands the exact type
category: boxing
tags: [boxing, unboxing, InvalidCastException]
rule: "never unbox to anything but the **exact** boxed type"
---

# #0041 - Unboxing Demands the Exact Type

## 💥 Symptom

A data-access line that has worked for years starts throwing
`InvalidCastException: Unable to cast object of type 'System.Int64' to type
'System.Int32'`. Nothing in the code changed. What changed was the *database*: the
report ran against SQL Server in dev, where `COUNT(*)` comes back as an `int`, and
against SQLite in production, where it comes back as a `long`. The offending line
is `(int)reader["Count"]` - a cast every reviewer has waved through a hundred
times, because of course an int count fits in an int. The value does fit. The
*box* does not.

## 🔍 The Offending Code

```csharp
object countCell = reader["Count"]; // boxed as long by this provider
int count = (int)countCell;         // 💥 InvalidCastException
```

## 🧠 What's Actually Going On

Unboxing is not a conversion. It is a runtime check that the box's **exact** type
equals the target, followed by a copy of the bytes back out - and if the types do
not match to the letter, it throws. No numeric widening, no narrowing, none of the
implicit rules that govern values apply to boxes.

This collides head-on with everything the language teaches about numbers. `42`
implicitly converts to `long`; `(long)42 == 42` is `true`; an `int` fits in a
`long` with room to spare. So the intuition "a long holding 42 is basically an
int" is completely reasonable - and completely irrelevant. The box is tagged
`System.Int64`. `(int)` asks for `System.Int32`. Different type, immediate
`InvalidCastException`, even though the number would fit perfectly.

The exact-match rule is total: `(decimal)` from a `double` box throws, `(int)`
from a `byte` box throws, even `(uint)` from an `int` box throws - a sign
difference is enough. The only way through is two steps: `(int)(long)countCell`
unboxes to the exact type first, *then* does an ordinary numeric conversion. And
there is one documented exception that makes the whole thing worse to learn: enum
boxes unbox to their underlying type and back - `(int)(object)DayOfWeek.Monday` is
`1` and `(DayOfWeek)(object)1` is `Monday`, both fine. The one place the rule bends
is the place people generalize from, so they expect leniency exactly where there
is none.

## ✅ The Fix

At a data border you do not control which numeric type the provider boxed, so do
not unbox - **convert**. `Convert.ToInt32` reads the box through `IConvertible` and
produces an `int` from a boxed `long`, `double`, `byte`, `decimal`, or `string`
alike:

```csharp
int count = Convert.ToInt32(countCell); // works whatever the provider boxed
```

Full version in [Good.cs](Good.cs). Choosing the approach:

| Approach | When it's the right call |
|---|---|
| `Convert.ToInt32(cell)` | The default at a border you do not own (ADO.NET, interop, deserialized data). Converts any boxed numeric; throws `OverflowException` if it genuinely does not fit |
| `(int)(long)cell` - unbox exact, then convert | You *know* the precise boxed type (one fixed provider) and want it explicit and allocation-free. Brittle: a provider change reintroduces the crash |
| `cell is long l ? (int)l : ...` | You want to branch on the real type without throwing - handle each provider shape deliberately |
| The typed accessor - `reader.GetInt32(i)` | The API offers one. Skip the `object` box entirely and let the provider do the conversion |

One caveat worth knowing: `Convert.ToInt32` of a boxed *fractional* value rounds,
and it rounds half-to-even - the same banker's rounding that surprises people in
[0025-math-round-banker](../../numbers/0025-math-round-banker/). For an integer
`COUNT` that never matters; for a boxed `double` it might.

## 😈 The Even Worse Sibling

The loud crash is the friendly version. Take the same box to pattern matching -
the modern, "safer" spelling everyone is nudged toward - and it goes quiet.
`if (countCell is int n)` does **not** throw on a boxed `long`; `is` demands the
exact type too, so it simply evaluates to `false`, and your `else` runs on a value
that "is obviously an int." A `switch` with `case int n:` falls straight through to
`default`. So refactoring `(int)countCell` into `countCell is int n` trades an
`InvalidCastException` you cannot miss for a silent wrong branch you will never
notice - the dispatcher routing the count to the "unknown type" path, no exception,
no log. Same box, one rung further down the fear ladder from crash to silently
wrong.

## 🎓 Advanced Nuance

Why the two-step works: `(int)(long)o` is two independent operations - an unbox
that must match `System.Int64` exactly, then a plain `long`-to-`int` numeric
conversion on the unboxed value. `Convert.ToInt32` does the equivalent through
`IConvertible`, and unlike a bare cast it is *checked*: a boxed `long` of
5,000,000,000 throws `OverflowException` rather than silently wrapping.

Unboxing has no inheritance to walk, which is what makes it so much stricter than
a reference cast. Casting `object` to a base or derived *class* succeeds whenever
the runtime type is assignable - the compiler and runtime follow the hierarchy.
Value types have no such hierarchy inside a box; there is exactly one right type
and everything else is an error. It is the same "the compiler allowed it, the
runtime refuses it" shape as writing through a covariant array in
[0030-array-covariance-betrayal](../../collections/0030-array-covariance-betrayal/) -
a type promise the language lets you make and the runtime declines to keep.

## 🔎 How to Find It in Your Codebase

- Grep for numeric casts of `object`-typed values: `(int)`, `(long)`, `(decimal)`,
  `(double)` applied to `reader[...]`, `row[...]`, a `Dictionary<string,object>`
  lookup, `DataRow` cells, or anything deserialized. The boxed type is the
  *source's* choice, not yours.
- No analyzer flags it - the cast is syntactically valid and only fails at runtime
  on the wrong provider - so this is a review-and-border-hardening rule, not a
  squiggle. Prefer `Convert.ToXxx` or the typed accessor at every such boundary.
- The tell is a cast that "obviously fits": `(int)` on a count, `(decimal)` on a
  price read back as `double`. Wherever a number crosses an `object` boundary from
  a database, COM/Excel interop, or a serializer, assume you do not know its boxed
  type and convert instead of unbox.
- When modernizing casts into `is`/`case` patterns over boxed numbers, remember the
  pattern is exact-type too: it will not throw, it will silently not match - review
  those changes as behavior changes, not cleanups.
