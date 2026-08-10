# 🗂 collections

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### dictionary-order-illusion (A6)

- **Twist:** Enumeration order looks like insertion order until one Remove;
  the next Add reuses the freed slot and the new key surfaces in the middle of
  the sequence.
- **Mechanic:** `Dictionary<K,V>` stores entries in an internal array and
  enumerates it in storage order. With no removals, storage order happens to
  equal insertion order, which trains the illusion that order is guaranteed.
  `Remove` puts the slot on a free list; the next `Add` fills the freed slot,
  so the newest entry enumerates where the deleted one used to be.
- **Who hits it:** anyone printing or exporting a dictionary and trusting the
  visible order - CSV exports, config dumps, dropdowns built from a
  Dictionary. Every test passes (tests rarely delete), production breaks
  after the first delete.
- **Repro:** build a small dictionary, print keys; Remove one entry, Add a new
  one, print again - the new key appears mid-sequence. No packages,
  deterministic.
- **Damage:** ordered output (menus, exports, hash-over-serialized payloads)
  silently reorders after the first delete in the data's lifetime.
- **😈 seed:** the layout is an implementation detail - a runtime upgrade may
  legally change observed order with zero code changes.
- **Verified:** documented internal layout, widely reproduced; verify at build.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **removeat-in-forward-loop** (A5) - RemoveAt inside a forward `for` shifts
  every later index down one, so the loop skips the element that slid into
  the freed slot - and unlike foreach it never throws.

- **getvalueordefault-hides-missing** (A4,5) - `dict.GetValueOrDefault(sku)`
  returns `default(decimal)` for an absent key - a real 0.00 and "not
  priced" are the same value, so the order ships free with nothing thrown.
