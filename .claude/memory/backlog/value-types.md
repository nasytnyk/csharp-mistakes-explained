# 📦 value-types

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-vanishing-mutation (A3)

- **Twist:** Mutating a struct taken from a List edits a temporary copy; the
  identical line against an array works fine - so the collection is the last
  thing anyone suspects.
- **Mechanic:** `list[i]` calls the indexer, which *returns a copy* of the
  struct; `arr[i]` is direct storage access. BUILDER WARNING: the assignment
  form `list[i].X = 5` does not even compile (CS1612) - the compiler blocks
  the obvious spelling. The trap that ships is the method form:
  `list[i].Translate(5)` compiles without a whisper and mutates the copy. So
  Bad.cs must use a mutating *method* (or a `var tmp = list[i]; tmp.X = 5;`
  sequence), not direct member assignment.
- **Who hits it:** structs in Lists - points, money amounts, game entities.
  The array version worked yesterday; today someone changed `T[]` to
  `List<T>` in one place and every mutation became a no-op.
- **Repro:** same mutating method called on `arr[i]` (works) and `list[i]`
  (silently does nothing); print both. Deterministic, no packages.
- **Damage:** updates that no-op silently - balances never change, positions
  never move - while the identical code elsewhere (arrays) works, actively
  pointing the investigation away from the cause.
- **😈 seed:** `foreach` over a List of structs hands out copies too - the
  "fix everything in a loop" pass fixes nothing.
- **Verified:** CS1612 vs method-call nuance is language-specified; verify at
  build (the CS1612 note is load-bearing for Bad.cs).

### the-skipped-initializer (A4)

- **Twist:** Struct field initializers run for `new S()` but not for `default`
  or array elements - the same struct is born with different values depending
  on who created it.
- **Mechanic:** field initializers on a struct execute only as part of a
  constructor call. `new S()` invokes the parameterless constructor, so
  initializers run; `default(S)` and `new S[n]` just zero memory - no
  constructor, no initializers. BUILDER WARNING: a struct with field
  initializers and no declared constructor does not compile (CS8983), so the
  demo struct must declare `public S() { }`.
- **Who hits it:** structs given "sensible defaults" via initializers
  (`Rate = 1.0m`, `Enabled = true`) then materialized through arrays, `out`
  parameters, or `default` - every such instance carries zeros and falses
  where the author promised 1.0 and true.
- **Repro:** `struct WithInit { public decimal Rate = 1.5m; public WithInit() {} }`;
  print `new WithInit().Rate` (1.5), `default(WithInit).Rate` (0), and
  `new WithInit[1][0].Rate` (0). Three lines, deterministic, no packages.
- **Damage:** a multiplier that "defaults to 1" is 0 in every array-born
  instance: totals multiply to zero - silent money-math corruption.
- **😈 seed:** `Enabled = true` flips to false the same way - a permission or
  feature silently defaults OFF only on the code path that used an array.
- **Verified:** ran on .NET 10 (2026-07-22): 1.5 / 0 / 0 exactly as above.

### the-copy-returning-getter (A3)

- **Twist:** `player.Position.MoveBy(10)` compiles, runs, and moves nothing -
  a property getter returns a *copy* of the struct, so the method mutates a
  temporary that is discarded; the identical struct exposed as a *field*
  moves for real.
- **Mechanic:** a property getter that returns a struct returns a fresh
  copy; a mutating method called on that copy changes only the temporary.
  The assignment form `player.Position.X = 5` at least fails to compile
  (CS1612 "cannot modify the return value"); the method form compiles
  silently. A public struct *field* is direct storage, so the same call
  mutates in place - property vs field, same syntax, opposite outcome.
- **Who hits it:** structs behind auto-properties on classes - positions,
  sizes, money on a DTO or game entity: `transform.Position.Offset(...)`,
  `order.Total.Add(...)`. The "why won't my struct move" classic.
- **Repro:** a class with `Position { get; set; }` (a struct) and another
  with a struct field; call the same mutating method through each -
  property no-ops (X=0), field works (X=10). Deterministic, no packages.
- **Damage:** silent no-op updates through a property while the field
  version of the same code works - the mutation lands nowhere and nothing
  warns, so the property is the last suspect.
- **😈 seed:** the "fix" of adding a setter changes nothing - the getter
  still hands out a copy; the only real fixes are making the struct
  immutable (return a new one and assign it back) or not using a mutable
  struct at all.
- **Verified:** ran on .NET 10 (2026-07-24): property getter no-op (X=0),
  struct field mutated in place (X=10).

### default-struct-has-null-fields (A4,5)

- **Twist:** `default(Cart)` and `new Cart[1][0]` skip the constructor, so a
  struct that "always initializes its list" hands you a null one - and the
  first `.Items.Add(...)` throws NullReferenceException from an object that
  looks fully constructed.
- **Mechanic:** `default(T)` and array allocation zero the memory and run no
  constructor and no field initializers, so every reference field
  (`List<T>`, `string`, a nested class) is null. The struct's own type says
  nothing is nullable; the value simply never ran the code that would fill
  them. This is the-skipped-initializer's sibling one rung *up* the fear
  ladder: value fields come back 0/false (silently wrong), reference fields
  come back null (a crash).
- **Who hits it:** structs holding a collection or string, materialized
  through `default`, `new T[n]`, an uninitialized field, or an `out`
  parameter - then handed to code that trusts the constructor ran.
- **Repro:** `struct Cart { public List<string> Items; public Cart() {
  Items = new(); } }`; `new Cart()` works, `default(Cart).Items` is null and
  `.Add` throws NRE, `new Cart[1][0].Items` is null too. Deterministic, no
  packages.
- **Damage:** NRE from a value the type system swears is constructed -
  arising far from the `default`/array that made it, so the crash site and
  the cause sit in different components.
- **😈 seed:** cross-link the-skipped-initializer: same "default skips
  construction" root, opposite fear rung - the value-field version is
  silently wrong (a 0 multiplier), the reference-field version at least
  crashes loudly; the quiet one is the dangerous one.
- **Verified:** ran on .NET 10 (2026-07-24): default(Cart) and
  new Cart[1][0] had null Items and null Name; `.Items.Add` threw NRE.
