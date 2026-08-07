# 📄 serialization

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### tuples-serialize-to-nothing (A5)

- **Twist:** System.Text.Json serializes properties; tuples are all fields -
  so your (id, total) goes over the wire as {} and comes back as zeros, with
  no error in either direction.
- **Mechanic:** STJ ignores public fields unless `IncludeFields = true`.
  `ValueTuple`'s Item1/Item2 are fields, so a tuple serializes to `{}`. The
  friendly names (`(int id, decimal total)`) are compiler fiction that never
  exists at runtime, so even with IncludeFields you get Item1/Item2, never
  your names. Deserializing `{}` into a struct yields all defaults - #0012's
  tolerant reading completes the silent round trip.
- **Who hits it:** quick internal APIs, cache layers, queue messages where
  someone returns a tuple "for now"; plus older DTOs using public fields
  instead of properties - same rule, same empty object.
- **Repro:** `JsonSerializer.Serialize((1042, 149.99m)) == "{}"` - one line.
  Needs `#:property PublishAot=false`. Deterministic.
- **Damage:** order id 0, amount 0.00, HTTP 200 everywhere; data loss with
  every status green.
- **😈 seed:** `IncludeFields = true` "fixes" it into
  `{"Item1":1042,"Item2":149.99}` - the data survives but the contract is
  still garbage, and every consumer now binds to Item1/Item2 forever.
- **Verified:** ran on .NET 10 (2026-07-22): Serialize((1, "a")) == "{}".

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **json-cycle-throws** (A5) - a parent referencing children that reference
  the parent serializes fine right up until JsonException at runtime: the
  default serializer has no cycle handling.

- **json-case-sensitive-by-default** (A4,5) - System.Text.Json matches
  property names case-sensitively (Newtonsoft did not); one `"userId"` vs
  `"UserId"` and the field stays default with nothing logged - a migration
  that "changed only the library" drops data.
