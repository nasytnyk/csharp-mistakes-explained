# 🏷️ enums

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### the-overlapping-flags (A5)

- **Twist:** [Flags] values numbered 1, 2, 3: the third flag IS the first two
  OR-ed together, so granting Delete silently grants Read and Write - and
  every HasFlag check happily agrees.
- **Mechanic:** flags combine by bitwise OR, so members must be powers of
  two. Sequential numbering makes 3 == 1|2: setting "flag 3" sets both lower
  bits; checking it answers true whenever both others are present. Nothing
  in the language or runtime objects.
- **Who hits it:** whoever adds the third member to a two-member [Flags] enum
  by continuing the sequence 1, 2, 3 - the single most natural wrong move in
  the API.
- **Repro:** `[Flags] enum Perm { Read = 1, Write = 2, Delete = 3 }`;
  `(Read | Write).HasFlag(Delete)` is true - a user granted read+write can
  delete. Deterministic, no packages.
- **Damage:** permission escalation - security stakes, screenshots well.
- **😈 seed:** the enum prints correctly (`Delete`), logs look right, and
  audits confirm the user "had the Delete flag" - the corruption extends
  into the investigation.
- **Verified:** ran on .NET 10 (2026-07-22): (Read|Write).HasFlag(Delete)
  == true.

### enum-default-is-zero (A5)

- **Twist:** nobody assigned anything and the order is already "Active":
  default(TEnum) is whichever member equals 0 - and if *no* member equals
  0, the runtime manufactures a value that is not any member, without a
  single cast in sight.
- **Mechanic:** enums are integers with names and default is always 0.
  Flavor (a): the first declared member sits at 0 by accident of
  ordering, so every uninitialized field, array slot, and missing JSON
  property arrives as a real business state. Flavor (b): with
  `enum Priority { Low = 1, High = 2 }`, default(Priority) prints "0",
  Enum.IsDefined says false, and a switch falls to the fallback arm - a
  non-member value born from `default`, not from any parse or cast.
- **Who hits it:** every DTO, struct field, array, and deserialized
  payload holding an enum whose author didn't reserve 0 for
  Unknown/None - the platform's own design guideline exists precisely
  because of this.
- **Repro:** OrderStatus { Active, Cancelled, Shipped }: default(),
  new OrderStatus[2], new Dto().Status, and Deserialize&lt;Dto&gt;("{}")
  all print Active. Then Priority with no zero member: prints 0,
  IsDefined false, switch hits the fallback. JSON half needs
  `#:property PublishAot=false`.
- **Damage:** unset reads as Active - orders nobody activated get
  processed, and the audit shows a valid state that no code ever
  assigned; flavor (b) instead feeds your switches a value they never
  planned for.
- **NOTE for the curator:** adjacent to rejected `enum-accepts-undefined`,
  but the objection there was "nobody pushes a number into an enum" -
  here nobody pushes anything: default, arrays, and missing JSON
  manufacture the value from inside. Flagged for the judgment call.
- **😈 seed:** the fix has its own trap: renumbering an existing enum to
  insert Unknown = 0 shifts every persisted value by one -
  the-renumbered-status (serialization) is one well-intentioned fix away
  from this exhibit.
- **Verified:** ran on .NET 10 (2026-07-24): all four flavor-(a) paths
  returned Active; flavor (b) printed 0, IsDefined false, switch fell
  through.

### isdefined-rejects-legal-flags (A4,5)

- **Twist:** the validation gate `Enum.IsDefined(request.Permissions)`
  rejects Read | Admin - a perfectly legal combination - while accepting
  Read | Write, because someone happened to *name* that combo: validity
  by whether a constant was declared, not by what the type allows.
- **Mechanic:** IsDefined checks membership among declared constants, not
  representability: a combined flags value is "undefined" unless the
  exact combination is itself a named member (ReadWrite = 3 makes 3
  defined; 5 stays undefined). Meanwhile ToString prints "Read, Admin"
  fluently - the formatting layer understands flags, the validation
  layer doesn't.
- **Who hits it:** guard clauses and model validation on [Flags]
  properties - "reject values not in the enum" is a standard defensive
  line, and on a flags enum it rejects most of the feature it guards.
  Teams then either delete the check or keep shipping 400s for legal
  requests.
- **Repro:** [Flags] Perm { Read=1, Write=2, Admin=4, ReadWrite=3 }:
  IsDefined true for Read and Read|Write, false for Read|Admin and
  Write|Admin; a validation loop 400-rejects the legal request while
  ToString prints it as "Read, Admin". Deterministic, no packages.
- **Damage:** 400s that depend on which checkboxes a user combined - the
  permissions matrix works for the combos QA tested (the named ones) and
  rejects the rest; deleting the check instead reopens the gate to
  actual junk.
- **COORDINATION:** three different lies about one feature, three
  exhibits: bad numbering (the-overlapping-flags, this hall), pattern
  equality (the-banned-user-walked-in, pattern-matching), and this -
  validation vs combination. Cross-link, don't merge.
- **😈 seed:** naming every meaningful combo "fixes" it onto a
  treadmill: each new flag doubles the combinations, and the first
  forgotten name is a fresh 400 that lands as a regression ticket.
- **Verified:** ran on .NET 10 (2026-07-24): named combo accepted,
  unnamed legal combos rejected, ToString printed the rejected combo as
  "Read, Admin".

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **the-alias-that-never-prints** - `enum Status { Active = 1,
  Enabled = 1 }`: Enabled.ToString() prints "Active" (verified
  2026-07-24) - an alias member exists for source compatibility but
  never appears in logs, string-serialized JSON, or GetValues-built
  dropdowns (GetValues returns the duplicate). Which name wins is
  documented as unspecified. Needs a damage framing (audit logs,
  external API contracts?) before promoting.
