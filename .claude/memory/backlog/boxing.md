# 🥊 boxing

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### unbox-must-match-exact-type (A4,5)

- **Twist:** `42L == 42` is true and every implicit conversion in the
  language agrees an int fits in a long - yet `(int)(object)42L` throws
  InvalidCastException: unboxing demands the exact type. And the one place
  the rule bends - enums - trains you to expect leniency exactly where
  there is none.
- **Mechanic:** unboxing checks the box's runtime type against the target;
  no numeric conversions apply. `(int)` from a long box, `(decimal)` from
  a double box, `(int)` from a byte box, `(uint)` from an int box - all
  throw. The two-step `(int)(long)o` works: unbox exactly, then convert.
  The documented exception: enum boxes unbox to their underlying type and
  vice versa - `(int)(object)DayOfWeek.Monday` and `(DayOfWeek)(object)1`
  both succeed.
- **Who hits it:** object-typed data at borders - ADO.NET readers (SQLite
  hands INTEGER columns over as long), Excel/COM interop (every number is
  a double), DataTable cells, deserialized payloads.
  `(int)reader["Count"]` works against one provider and throws against
  the next.
- **Repro:** one boxed 42L: `(int)` throws, `(int)(long)` works; boxed 1.1
  vs `(decimal)` throws; boxed (byte)5 vs `(int)` throws; both enum
  directions succeed; `(uint)(object)42` throws. Deterministic, no
  packages.
- **Damage:** a crash keyed to the data *source*, not the code: the same
  line works in dev against SQL Server's int and dies in prod against
  SQLite's long - provider migrations and test-vs-prod database
  differences detonate a cast that reviewed as obviously safe.
- **😈 seed:** pattern-matching's boxed-five-is-not-five is this same box
  with the opposite failure mode - `is 5` misses *silently* where the
  cast throws loudly. "Modernizing" the cast into a pattern match trades
  the crash for wrong routing, one rung down the fear ladder. Cross-hall
  pair - keep the two exhibits coordinated.
- **Verified:** ran on .NET 10 (2026-07-24): long/double/byte/uint casts
  all threw, two-step worked, both enum directions worked.

### boxed-values-are-equal-not-same (A2,4)

- **Twist:** box the same 5 twice: Equals says equal, `==` says
  different - object-typed `==` is reference comparison and every boxing
  mints a fresh heap object, so change detection over object-typed
  storage reports "changed" for identical values, forever.
- **Mechanic:** between object operands `==` compiles to reference
  equality - no operator lookup, no value semantics. And .NET interns no
  boxes: bools, zero, enum values, even the same variable boxed twice all
  produce distinct objects (verified - unlike Java's small-integer
  cache). So the same pair of values answers differently to Equals and
  `==` depending on nothing but static types.
- **Who hits it:** object-typed storage layers - settings bags, view
  state, cache entries - and every `if (field != value)` change guard
  written against object-typed fields: each assignment of an equal value
  registers as a change, so events cascade, caches invalidate, and dirty
  flags never clear.
- **Repro:** two boxes of 5: Equals true, `==` false, ReferenceEquals
  false; cache probes for bool/0/enum/same-variable all false; the
  change-detection guard firing on an identical value. BUILDER NOTE:
  analyzer CA2013 flags ReferenceEquals with value-type arguments - box
  into object locals first so the build stays warning-free.
  Deterministic, no packages.
- **Damage:** permanently "dirty" state: update pipelines and re-renders
  run on every touch with identical data, and the log's
  "value changed: 5 -> 5" line is the whole bug report.
- **😈 seed:** it heals and relapses with storage-type refactors: type
  the field int and `==` becomes value comparison, type it object again
  and the bug returns - a diff that "doesn't touch logic" toggles it, and
  string's value-comparing `==` has trained everyone to expect the safe
  behavior everywhere.
- **Verified:** ran on .NET 10 (2026-07-24): Equals true / == false /
  ReferenceEquals false; no box caching for bool, zero, enum, or the same
  variable boxed twice; the != guard fired for an identical value.

