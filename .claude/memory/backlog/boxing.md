# 🥊 boxing

> Status: **opened** (2026-08-05, by #0041 unbox-must-match-exact-type). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

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
