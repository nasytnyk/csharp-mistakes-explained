---
id: "0050"
title: The widening that came too late
category: numbers
tags: [numbers, overflow, integer-arithmetic]
author: palkotnyk
rule: "never trust a **wide** target type to fix `int` math - cast one operand first"
---

# #0050 - The Widening That Came Too Late

## 💥 Symptom

A value that could not possibly be wrong is wrong. A 30-day retention window comes out
**negative**. A "30 days in milliseconds" timeout is a negative duration; a total-bytes
counter wraps below zero; a success rate prints `0%`. The variable is a `long` (or a
`double`) - a wide type chosen *on purpose*, for headroom or precision - which is exactly
what makes the bug feel impossible. And it passed every test: the small durations in the
fixtures fit fine; only the real production value tips it over.

## 🔍 The Offending Code

```csharp
int retentionDays = 30;                                  // from config
long retentionMs = retentionDays * 24 * 60 * 60 * 1000;  // 💥 -1702967296, not 2_592_000_000
```

The `long` was chosen for headroom. It never got the chance to hold it.

## 🧠 What's Actually Going On

The **operand** types decide the arithmetic - the target type does not get a vote.
`retentionDays * 24 * 60 * 60 * 1000` is `int * int * ...`, so it is an **`int`**
operation, computed and stored in 32 bits. The true product, 2,592,000,000, is larger
than `int.MaxValue` (2,147,483,647), so it wraps to **-1,702,967,296** - and only *that*,
the already-broken `int`, is widened to `long` on the way into `retentionMs`. The `long`
did its job perfectly: it faithfully stored a number that was garbage before it arrived.

The broken belief is "the result type governs the math." It governs where the result is
*put*, not how it is *computed*. The conversion is real - it just runs one step too late,
after the operator has already lost the value.

The same gap has a second face in division. `int / int` is **integer** division no matter
what it flows into: `double rate = passed / total;` computes `7 / 8 == 0` in `int`, *then*
widens the `0` to `0.0`. A rate that is genuinely 87.5% reports as 0% - the truncation
happened before the `double` ever saw it.

## ✅ The Fix

Widen **one** operand *before* the operator, so the whole expression runs wide from that
point on:

```csharp
long retentionMs = (long)retentionDays * 24 * 60 * 60 * 1000; // 2_592_000_000
double rate      = (double)passed / total;                    // 0.875
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| Cast the **first** operand wide - `(long)days * ...`, `(double)passed / total` | The default. One cast at the start promotes every operator after it, so no intermediate product runs narrow. |
| Type a literal wide early - `24L * 60 * 60 * 1000`, `passed / (double)total` won't do it | The constant is what pushes it over. A wide literal promotes from its position on - but only downstream of it. |
| `checked { ... }` (or `<CheckedForOverflow>` in the csproj) | You'd rather it throw than wrap. Turns the silent garbage into an `OverflowException` you cannot ship past. |
| A purpose-built type - `TimeSpan.FromDays(30).TotalMilliseconds`, `decimal` for money | Duration and money math have types that sidestep the raw `int` multiply entirely. |

## 😈 The Even Worse Sibling

The trap is aimed squarely at your test data. `2 * 24 * 60 * 60 * 1000` - two days in ms -
is 172,800,000, comfortably inside `int`, so every fixture with a modest duration passes
and the code ships "verified." Only the real 30-day window (or a real multi-GB file size,
or a `50000 * 50000` row count) crosses 2^31 in production. The bug scales *in* with
success: the bigger and more real the input, the more certainly it overflows. And because
overflow is **unchecked by default**, there is no exception at the seam - just a `long`
(or `double`) holding a confident, wrong number that flows downstream into a timer, a
quota, or a bill, wearing a type that swears it had room.

## 🎓 Advanced Nuance

- **The pure-literal version doesn't even compile.** `long ms = 30 * 24 * 60 * 60 * 1000;`
  is a *constant* expression, and C# evaluates constants in a **checked** context at
  compile time - so it is a build error, `CS0220: The operation overflows at compile time
  in checked mode`. The overflow only survives to runtime when at least one operand is
  *not* a compile-time constant - a config value, a method parameter, a field - which is
  exactly how it reaches real code. That is why the demo reads `int retentionDays = 30;`
  and not the bare product.
- **`var` would have told you.** Written `var ms = retentionDays * 24 * 60 * 60 * 1000;`,
  the inferred type is `int`, and the mismatch is visible right at the declaration. The
  explicit `long` annotation is what disguises the wrong-typed result as intentional.
- **Where you put the cast matters.** `(long)days * 24 * 60 * 60 * 1000` is safe because
  the *first* operator already runs in `long`. Casting a *later* operand leaves every
  earlier intermediate product running in `int`, where it can still overflow before the
  widening arrives. Cast first.
- **The division face is the same rule.** `(1 + 2) / 2 == 1`, `7 / 8 == 0`: `int / int`
  truncates toward zero and *then* widens. `(double)passed / total` (one cast, first
  operand) is the fix; `(double)(passed / total)` is not - it widens the already-truncated
  `0`.

## 🔎 How to Find It in Your Codebase

- Grep for `long` or `double` locals and fields assigned from an all-`int` expression -
  especially `* 1000`, `* 60`, `* 1024`, `* 100` chains (durations, byte sizes, money in
  minor units) and `a / b` assigned to a `double` (rates, averages, percentages).
- The tell is a **wide** declared type fed by **narrow** arithmetic with no cast on any
  operand. Rewrite the same expression with `var` and hover the inferred type - if it says
  `int`, the widening comes too late.
- Turn on overflow checking in the builds you can afford to: `<CheckedForOverflow>true</CheckedForOverflow>`
  in a Debug or test `.csproj` turns every silent wrap into an `OverflowException` at the
  exact line, converting a scale-dependent production bug into a deterministic test failure.
- No general analyzer flags "int math widened too late"; treat the wide-type-from-narrow-math
  shape as a review smell, and reach for `TimeSpan` / `decimal` / `checked` at the
  arithmetic, not at the assignment.