### nullable-boxes-to-nothing (A4,5)

- **Twist:** int? is a value type - and yet `(object)(int?)null == null`
  is true: boxing evaporates the Nullable wrapper, an empty one becomes a
  plain null *reference*, and reading it back throws
  NullReferenceException - from a cast.
- **Mechanic:** Nullable&lt;T&gt; never exists inside a box: empty boxes
  to a null reference, filled boxes to a plain T (GetType() reports
  Int32). That is why `empty.HasValue` works while `empty.GetType()`
  throws NRE - GetType is non-virtual on object, so the call boxes first,
  producing null, then dereferences it. The reverse door is open: a plain
  int box unboxes into int? without complaint.
- **Who hits it:** any object-typed container receiving nullables -
  settings and state dictionaries, logging scopes, DbParameter values,
  object[] rows. The int? you stored arrives as a bare null on the other
  side, and `(int)bag["retries"]` explodes even though "we stored a value
  type".
- **Repro:** `object o = (int?)null` compares == null true; a filled int?
  boxes as Int32; HasValue works while GetType() throws; a
  `Dictionary<string, object?>` stores the "value", hands back null, and
  the `(int)` cast throws NRE. BUILDER NOTE: naive spellings draw
  CS8600/CS8629 hints - shape Bad.cs around object?-typed storage so it
  compiles warning-free. Deterministic, no packages.
- **Damage:** NullReferenceException from a line with no visible
  dereference, hours after the null was stored by code that believed
  structs can't be null - store site and crash site live in different
  components.
- **😈 seed:** the roundtrip is asymmetric, and the asymmetry aims at
  your tests: store-filled-read-back passes (`(int?)(object)7` works,
  verified), only the *empty* case dies - and empty is exactly the case
  test data never contains.
- **Verified:** ran on .NET 10 (2026-07-24): empty boxed to a null
  reference, filled boxed as Int32; HasValue worked while GetType() threw
  NRE; the settings-bag (int) cast threw NRE; a plain box unboxed into
  int? cleanly.

### boxed-enum-isnt-its-number (A4,5)

- **Twist:** `(int)(object)Color.Red` unboxes fine to 0 - but
  `((object)Color.Red).Equals(0)` is false, and a `Dictionary<object>` keyed
  by the enum is not found by a boxed `0`: the cast treats an enum as its
  number, `Equals` and hashing do not.
- **Mechanic:** a boxed enum carries its enum type; `Equals` between two
  boxes requires the same runtime type, so a boxed enum and a boxed
  underlying int are never equal (both directions), and they hash into
  different dictionary buckets. Unboxing is the one operation that bridges
  enum and underlying (see unbox-must-match-exact-type) - so the same pair
  is interchangeable by cast and disjoint by Equals.
- **Who hits it:** `object`-typed storage that mixes enums and their numbers
  from different sources - a config/JSON layer that reads a status as `int`
  while code stores it as the enum, both dropped into a
  `Dictionary<object,handler>` or compared with `.Equals`.
- **Repro:** `(int)(object)Color.Red` is 0; `((object)Color.Red).Equals(0)`
  and `((object)0).Equals(Color.Red)` both false; a dict keyed by the enum
  is found by the enum, missed by boxed `0`. Deterministic, no packages.
- **Damage:** a handler lookup or equality check that silently misses across
  the enum/int boundary - the enum path works in tests, the int-from-JSON
  path in production falls through to the default, on data that "is the same
  value".
- **😈 seed:** the cast working is the trap's cover: a reviewer checks
  `(int)key == 0`, sees it pass, and concludes the boxed forms are
  interchangeable - but every `Equals`/dictionary/`Contains` in the code
  disagrees with the cast they tested.
- **Verified:** ran on .NET 10 (2026-07-24): cast to int gave 0, both
  Equals directions false, dict missed by boxed int and hit by the enum.
