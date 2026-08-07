# 🧵 strings

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### mojibake-factory (A4)

- **Twist:** Decode bytes with the wrong encoding, save the result, and the
  text is gone for good: "Привіт" becomes "ÐŸÑ€Ð¸Ð²Ñ–Ñ‚" - readable proof
  of what one wrong round-trip does.
- **Mechanic:** UTF-8 bytes read as Latin-1/1252 turn each multi-byte
  character into two garbage-but-valid characters; the mistake is invisible
  to the type system (it's all "string") and *reversible* until the first
  save re-encodes the garbage as genuine UTF-8 - then the original bytes no
  longer exist.
- **Who hits it:** any boundary with a charset assumption: files, HTTP
  bodies, DB connections - plus the "fix" where someone saves the mangled
  text back "corrected".
- **Repro:** pin both encodings in code (UTF-8 bytes of a Ukrainian string,
  decoded as `Encoding.Latin1`): print the mojibake; round-trip once more to
  show the point of no return. Deterministic, no packages, no CodePages
  needed with Latin1.
- **Damage:** permanent corruption of every non-ASCII name in the batch -
  and the demo doubles as the museum's most shareable screenshot.
- **BUILDER NOTE:** adjacent to readalltext-guesses-encoding (silent U+FFFD
  substitution vs. reversible-then-baked mojibake). Both can live in the
  hall, but cross-link and keep the mechanics distinct; if only one is
  wanted, the curator picks.
- **Verified:** encoding math; verify at build.

### trim-is-a-charset-not-a-prefix (A4,5)

- **Twist:** `url.TrimStart("https://".ToCharArray())` does not strip the
  prefix - it eats every leading character from the *set* {h,t,p,s,:,/}:
  "track" becomes "rack", "svc" becomes "vc" - while "example.com"
  survives intact, so the developer's own test passed.
- **Mechanic:** Trim/TrimStart/TrimEnd take a char array as an unordered
  set and keep trimming while the next character is in it. Any host whose
  first letter is h, t, p or s gets eaten past the prefix, letter by
  letter, until a non-member appears. There is no TrimPrefix in the BCL;
  the correct spelling is StartsWith + a range slice.
- **Who hits it:** prefix stripping over URLs, paths, and keys - "remove
  the scheme", "drop the env prefix" - written as the one-liner that
  reads exactly like the intent and reviews as obviously right.
- **Repro:** four hosts through both spellings: track->rack, svc->vc,
  portal->ortal, example.com->untouched; the StartsWith version leaves
  all four correct. Deterministic, no packages.
- **Damage:** corrupted hostnames and keys stored or requested - and only
  for values whose first letter collides with the "prefix" set, so the
  failures look data-dependent and random instead of systematic.
- **😈 seed:** example.com passing IS the trap's own certification: the
  canonical placeholder domain starts with 'e', outside the set, so the
  demo, the unit test, and the docs sample all confirm the broken line.
- **Verified:** ran on .NET 10 (2026-07-24): rack / vc / ortal /
  example.com untouched; StartsWith spelling correct on all four.

### unnormalized-strings-not-equal (A2,4)

- **Twist:** two "café" print identically and `==` says they differ
  (Lengths 4 and 5) - and the app splits down the middle: HashSet and the
  database index see two values, culture-aware Equals sees one.
- **Mechanic:** Unicode encodes é two ways - composed U+00E9 and "e" plus
  combining U+0301. Ordinal machinery (==, default HashSet, unique
  indexes) compares code units: different. Linguistic comparison
  (InvariantCulture Equals) matches them: equal. `string.Normalize()`
  brings ordinal into agreement. The two forms genuinely co-arrive in one
  system: macOS emits decomposed (NFD), Windows keyboards composed.
- **Who hits it:** uniqueness and lookup over human text - usernames,
  tags, coupon codes, file names crossing macOS/Windows - two visually
  identical entries both get in, or a lookup misses a row that "is right
  there".
- **Repro:** composed and decomposed literals: print both, Lengths 4/5,
  == false, Ordinal false vs InvariantCulture true, HashSet counts 2 vs 1
  by comparer, Normalize() == true. Deterministic, no packages.
- **Damage:** duplicate accounts and coupons that support cannot even see
  as duplicates - both rows render identically in every UI - or the
  mirror: "name already taken" by a name no search can find.
- **😈 seed:** the investigation runs on ordinal tools - SQL WHERE, grep,
  Ctrl+F all miss the twin - so the duplicate is invisible precisely to
  the person hunting it.
- **Verified:** ran on .NET 10 (2026-07-24): == false, InvariantCulture
  Equals true, HashSet 2 vs 1 by comparer, Normalize made them equal.

### removeemptyentries-shifts-columns (A5)

- **Twist:** the empty CSV field is legal - discount just isn't set - and
  the cleanup option everyone reflexively adds, RemoveEmptyEntries,
  deletes it: every later column shifts left and the price becomes the
  quantity.
- **Mechanic:** default Split preserves positions - empty entries are
  kept, trailing ones included - so positional indexing is safe.
  StringSplitOptions.RemoveEmptyEntries collapses the array;
  TrimEntries | RemoveEmptyEntries additionally nukes whitespace-only
  fields. After either, items[i] reads a neighboring column. NOTE: the
  original PR-#2 seed framed the *default* as the bug ("empty kept, so
  columns shift") - verification showed the opposite: the default is
  positionally safe, the option is the corruption. Reframed accordingly.
- **Who hits it:** positional parsing of delimited exports where blank
  means "not set" - the option usually arrives later, in a
  "handle blank lines" or tidy-up commit that reviews as harmless.
- **Repro:** "Widget,,4,19.99" as name,discount,qty,price: default gives
  4 items with qty=4 and price=19.99 in place; RemoveEmptyEntries gives 3
  items with 19.99 sitting in the qty slot. Deterministic, no packages.
- **Damage:** per-row transposition: complete rows parse perfectly, only
  rows with a blank field shift - numbers land in wrong columns and
  "parse successfully", defeating spot checks and samples alike.
- **😈 seed:** test fixtures are written from happy, fully-filled example
  rows - the one shape the bug cannot touch - so coverage certifies the
  parser on exactly the inputs that dodge it.
- **Verified:** ran on .NET 10 (2026-07-24): default kept 4 positions;
  RemoveEmptyEntries shifted price into the qty slot; trailing empties
  kept by default.

### interpolation-is-culture-sensitive (A4)

- **Twist:** the same `$"{price}"` writes "1234.56" on the build server
  and "1234,56" in Frankfurt - interpolation formats with CurrentCulture,
  so the CSV or SQL your code assembles is malformed in exactly one
  region.
- **Mechanic:** `$"..."` lowers to formatting with CurrentCulture:
  decimal separators and date order flip per machine (en-US
  "1234.56;7/24/2026" vs de-DE/uk-UA "1234,56;24.07.2026"). The invariant
  spellings - `FormattableString.Invariant($"...")` and
  `string.Create(CultureInfo.InvariantCulture, $"...")` - both fix it.
  Cultures are pinned in code, per the CI-honesty rule.
- **Who hits it:** anyone assembling machine-readable text by
  interpolation - CSV exports, SQL strings, query strings, config -
  developed under en-US or invariant CI, deployed onto servers with real
  locales.
- **Repro:** pin en-US, de-DE, uk-UA in turn; print one CSV line and one
  SQL WHERE: comma decimals and flipped dates appear under de/uk; then
  both invariant spellings print dots. Deterministic *because* pinned.
- **Damage:** exports whose numbers change meaning by deployment region -
  downstream parsers reject the line or, where comma is a grouping
  separator, quietly read a different number; the SQL comparison stops
  matching anything.
- **😈 seed:** CI is blind by construction: build containers typically
  run the invariant culture, so every test passes there and the bug
  exists only on the machines nobody tests on - the inverse of
  works-on-my-machine.
- **Verified:** ran on .NET 10 (2026-07-24): de-DE and uk-UA produced
  comma decimals and flipped dates; both invariant spellings produced
  dots.

### contains-and-indexof-disagree (A4,5)

- **Twist:** string.Contains is ordinal by default, string.IndexOf(string)
  is CurrentCulture - one method family, two defaults - so the
  hidden-character guard written with IndexOf "finds" a zero-width joiner
  at position 0 of the clean word "admin" and blocks every input in the
  system.
- **Mechanic:** culture-sensitive comparison treats ignorable characters
  (ZWJ, soft hyphen) as matching the empty string, so IndexOf(zwj)
  returns 0 on *any* string while Contains(zwj) - ordinal - says false
  and the Ordinal IndexOf overload says -1. StartsWith shares the culture
  default: a joiner-prefixed "admin" StartsWith("admin") is true
  culturally, false ordinally, `==` false, InvariantCulture Equals true -
  five spellings, three answers. Requires ICU globalization (the default
  since .NET 5); keep `InvariantGlobalization` off or every comparison
  collapses to ordinal and the demo - like production behavior - changes.
- **Who hits it:** sanitizers hunting invisible characters (the
  hidden-char defenses themselves!), prefix routing on StartsWith, and
  any logic mixing Contains and IndexOf as if interchangeable - which is
  how they read in review.
- **Repro:** ZWJ probe: Contains false / IndexOf 0 / Ordinal -1; the
  guard blocking a clean "admin"; joiner-prefixed name passing culture
  StartsWith while failing ordinal and ==. Deterministic under ICU, no
  packages.
- **Damage:** both failure directions at once: the sanitizer that blocks
  everyone (loud), and the allowlist/prefix check that a joiner-carrying
  impostor passes while `==` would have caught it (silent) - identity
  confusion in exactly the code written to prevent it.
- **😈 seed:** the inconsistency is permanent by design - IndexOf is
  ancient (culture), Contains arrived later (ordinal), and compat locks
  both defaults forever; the CA1310 analyzer that would flag it ships
  disabled.
- **Verified:** ran on .NET 10 (2026-07-24, Linux ICU): Contains false /
  IndexOf 0 / Ordinal -1; the guard blocked clean input; StartsWith split
  true/false; == false while InvariantCulture Equals true.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **encoding-getbytes-drops-chars** - Encoding.ASCII.GetBytes("café")
  silently yields "caf?" - the encode-direction sibling of
  mojibake-factory (decode direction) and readalltext-guesses-encoding
  (io hall). Verify the exact fallback behavior and find a framing
  distinct from both before promoting.
