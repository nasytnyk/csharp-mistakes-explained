# 📇 records

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### record-equality-skin-deep (A4,5)

- **Twist:** Record value equality is one property deep: add a List and two
  identical-looking records stop being equal, Distinct stops deduplicating,
  and dictionary lookups quietly miss.
- **Mechanic:** record equality compares each member with
  `EqualityComparer<T>.Default`; for List/array/Dictionary members that is
  *reference* equality. GetHashCode composes the same way, so hashes differ
  too and everything stays self-consistent - wrong, but never inconsistent
  enough to throw.
- **Who hits it:** records as DTOs and value objects with a `List<string>
  Tags` or `Items` array: test assertions, Distinct, records as cache keys.
- **Repro:** `record Order(int Id, List<string> Tags)`; two same-content
  instances: `!=` is true, `Distinct()` keeps both, a Dictionary keyed by
  one misses the other, hashes differ. Deterministic, no packages.
- **Damage:** cache keyed by records: 100% miss rate (a cost bug that looks
  like a traffic increase); test assertions that fail on equal data - or
  pass on unequal data once someone "fixes" the test with reference reuse.
- **😈 seed:** cross-link #0028: `with` copies the *reference*, so the two
  records that ARE equal share one list - mutate one, both change.
  Equal-when-they-shouldn't-be and unequal-when-they-should-be, from the
  same design gap.
- **Verified:** ran on .NET 10 (2026-07-22): records unequal, hashes differ.

### record-struct-is-mutable (A3,4)

- **Twist:** `record struct Point(int X, int Y)` is fully mutable -
  `p.X = 99` compiles and changes it - so the word that means "immutable
  value" for a record *class* means the opposite for a record *struct*,
  and the struct copy-semantics pile a second surprise on top.
- **Mechanic:** positional record *structs* generate `get; set;`
  properties (mutable); record *classes* and `readonly record struct`
  generate `get; init;`. So "records are immutable" silently flips with
  one keyword. And because it is a struct, it copies on assignment and its
  List/array indexer returns a copy - editing `list[0]` either fails to
  compile or (via a temp) is lost, the value-types trap wearing a record's
  clothes.
- **Who hits it:** anyone reaching for `record struct` for a small value
  (Point, Money, Range) expecting record immutability - shared instances
  get mutated in place, or the mutation vanishes into a copy, depending on
  where it lives.
- **Repro:** `record struct Point(int X, int Y)`; `p.X = 99` mutates;
  `readonly record struct` version won't compile the same line; a
  `List<record struct>` shows the indexer-copy loss. Deterministic, no
  packages.
- **Damage:** a "value object" mutated under a caller who assumed the
  record contract, or an update silently dropped because it hit a copy -
  the same code with a record class behaves the opposite way.
- **ADJACENCY:** the copy-loss face overlaps value-types
  the-vanishing-mutation / #0011; the *new* belief here is "record ==
  immutable" failing for structs. Cross-link; lead with the mutability
  flip.
- **😈 seed:** `readonly` is the one-word fix and the compiler never
  demands it - `readonly record struct` is a deliberate choice most
  people don't know to make, so the mutable form is the default you get
  by typing the obvious thing.
- **Verified:** ran on .NET 10 (2026-07-24): `record struct` property set
  mutated the value; readonly form immutable; List indexer returned a
  copy (edit lost).

### with-skips-constructor-validation (A5)

- **Twist:** the invariant lives in the record's constructor, every
  `new` is validated - and `order with { Total = -50 }` sails right past
  it: `with` copies fields and never calls your constructor, so the
  "impossible" negative state exists one expression later. Move the same
  check into an `init` accessor and `with` suddenly enforces it.
- **Mechanic:** `with` invokes the compiler-generated copy constructor
  (a member-by-member field copy) and then applies the changed members
  through their `init` setters. Validation in the *primary/explicit
  constructor body* is skipped entirely; validation in an *init accessor*
  runs on every `with`. Verified both placements: ctor-body validation
  produced Balance = -50 with no throw, init-setter validation threw. The
  fix is to put invariants in init accessors (or a validating factory
  with non-public setters).
- **Who hits it:** records as validated value objects / DTOs where the
  author put the guard in the natural place - the constructor - then
  derives edited copies with `with`, trusting the type to stay valid.
- **Repro:** two equivalent records, validation in the ctor body vs in the
  init setter; `x with { Balance = -50 }` produces an invalid record for
  the first and throws for the second. Deterministic, no packages.
- **Damage:** invariant-violating records flowing downstream - a negative
  balance, an empty required field, a bad enum - created by the one
  operation (`with`) the immutability story sold as safe, past the guard
  the author believed was total.
- **😈 seed:** it hides behind the happy path: direct construction is
  validated, so unit tests that `new` the record all pass; only the
  edit-a-copy path in production reaches the unguarded door.
- **Verified:** ran on .NET 10 (2026-07-24): ctor-body validation bypassed
  by `with` (Balance = -50, no throw); init-accessor validation enforced
  on `with`.

### record-equality-folds-in-type (A4,5)

- **Twist:** two records with byte-identical data are *not* equal because
  their types differ - a base record and a derived one, same fields,
  compare false through a base-typed variable, so a `HashSet<Base>` misses
  the derived twin and Distinct keeps both.
- **Mechanic:** record equality compares the `EqualityContract` (the
  runtime Type) before any member, and GetHashCode folds it in too. So
  `Base("x",1)` and `Derived("x",1)` are unequal via `==`, `Equals` (both
  directions), and hash - self-consistent, never throwing, just wrong for
  anyone who thinks record equality is data equality. Same-type records
  with equal data still compare equal, which is what hides it.
- **Who hits it:** record type hierarchies used as DTOs or keys, and any
  place a subclass instance (a specialized variant, a proxy-like wrapper)
  is compared against a base record with the same payload - dedup,
  cache-key, and test-assertion code over `List<Base>`.
- **Repro:** `record Base(string Key,int N)` /
  `record Derived(...):Base(...)`: base-typed `b == d` false, HashSet
  Contains false, Distinct().Count() == 2; two same-type Deriveds still
  equal. Deterministic, no packages.
- **Damage:** silent duplicates and cache misses across a record
  hierarchy - the deduper keeps both the base and derived views of one
  logical value, and a lookup by the base type never finds the entry
  stored as a subtype.
- **ADJACENCY:** complements record-equality-skin-deep above - that one is
  "equal data, unequal because of a *collection member*"; this is "equal
  data, unequal because of the *type*". Same disappointed belief (records
  give value equality) from two directions; cross-link, keep distinct.
- **😈 seed:** the type check is invisible in the debugger - every field
  shows identical, EqualityContract has no value to inspect - so the
  investigation stares at two records that are equal in every way it can
  see and unequal in the one way it can't.
- **Verified:** ran on .NET 10 (2026-07-24): base-typed == false, Equals
  false both ways, hashes differ, HashSet.Contains false, Distinct kept
  2, same-type equal.
