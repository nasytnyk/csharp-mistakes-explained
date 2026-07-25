# ⚖️ equality

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### equals-but-not-equal (A4)

- **Twist:** The same two objects are equal inside a HashSet and unequal in an
  if-statement: Equals was overridden, == was not, and both spellings look
  interchangeable.
- **Mechanic:** overriding Equals/GetHashCode does not touch `operator ==`;
  for classes it remains reference comparison. Collections and LINQ
  (Contains, Distinct, HashSet, Dictionary) call Equals; hand-written ifs use
  `==`. One value-object type, two equality regimes, selected by syntax - and
  no compiler warning exists for the mismatch.
- **Who hits it:** value objects written as classes with Equals overridden -
  Money, Address, DateRange - and then `if (total == expected)` in a check
  somewhere. Records exist precisely to kill this bug, which makes Good.cs a
  one-word rewrite.
- **Repro:** Money class overriding Equals/GetHashCode: `a.Equals(b)` true,
  `a == b` false, `new HashSet<Money> { a }.Contains(b)` true - three
  contradictory-looking lines in a row. Deterministic, no packages.
- **Damage:** a payment-matching branch that silently never matches while
  every collection lookup around it works - behavior differs between "is it
  in the set" and "is it the same", on identical data.
- **😈 seed:** the trap inverts for strings: `==` on string is value equality
  - so developers *trained on strings* expect `==` to follow Equals
  everywhere else.
- **Verified:** ran on .NET 10 (2026-07-22): Equals true, == false, HashSet
  Contains true.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **equality:** default struct Equals over float/double fields diverges from
  `==` - but NOT bitwise as long assumed. Verified 2026-07-24:
  ValueType.Equals delegates to each field's `double.Equals`, so
  `Vec(0.0).Equals(Vec(-0.0))` is **true** and `Vec(NaN).Equals(Vec(NaN))`
  is **true**, while `==` says the opposite on NaN (false) and agrees on +0
  (true). So the real divergence is only on NaN (Equals true, == false) -
  which is shipped #0029 (nan-poisons-comparison) reached through a struct
  wrapper. Do not propose the "bitwise" framing; if revisited, it is a
  struct-flavored cross-link to #0029, not a new mechanic.

- **equals-without-gethashcode** (A2) - override Equals but not GetHashCode
  and the object works in `List.Contains` yet goes missing in a HashSet or
  dictionary: two "equal" instances land in different buckets.

- **nan-equals-disagrees-with-operator** (A4,2) - `NaN == NaN` is false but
  `NaN.Equals(NaN)` is true, so `List.Contains(NaN)` finds it while
  `Any(x => x == NaN)` never will. Check overlap with shipped #0029
  (nan-poisons-comparison) before promoting.
