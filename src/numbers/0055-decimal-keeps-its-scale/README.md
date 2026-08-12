---
id: "0055"
title: decimal keeps its scale
category: numbers
tags: [numbers, decimal, formatting]
rule: "never key or compare a `decimal` by its **text** - equal values keep different scales (`1.5m` vs `1.50m`)"
---

# #0055 - decimal Keeps Its Scale

## 💥 Symptom

Reconciliation reports a discrepancy that is not there. Two systems quote the same unit
price - one as `1.5`, the other as `1.50` - and a "distinct prices" check flags them as two
different prices. Or an audit log records a "price changed" event where nothing changed. Or
a UI shows `1.5` on one screen and `1.50` on another for the same item. The numbers are
equal - `==` says so - yet everything that touches their *text* sees two values.

## 🔍 The Offending Code

```csharp
decimal catalog = 1.5m, invoice = 1.50m;
catalog == invoice;                        // true
catalog.ToString() == invoice.ToString();  // 💥 false - "1.5" vs "1.50"
```

## 🧠 What's Actually Going On

A `decimal` stores not just a value but a **scale** - the number of digits it keeps after
the point - and it *preserves* it. `1.5m` has scale 1, `1.50m` has scale 2, and each carries
that scale around for the rest of its life. Equality and hashing **normalize** the scale
away: `1.5m == 1.50m` is true, `Equals` is true, and their hash codes match, so they are the
same key in a `Dictionary<decimal, _>` or `HashSet<decimal>`. But formatting does **not**
normalize: `ToString()`, string interpolation, `JsonSerializer.Serialize`, and most
on-the-wire encodings emit the trailing zeros exactly as stored - so equal decimals produce
unequal text.

The broken belief is "equal values are interchangeable, so I can compare, store, or key them
by their string form." For `decimal` that is false: the *value* ignores scale, the
*representation* does not. Anything that uses a decimal's text as a proxy for its value - a
`HashSet<string>` of prices, a serialized-snapshot change detector, a string-keyed cache, a
`==` between formatted amounts - treats `1.5` and `1.50` as different money.

## ✅ The Fix

Compare, key, and dedup by the decimal *value*, never by its text:

```csharp
var pricesSeen = new HashSet<decimal>(); // not HashSet<string>
```

And when you genuinely need text - display, storage, an API contract - normalize the scale
explicitly so it is consistent. Full version in [Good.cs](Good.cs); the mistake is
[Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| Compare / key / dedup by the `decimal` value | The default. Equality and `GetHashCode` already ignore scale, so `HashSet<decimal>` / `Dictionary<decimal, _>` do the right thing. |
| Canonical text via a fixed format - `d.ToString("F2")`, or `Math.Round(d, 2)` first | You need one consistent string (invoice, API, UI). Fix the scale at the boundary; don't inherit whatever scale arrived. |
| Strip trailing zeros before you serialize/store | `1.50` and `1.5` must round-trip identically as text - normalize the scale with a small helper first. |
| Keep the scale on purpose | Trailing zeros are meaningful (a measured `1.50 mm` vs `1.5 mm`) - fine, just never *also* compare that value by text elsewhere. |

## 😈 The Even Worse Sibling

The scale does not sit still - arithmetic *changes* it, so the mismatch shows up with no
literal `1.50m` anywhere. `1.5m + 0.00m` is `1.50` (addition takes the larger scale);
`1.5m * 1.00m` is `1.500` (multiplication *adds* the scales); a `price * quantity` where
`quantity` came from a `decimal(18,2)` column is scaled differently from the same price times
an `int`. So the "same" total, computed down two code paths, serializes to two different
strings - and a change-detector that diffs serialized snapshots logs a phantom change every
run, or a hash-of-payload cache misses on a value it already holds. Nothing rounds, nothing
loses precision: the money is right and the text still disagrees.

## 🎓 Advanced Nuance

- **Equality normalizes; `GetHashCode` normalizes; formatting and `GetBits` do not.**
  `1.5m.Equals(1.50m)` is true and their hashes match (so collections key them correctly),
  but `decimal.GetBits` shows different scale bytes, and every text or serialization path
  reads that scale. The value and its representation genuinely diverge.
- **`double` / `float` have the opposite problem.** A binary float has no notion of "trailing
  zeros to keep" - `1.5` and `1.50` are the identical bit pattern. `decimal` is the *right*
  type for money ([0002-doubles-for-money](../../numbers/0002-doubles-for-money/)), and
  scale-preservation is part of why; it just surprises you at the text boundary.
- **Serializers preserve scale, and usually that is a feature.** `System.Text.Json` writes
  `1.50m` as `1.50`; a financial API often *wants* two-decimal money. The bug is not the
  serializer keeping scale - it is your code assuming two equal decimals serialize
  identically.

## 🔎 How to Find It in Your Codebase

- Grep for `decimal` values compared or keyed as text: `.ToString()` on a `decimal` feeding a
  `HashSet<string>` / `Dictionary<string, _>`, a `==` between formatted amounts, or a
  `GroupBy(x => x.Price.ToString())`.
- Look for change-detection, audit, or cache logic that diffs *serialized* snapshots
  containing decimals - a scale change reads as a value change.
- Symptom-side: reconciliation "discrepancies" where the numbers are equal, duplicate rows
  that differ only in trailing zeros, a UI showing inconsistent decimal places for one value.
- Fix at the boundary: choose a canonical scale for money (`ToString("F2")` / `Math.Round(x, 2)`)
  wherever you format or persist text, and compare by value everywhere else.
