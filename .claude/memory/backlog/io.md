# 📁 io

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### readalltext-guesses-encoding (A5)

- **Twist:** Read a legacy-encoded file with File.ReadAllText and the text
  comes back mangled instead of failing - the decoder quietly substitutes
  replacement characters and the corruption ships downstream.
- **Mechanic:** ReadAllText defaults to UTF-8; bytes that aren't valid UTF-8
  decode to U+FFFD without any exception (the strict-throwing UTF8Encoding
  variant exists and is not the default). The damage happens at ingestion
  and is baked in by the first save.
- **Who hits it:** ingesting exports from legacy systems - bank CSVs,
  ERP dumps in Windows-125x encodings - on the .NET side that assumes UTF-8.
- **Repro:** BUILDER TIP: avoid the CodePages package - use
  `Encoding.Latin1` (built-in) to write "café" as Latin-1 bytes, read with
  ReadAllText: "caf�". Both encodings pinned in code, fully deterministic,
  no packages.
- **Damage:** customer names and addresses permanently corrupted in the
  database; the earliest anyone notices is a customer complaint, long after
  the original files are gone.
- **😈 seed:** strict decoding (`new UTF8Encoding(false, true)`) would have
  crashed honestly at ingestion - silence is the default, correctness is
  opt-in.
- **Verified:** documented decoder defaults; verify at build with the Latin1
  approach.

### utf8-bom-breaks-parser (A4,5)

- **Twist:** File.WriteAllText writes clean UTF-8 - until someone adds
  Encoding.UTF8 "to be explicit": that argument prepends a three-byte
  BOM, the CSV's first column becomes "﻿id" - and even
  StartsWith("id") swears the file is fine.
- **Mechanic:** the parameterless write overloads use BOM-less UTF-8; the
  static Encoding.UTF8 instance is configured to emit a preamble, so the
  *more explicit* call changes the bytes (13 -> 16, EF BB BF first).
  Read-side asymmetry: File.ReadAllText detects and strips the BOM,
  Encoding.UTF8.GetString(ReadAllBytes) keeps it. Poison cherry: culture-
  sensitive StartsWith("id") returns TRUE on the BOM'd string (U+FEFF is
  culturally ignorable) while the ordinal header == "id" lookup fails -
  the probe people write to hunt the bug reports everything clean
  (cross-link: strings hall, contains-and-indexof-disagree).
- **Who hits it:** exporters "hardened" by making the encoding explicit,
  and consumers doing byte-level or GetString reads - header-keyed
  CSV/JSON importers, checksums and signatures computed over "the same"
  text.
- **Repro:** write with and without the encoding argument; hex-dump the
  first bytes; show the two readers disagreeing; the header lookup
  failing while StartsWith lies. Deterministic, no packages.
- **Damage:** the header column that "doesn't exist": importers keyed by
  first-column name drop or misroute every row of a file that renders
  identically in every editor; producer and consumer disagree on the
  hash of the same text.
- **😈 seed:** the diff that planted it reads as pure hardening - adding
  ", Encoding.UTF8" looks like rigor - and the fix,
  `new UTF8Encoding(false)`, looks identical in review to the trap; the
  deciding parameter is invisible at the call site.
- **Verified:** ran on .NET 10 (2026-07-24): 13 vs 16 bytes, EFBBBF
  prefix; ReadAllText stripped, GetString kept; culture StartsWith true
  while the ordinal header compare failed.

### exists-swallows-every-error (A5)

- **Twist:** File.Exists said false, so the code took the first-run path
  and re-initialized the config that was sitting right there: Exists
  never throws - every failure to *look* (access denied, bad path,
  file-as-directory) is reported as "not there".
- **Mechanic:** the contract is "true only if the caller can confirm the
  file exists"; all internal exceptions become false. Verified faces: a
  path under an unreadable (chmod 000) parent - false, no exception; a
  file used as a directory segment - false; null, empty, and
  NUL-containing paths - false. So false means
  "missing OR unreachable OR nonsense", and callers read it as plain
  "missing".
- **Who hits it:** first-run and ensure-created logic
  (`if (!File.Exists(p)) initialize()`), pre-overwrite guards, cleanup
  jobs deleting "orphans" - anywhere a transient permission or mount
  hiccup gets converted into a confident business decision.
- **Repro:** real file true; file-as-directory-segment, null, empty, NUL
  paths all false with no throw; then the permission act:
  File.SetUnixFileMode the parent to 000 - the intact file reads as
  missing and the first-run guard fires; restore the mode and the file
  is back with its contents. BUILDER NOTE: the permission act is
  Unix-only (CA1416) - lead with the cross-platform faces, gate the
  chmod act, and restore the mode before cleanup. Deterministic, no
  packages.
- **Damage:** data overwritten or state re-initialized because a
  directory was briefly unreadable - and the post-incident check finds
  the file present and healthy, so the report says "cannot reproduce".
- **😈 seed:** the defensive rewrite is built from the same lying
  primitives - Directory.Exists swallows identically - so "check the
  directory first, then the file" just asks two liars instead of one.
- **Verified:** ran on .NET 10 (2026-07-24): all five false-faces
  confirmed; under a 000 parent the existing file read as missing
  without an exception and the guard took the re-initialize path.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **io:** relative paths resolve against the current working directory, not
  the exe location - real (services, schedulers), but needs a framing that
  clears the primer floor.

- **io:** `File.Exists` then `File.Open` TOCTOU, and `Directory.GetFiles`
  order not being guaranteed - both race- or environment-shaped; promote
  only with a hard assertion.
