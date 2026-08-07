# 🗄 orm

> Status: **opened**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### untranslatable-where (A4)

- **Twist:** Extract a predicate into a helper method - the refactor every
  reviewer approves - and the query that compiled and passed every unit test
  throws at runtime: EF cannot translate your method to SQL.
- **Mechanic:** EF Core builds SQL from expression trees; a call to your own
  method inside `Where` has no translation, and since EF Core 3 the query
  throws InvalidOperationException ("could not be translated") instead of
  silently downloading the table. The same predicate written inline
  translates fine - the difference is invisible in the code's meaning, only
  in its shape.
- **Who hits it:** everyone who refactors shared predicates ("IsActive(c)")
  out of queries. Compiles; green against in-memory lists; explodes on the
  first real database query.
- **Repro:** SQLite EF setup as #0008; `.Where(c => IsVip(c))` throws; the
  same expression inlined returns rows. Deterministic.
- **Damage:** honest crash, but at runtime in production, in a query the
  type system and the test suite both blessed. The exhibit's lesson is the
  displaced failure point.
- **😈 seed:** the pre-3.0 behavior was *silent* client evaluation - and it
  still exists today: insert `.AsEnumerable()` before the Where and the
  "fix" quietly downloads the entire table to filter it in memory.
- **Verified:** documented EF Core 3+ behavior; verify at build.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **savechanges-without-transaction** (A5) - two `SaveChanges` calls in one
  method are not one unit of work: the first commits, the second throws, and
  the database is left in the half-written state the code assumed impossible.
