# 🧩 pattern-matching

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### not-binds-before-or (A4,5)

- **Twist:** `is not Status.Active or Status.Pending` reads like "neither of
  the two" - but `not` grabs only the first value, so the guard quietly
  approves and rejects the wrong statuses, with zero compiler warnings.
- **Mechanic:** in pattern combinators `not` binds tighter than `or`, so
  `x is not A or B` parses as `x is ((not A) or B)` - true for everything
  except A, and the `or B` branch is effectively dead weight. The intended
  meaning needs parentheses: `x is not (A or B)`. The compiler emits no
  warning for the misgrouped form (CS8794 "always matches" does not fire -
  the pattern is not vacuous, just wrong). The English reading and the parse
  disagree on exactly one value: B.
- **Who hits it:** anyone writing a multi-value guard the way they would say
  it aloud - `if (order.Status is not Status.Active or Status.Pending)
  reject();` in validation gates, state machines, early returns. Pattern
  combinators are new enough that teams write them by ear.
- **Repro:** enum `Status { Active, Pending, Cancelled }`; print the guard
  for all three members next to the parenthesized intended form. The
  misparse rejects Pending (or admits it, depending on gate direction) -
  a one-value divergence that is easy to stage as a paying customer turned
  away. Deterministic, no packages.
- **Damage:** a silently wrong gate: valid Pending orders bounced (or
  invalid states waved through) while every test that only probes A and a
  "clearly bad" value passes - the divergent value is exactly the second
  one listed, the one the author felt safest about.
- **😈 seed:** the mirror trap `is not A and not B` vs `is not A or not B`:
  the `or` version is *always true* for any two distinct constants - a guard
  that never guards - and that one the compiler also accepts silently.
- **Verified:** ran on .NET 10 (2026-07-22): `is not Active or Pending` gave
  true for Pending and Cancelled, false for Active; parenthesized form gave
  the intended neither-semantics; no warnings in build output.

### boxed-five-is-not-five (A4,5)

- **Twist:** `5L == 5` is true - but box that long into an `object` and
  `is 5` is false: a constant pattern type-tests before it compares, so the
  dispatcher's switch slides past every numeric case into default.
- **Mechanic:** a constant pattern against a value of static type `object`
  first checks the runtime type matches the constant's type (int for `5`),
  then compares - no numeric conversions, unlike `==`, which converts both
  operands at compile time. A boxed `long`, `double`, `byte` or `decimal` 5
  matches neither `is 5` nor `case 5:`. `o.Equals(5)` is false too (long's
  Equals rejects int). The value is right; the box's type is wrong; nothing
  throws.
- **Who hits it:** anyone switching over an `object` that came from a
  deserializer or data reader: Newtonsoft materializes every JSON integer
  as `long` inside `object`, SQLite's ADO.NET provider returns INTEGER
  columns as `long`, Excel/interop hands numbers over as `double`. The
  author typed `5`; the runtime delivered `5L`.
- **Repro:** deserialize `{"code": 5}` with Newtonsoft into
  `Dictionary<string, object>`, switch on the value with `case 5:` arms -
  falls to default while `(long)code == 5` prints true. Needs
  `#:package Newtonsoft.Json@13.0.3` and `#:property PublishAot=false`
  (reflection-based JSON, precedent #0012); the packageless core
  (`object o = 5L; o is 5` false) also reproduces in three lines.
- **Damage:** silent misroute - the status-code dispatcher handles nothing,
  every message takes the default branch, and logs show the "right" value
  the whole time because `ToString()` prints 5 either way.
- **😈 seed:** it round-trips through reviews forever: the fix someone
  ships - `case 5L:` - breaks again the day the data source changes to
  System.Text.Json's JsonElement or an int-typed column, because the guard
  is still welded to one box type instead of unboxing first.
- **Verified:** ran on .NET 10 (2026-07-22): `(object)5L is 5` false,
  `(object)5.0 is 5` false, `(object)(byte)5 is 5` false, `o.Equals(5)`
  false, switch fell to default; Newtonsoft run confirmed `{"code": 5}`
  arrives as System.Int64 and misses `case 5`.

### the-hijacked-null-check (A4)

- **Twist:** `if (order == null)` does not check for null - it calls
  whatever `operator ==` the class defined, which can answer true for a
  live object, or throw NullReferenceException *from the null check
  itself*. `is null` is the spelling the class cannot hijack.
- **Mechanic:** `==` against `null` dispatches to a user-defined operator
  when one exists; only `is null` / `is not null` are guaranteed
  reference-vs-null tests (constant pattern, no operator lookup). Two
  realistic operator bugs: (a) the ?.-style body `a?.Id == b?.Id` makes any
  object with an unassigned Id compare equal to null; (b) the unguarded
  body `a.Key == b.Key` makes `e == null` throw NRE inside the operator.
  Both compile clean when Equals/GetHashCode are overridden alongside.
- **Who hits it:** codebases with equality-overloading value objects and
  entities (Money, EntityId, DDD aggregates) - the overload is written for
  value semantics, then every plain `== null` guard in the codebase quietly
  routes through it. EF/Unity developers know the genre; console-honest
  version needs no framework.
- **Repro:** class Order with `Guid? Id` and operator == comparing
  `a?.Id == b?.Id`: `new Order() == null` prints true while
  `new Order() is null` prints false - an unsaved order "is" null. Second
  act: the unguarded operator variant throws NRE on `e == null`.
  Deterministic, no packages.
- **Damage:** the cache-miss branch fires for an object that exists -
  re-fetch, duplicate insert, "not found" for a record the user is looking
  at; in the NRE variant, the defensive guard is the crash site, which
  reads as impossible in the stack trace.
- **NOTE on hall placement:** equality hall has `equals-but-not-equal`
  (Equals overridden, == forgotten - the two regimes drift apart). This is
  the complementary failure - == *was* overridden and now lies about null -
  and it lives here because the broken belief is "`is null` is just syntax
  sugar for `== null`". Flagged so the curator can move it if he reads the
  center of gravity differently.
- **😈 seed:** `is null` fixes the guard you rewrote - but every
  `Assert.AreEqual(null, order)`, LINQ `FirstOrDefault() == null`, and
  third-party helper still calls the operator, so the codebase disagrees
  with itself about which objects exist.
- **Verified:** ran on .NET 10 (2026-07-22): `?.`-body operator gave
  `unsaved == null` true / `is null` false; unguarded-body operator threw
  NRE from `e == null`.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **Dead end (verified 2026-07-22, do not re-derive):** "switch expression
  re-reads a property per arm, so a total-looking switch can throw" -
  false. Roslyn's decision DAG shares one read of the same property path
  across arms: a counting getter was called exactly once across three
  property-pattern arms. Only explicit `when` guards re-read.

- **earlier-pattern-shadows-later** (A5) - a broad `when` guard or base-type
  case placed first swallows everything, leaving the specific arm below it
  unreachable - no error, the specialized branch just never runs.

- **type-pattern-skips-null** (A5) - `case string s:` does not match null
  (null is not an instance of anything), so a null slips past the branch
  that "handles strings" into the default arm meant for other types.
