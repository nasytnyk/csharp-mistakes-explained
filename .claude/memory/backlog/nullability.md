# 🕳️ nullability

> Status: **opened** (2026-08-05, by #0046 null-forgiving-lies). Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-stale-narrowing (A1,5)

- **Twist:** `if (_user != null)` narrows the field, the helper call on the
  next line sets it back to null, and the compiler keeps vouching for the
  dereference after it - the null check was a snapshot, the analysis treats
  it as a promise.
- **Mechanic:** nullable flow analysis does not model side effects of
  method calls: narrowing on a field survives any number of intervening
  calls (a deliberate, documented soundness trade-off), and only a direct
  assignment visible in the same body resets it. So annotated, `!`-free,
  warning-free code throws NRE.
- **Who hits it:** stateful classes: a guard at the top of a handler, then
  cleanup/reset/audit helpers mid-body that null the field - plus the
  async and event variants where someone else's continuation does it
  between the check and the use.
- **Repro:** `string? _user`; Handle() checks `!= null`, calls
  FinishAudit() which nulls the field, then dereferences - no warning,
  NRE. Deterministic, no packages.
- **Damage:** NRE on a line the compiler explicitly approved, in a class
  the team believes proven null-safe - so the investigation starts by
  distrusting everything except the actual cause.
- **😈 seed:** swap the helper for an await and the mutation no longer
  needs to be in your code at all - any continuation or second thread can
  null the field mid-method; the analysis was never designed to see it.
- **Verified:** ran on .NET 10 (2026-07-22): no warning on the post-call
  dereference; NRE at runtime.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **default-of-t-is-null** (A5,6) - a generic `T Get<T>()` returning
  `default` hands back null for every reference T despite the non-nullable
  annotation: the "never null" contract is a compile-time fiction.

- **nullability:** `new string[10]` and `default(StructWithStringField)`
  both manufacture nulls of a non-nullable type with zero warnings
  (verified 2026-07-22) - primer-adjacent as a standalone; more likely a 😈
  inside the-smuggled-null or value-types' the-skipped-initializer.
