---
id: "0072"
title: the midpoint that overflowed
category: numbers
tags: [numbers, overflow, integer-arithmetic]
rule: "never average two ints as `(a + b) / 2` - the sum **overflows**; use `a + (b - a) / 2`"
---

# #0072 - The Midpoint That Overflowed

## 💥 Symptom

A binary search that works on every test suddenly returns garbage - or throws `IndexOutOfRange` -
on a large input. A midpoint or average lands *outside* the two values it sits between: the middle
of `[1.5B, 2.0B]` comes back as a negative number. No data is corrupt, the logic reads as textbook
correct, and yet the halfway point is nowhere near the middle.

## 🔍 The Offending Code

```csharp
int mid = (low + high) / 2; // 💥 low + high overflows int before the divide
```

## 🧠 What's Actually Going On

`int` addition happens in `int`, and it wraps silently on overflow (the default `unchecked`
context). When `low + high` exceeds `int.MaxValue` (about 2.1 billion), the sum wraps to a large
negative number *before* the division runs - so `(1_500_000_000 + 2_000_000_000) / 2` is not
`1.75B`, it is `-794_967_296 / 2 = -397_483_648`. The divide faithfully halves a value that is
already wrong. Each operand fits in an `int`; their *sum* does not, and nothing warns you, because
overflow is defined behavior here, not an error.

The broken belief is "both values are valid ints, so `(a + b) / 2` is a valid int." The result is;
the intermediate `a + b` is the trap. This is the exact bug that sat in binary-search
implementations - a famous library's among them, and countless textbooks' - for years: correct for
small ranges, silently broken the moment the indices or values grow past the halfway point of
`int`.

## ✅ The Fix

Compute the offset from `low` instead of summing both endpoints - `high - low` always fits in an
`int` when both are non-negative and `low <= high`:

```csharp
int mid = low + (high - low) / 2; // no overflow
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `low + (high - low) / 2` | The standard midpoint of two ordered, non-negative ints - `high - low` cannot overflow, and the result matches `(a+b)/2` for every value where that would have worked. |
| Widen to `long` for the add | The values can be negative or unordered - `(int)(((long)a + b) / 2)` does the math in 64 bits where the sum fits, then narrows the in-range result. |
| A `checked` block | You would rather crash than wrap - `checked((a + b) / 2)` throws `OverflowException` at the sum, turning a silent wrong answer into a loud, fixable one. |
| `long` cents or `decimal` for money averages | Averaging money in cents past ~21M dollars overflows `int` too - use `long` cents or `decimal`, and decide the rounding of the halfway cent explicitly. |

## 😈 The Even Worse Sibling

The midpoint is the famous case; the same overflow hides in every "sum then divide" average.
`(a + b) / 2` for two large sensor readings, two timestamps stored as seconds, two balances in
cents - each wraps the instant the *sum* crosses `int.MaxValue`, long before either operand looks
large. And it is quiet by construction: the operands are plainly in range, the result is plainly in
range, only the invisible intermediate is not - so review sees two valid ints and a division and
moves on. Worse, it is data-dependent: the function is correct for years of small inputs and fails
the first time production feeds it two values whose sum tips over ~2.1 billion, which is exactly
when the numbers finally matter. The same overflow underlies
[0050-the-widening-that-came-too-late](../0050-the-widening-that-came-too-late/): the arithmetic
commits in the narrow type before you meant it to.

## 🎓 Advanced Nuance

- **Overflow is silent because `unchecked` is the default.** C# arithmetic wraps unless you opt into
  `checked` (or set `<CheckForOverflowUnderflow>` in the project), so `int.MaxValue + 1` is
  `int.MinValue`, not an exception - the average bug is one instance of that global default.
- **`high - low` is safe only when `low <= high` and both are non-negative.** If `low` can exceed
  `high`, or either can be negative, `high - low` can itself overflow - then widen to `long` for the
  add instead of using the subtraction trick. Know which invariant you actually hold.
- **The same shape bites multiply and shift.** `a * 2`, `a + b`, `1 << 31` all overflow an `int`
  well before the value looks large; any intermediate that outgrows the type wraps, even when the
  inputs and the final result are both in range.

## 🔎 How to Find It in Your Codebase

- Grep for `(low + high) / 2`, `(lo + hi) / 2`, `(left + right) / 2`, `(a + b) / 2`, and
  `(start + end) / 2` - the midpoint/average idiom over `int` is the shape; replace with
  `low + (high - low) / 2`.
- Look at binary searches, bisection, quicksort/mergesort pivots, and any average of two `int`s
  that can grow large (ids, timestamps-as-seconds, cents) - these are where the sum realistically
  crosses `int.MaxValue`.
- Symptom-side: a search or average correct for small inputs and wrong (or `IndexOutOfRange`) for
  large ones; a "middle" value that is negative or outside its endpoints.
- To fail loud instead of silent, wrap the arithmetic in `checked`, or turn on overflow checking for
  the project so wraps throw instead of lying.
