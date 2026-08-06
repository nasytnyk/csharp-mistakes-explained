# 🕳️ nullability

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### null-forgiving-lies (A5)

- **Twist:** `!` silences the compiler and changes nothing at runtime: a
  promise you made, not a check anyone performs - and the flow analysis now
  propagates your lie forward.
- **Mechanic:** the null-forgiving operator is erased at compilation; it
  emits no check. Worse, it *teaches* the nullable flow analysis that the
  value is non-null, so warnings downstream of the `!` disappear too - one
  suppression hides a family of them.
- **Who hits it:** nullable-migration codebases: `FirstOrDefault()!`,
  `Config["key"]!`, `default!` in constructors - each one a warning paid off
  with a promise.
- **Repro:** warning-free code with one `!` that NREs at runtime; sibling
  code without `!` that the compiler correctly flags. Deterministic, no
  packages.
- **Damage:** the annotation system reports the codebase clean while the
  NREs it exists to prevent ship anyway - false confidence at project scale.
- **Verified:** language-level erasure; verify at build.

### the-smuggled-null (A5,6)

- **Twist:** one JSON payload with `"CustomerId": null` writes null straight
  into a non-nullable, even initialized, property - annotations exist only
  at compile time, the deserializer never reads them, and the NRE fires far
  downstream.
- **Mechanic:** NRT is erased at runtime and System.Text.Json ignores it by
  default: an explicit JSON null overwrites a non-nullable property (even
  one with an initializer), and a missing member bound through a positional
  record constructor passes default = null silently. `required` guards
  *presence* only - a missing required member throws JsonException, but an
  explicit null still lands. The real fix is
  `JsonSerializerOptions.RespectNullableAnnotations` (.NET 9+), which turns
  the annotation into an actual runtime check.
- **Who hits it:** every API endpoint and message consumer - JS clients
  serialize undefined as null routinely, and version skew drops members;
  the DTO says non-null, the wire disagrees.
- **Repro:** deserialize `{"CustomerId": null}` into a class DTO and `{}`
  into a positional record; print both nulls, then dereference. BUILDER
  WARNING: do not null-probe the property before the final dereference -
  an `is null` probe teaches flow analysis the property can be null and
  CS8602 appears; keep Bad.cs's dereference clean so the build stays
  warning-free. `#:property PublishAot=false`.
- **Damage:** NRE hours later, far from the boundary - or worse, the
  "impossible" null is saved onward into the database and every consumer
  inherits the type system's forbidden state.
- **😈 seed:** `required` - the fix everyone reaches for - closes only the
  missing-member door: `{"CustomerId": null}` sails through `required` and
  lands anyway.
- **Verified:** ran on .NET 10 (2026-07-22): explicit null overwrote the
  initialized property; record ctor bound null for a missing member;
  required threw for missing but accepted explicit null; the dereference
  compiled warning-free and threw NRE; RespectNullableAnnotations made the
  same payload throw JsonException instead.

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
