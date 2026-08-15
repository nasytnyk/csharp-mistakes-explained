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

### getoradd-runs-twice (A5,6)

- **Twist:** ConcurrentDictionary is thread-safe, your factory is not: two
  threads enter GetOrAdd together, the "runs exactly once" factory runs twice,
  and one result is silently discarded.
- **Mechanic:** `GetOrAdd(key, valueFactory)` invokes the factory *outside*
  the internal lock (documented). Two threads asking for the same missing key
  can both run the factory; only one produced value is stored, the loser is
  thrown away - but the loser's *side effects* are not undone.
- **Who hits it:** caches of expensive resources: connections, sessions,
  "create the customer row on first order". The factory opens a socket or
  INSERTs a row; under concurrency it does so twice.
- **Repro:** the factory increments a counter and blocks on a `Barrier(2)`, so
  the demo *proves* both threads are inside the factory simultaneously, then
  returns; assert factory ran 2 times while the dictionary holds 1 value.
  Deterministic - the barrier replaces any timing assumption. No packages.
- **Damage:** duplicate side effects (two rows, two charges, two connections)
  under a green log; the dictionary itself looks perfectly consistent
  afterwards, so nothing points at the cache.
- **😈 seed:** the standard fix is caching `Lazy<T>` - which walks straight
  into `the-cached-failure` (async hall). The two exhibits cross-link.
- **Verified:** ran on .NET 10 (2026-07-22): barrier repro, factory ran 2x,
  one value stored.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **removeat-in-forward-loop** (A5) - RemoveAt inside a forward `for` shifts
  every later index down one, so the loop skips the element that slid into
  the freed slot - and unlike foreach it never throws.

- **getvalueordefault-hides-missing** (A4,5) - `dict.GetValueOrDefault(sku)`
  returns `default(decimal)` for an absent key - a real 0.00 and "not
  priced" are the same value, so the order ships free with nothing thrown.
