# 🕳️ nullability

> Status: **opened** (2026-08-05, by #0046 null-forgiving-lies). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

> Chosen candidate queue exhausted 2026-08-05: 3 shipped (#0046 null-forgiving-lies,
> #0047 the-smuggled-null, #0048 the-stale-narrowing), 1 rejected (the-oblivious-boundary;
> see `rejected.md`). Two Seeds remain below.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **default-of-t-is-null** (A5,6) - a generic `T Get<T>()` returning
  `default` hands back null for every reference T despite the non-nullable
  annotation: the "never null" contract is a compile-time fiction.

- **nullability:** `new string[10]` and `default(StructWithStringField)`
  both manufacture nulls of a non-nullable type with zero warnings
  (verified 2026-07-22) - primer-adjacent as a standalone; more likely a 😈
  inside the-smuggled-null or value-types' the-skipped-initializer.
