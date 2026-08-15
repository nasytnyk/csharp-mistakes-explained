# ⚙️ configuration

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### bool-config-only-true-false (A5)

- **Twist:** `"Enabled": 1` (or `"yes"`, `"on"`) in appsettings throws
  `InvalidOperationException` - config booleans accept only `true`/`false`,
  and it crashes at the `GetValue`/bind call, not at startup.
- **Mechanic:** the binder converts a bool through `bool.Parse` (invariant),
  which accepts only `"true"`/`"false"` (case-insensitive); `"1"`, `"0"`,
  `"yes"`, `"on"` throw `FormatException`, surfaced as
  `InvalidOperationException` at the read site. A JSON number `1` stringifies
  to `"1"` - same crash. JSON's native `true` works, so a file mixing
  `"A": true` and `"B": 1` only blows up on B.
- **Who hits it:** anyone writing a feature flag as `1`/`0` or `yes`/`on`
  by analogy with other config systems (INI, env-var conventions, other langs).
- **Repro:** `GetValue<bool>` on `"1"` throws; on `"true"` returns true.
  Packages `Microsoft.Extensions.Configuration` + `.Binder`,
  `#:property PublishAot=false`. Deterministic.
- **Damage:** a startup or first-read crash on a value that "looks right",
  raised deep at the read call site rather than at config load - far from the
  file that caused it.
- **😈 seed:** the value scans as obviously true (`1`), and only the exact
  strings `true`/`false` work - so the fix ("write true") feels arbitrary
  until you know bool.Parse is the converter.
- **Verified:** ran on .NET 10 (2026-08-15): GetValue<bool>("1") threw
  InvalidOperationException; "true" returned True.

## Seeds

Not yet a full candidate - brainstorm before proposing.
