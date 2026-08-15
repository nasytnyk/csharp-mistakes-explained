---
id: "0074"
title: ToDictionary throws on a duplicate key
category: collections
tags: [collections, LINQ, Dictionary]
rule: "never `ToDictionary` by a key that can repeat - a duplicate **throws**; use `GroupBy` / `ToLookup`"
---

# #0074 - ToDictionary Throws on a Duplicate Key

## 💥 Symptom

A service that starts fine in dev crashes at boot in production with
`ArgumentException: An item with the same key has already been added.` The code just built a lookup
- `things.ToDictionary(t => t.Id)` - from data that, this time, had two rows sharing an id. No id
was invalid; two were simply equal, and `ToDictionary` refuses to continue.

## 🔍 The Offending Code

```csharp
var priceBySku = feed.ToDictionary(p => p.Sku, p => p.Price); // 💥 throws if any SKU repeats
```

## 🧠 What's Actually Going On

`ToDictionary` builds a `Dictionary`, whose keys are unique - so the first time the key selector
produces a key it has already seen, `ToDictionary` throws `ArgumentException` rather than pick a
winner. `Dictionary.Add` and the `{ [k] = v }` collection initializer behave the same: they assert
uniqueness and fail loudly on a collision. There is no "last wins" or "first wins" overload of
`ToDictionary`; uniqueness is a precondition, and violating it is an exception, not a merge.

The broken belief is "this key is unique, so I can index by it." It is unique in the sample data
and the fixtures; production supplies the row that isn't - a duplicate SKU from a feed correction,
two users sharing an email, a foreign id that turned out one-to-many. `ToDictionary` did not compute
a wrong answer; it declared your uniqueness assumption false, at the worst possible moment.

## ✅ The Fix

Decide what a duplicate *means* and pick the operation that expresses it - group to keep them all,
or reduce to one on purpose:

```csharp
// last row wins per key:
var priceBySku = feed.GroupBy(p => p.Sku).ToDictionary(g => g.Key, g => g.Last().Price);

// keep every row per key (a one-to-many lookup):
ILookup<string, decimal> pricesBySku = feed.ToLookup(p => p.Sku, p => p.Price);
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `GroupBy(key).ToDictionary(g => g.Key, g => g.Last())` | Duplicates are real and one should win - newest, highest, whatever `Last` / `Max` / `OrderBy` says. Make the tiebreak explicit. |
| `ToLookup(key)` | Many values per key is the point (orders per customer, prices per SKU) - `ILookup` is a dictionary of groups, and a missing key returns an empty sequence, not an exception. |
| `dict[key] = value` in a loop | A hand-rolled last-wins build - the indexer overwrites silently where `Add` / `ToDictionary` throw. Choose it deliberately when overwrite is the intent. |
| Keep `ToDictionary` and let it throw | The key really must be unique and a duplicate is a data bug you want surfaced - then the exception is correct (it even names the colliding key). |

## 😈 The Even Worse Sibling

The throw is the honest half of the family; the *silent* members are worse. The `dict[key] = value`
indexer, reached for as the "fix," never throws - it overwrites, so a duplicate quietly discards
every earlier row and you keep only the last, with no sign that data was dropped. And a `Distinct()`
sprinkled on to "dedupe before ToDictionary" removes rows by *whole-value* equality, not by key - so
two rows with the same SKU but different prices both survive and `ToDictionary` still throws, while
two identical rows silently collapse to one, hiding a real duplicate you needed to see. Same
duplicate key, three outcomes: `ToDictionary` crashes, the indexer keeps one without a word,
`Distinct` changes which rows exist - and only the crash tells you the truth.

## 🎓 Advanced Nuance

- **`Add` throws, the indexer overwrites - by design.** `dict.Add(k, v)` asserts the key is new;
  `dict[k] = v` upserts. `ToDictionary` and the `{ [k]=v }` initializer both use `Add` semantics,
  which is why they throw on a collision.
- **The key comparer decides what "duplicate" means.** `ToDictionary(k, comparer)` uses the given
  `IEqualityComparer`; keys that are *ordinally* distinct can collide under a case-insensitive
  comparer (`"SKU"` and `"sku"`), turning a "unique" key into a duplicate - or the reverse. Pass the
  comparer that matches your definition of *same*.
- **`ToLookup` is eager and null-key-tolerant.** Unlike `GroupBy` (deferred), `ToLookup` runs
  immediately and *allows* a null key (as its own group), where `ToDictionary` throws on a null key.
  If keys can be null, that difference matters.

## 🔎 How to Find It in Your Codebase

- Grep for `.ToDictionary(` and `Dictionary.Add(` and ask whether the key can *ever* repeat in real
  data - a feed, an import, a join, user-entered values - not only in the fixtures.
- Prefer `ToLookup` when many-per-key is legitimate, and `GroupBy(...).ToDictionary(...)` with an
  explicit tiebreak when one should win; reserve raw `ToDictionary` for keys that must be unique and
  where a duplicate is a bug worth crashing on.
- Symptom-side: `ArgumentException: An item with the same key has already been added` at startup or
  first request; a lookup that "loses" rows (silent overwrite via the indexer); a `Distinct()` that
  didn't stop the throw.
- If a duplicate is a data error, surface it clearly - the exception already names the colliding key
  (`Key: A-1`); catch it at the build site and add which feed or import it came from.
