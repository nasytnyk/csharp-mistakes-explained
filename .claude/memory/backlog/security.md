# 🔒 security

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### aes-ecb-reveals-patterns (A5)

- **Twist:** "It's AES-256, it's encrypted." In ECB mode identical plaintext
  blocks produce identical ciphertext blocks - so the ciphertext is a
  coloring-book tracing of the plaintext's structure, and strong encryption
  changed nothing.
- **Mechanic:** ECB (Electronic Codebook) encrypts each 16-byte block
  independently with no IV and no chaining, so equal input blocks map to equal
  output blocks - patterns, repeats and structure survive encryption. The
  famous "ECB penguin" image is this exact property. It is a deterministic,
  data-only fact: no timing, no keys to guess. `Aes.Create()` defaults to CBC,
  but `aes.Mode = CipherMode.ECB` and the one-shot `EncryptEcb` are right there
  and read as harmless mode selection; the fix is authenticated encryption
  (AES-GCM) or at least CBC with a random per-message IV.
- **Who hits it:** anyone hand-rolling `System.Security.Cryptography` to
  "encrypt a column / a token / a cookie" who picks ECB because it needs no IV
  and "just works" - encrypted card numbers, tokens, PII at rest.
- **Repro:** encrypt 32 bytes made of two identical 16-byte halves with
  `EncryptEcb(..., PaddingMode.None)`; the two ciphertext halves are byte-for-
  byte equal. Then `EncryptCbc` with a random IV on the same input: the halves
  differ. Built-in crypto, no packages, deterministic.
- **Damage:** equal plaintext is visibly equal in the ciphertext, so an
  attacker reads structure and repetition (which records share a value, where a
  known block sits) and can cut-and-paste blocks - all without ever recovering
  the key. Compliance calls this a breach; the code calls it "encrypted".
- **😈 seed:** the reviewer sees `Aes`, `Key`, 256 bits and a green test that
  round-trips correctly - correctness tests never look at *two* ciphertexts,
  so the one property that is broken is the one nothing checks.
- **Verified:** ran on .NET 10 (2026-07-22): ECB ciphertext halves equal, CBC
  halves differ, same key and plaintext.

### path-combine-discards-root (A4)

- **Twist:** `Path.Combine(safeRoot, userFileName)` looks like it pins every
  file under safeRoot - but hand it an absolute path and Combine throws the
  root away and returns the attacker's path verbatim: path traversal from a
  method whose name promises the opposite.
- **Mechanic:** documented Path.Combine rule - if a later argument is an
  absolute (rooted) path, all earlier arguments are discarded:
  `Path.Combine("/var/app/uploads", "/etc/passwd") == "/etc/passwd"`. The
  relative-but-traversing cousin (`"../../etc/passwd"`) is not discarded but
  escapes once `GetFullPath` normalizes the `..`. Both defeat the "combine with
  a safe root" mental model; the real containment check is
  `Path.GetFullPath(combined).StartsWith(GetFullPath(root) + separator)`.
- **Who hits it:** file download/serve/upload endpoints that take a filename or
  path parameter and `Path.Combine` it with a storage root before opening the
  file - a universal shape in web apps and their background workers.
- **Repro:** pure string/path math, no filesystem needed:
  `Path.Combine(root, "/etc/passwd")` returns `/etc/passwd`;
  `Path.GetFullPath(Path.Combine(root, "../../etc/passwd"))` resolves outside
  root; a plain `invoice.pdf` stays inside. Deterministic, no packages.
- **Damage:** arbitrary file read (or write, on upload) outside the intended
  directory - `/etc/passwd`, connection strings, another tenant's documents -
  through an endpoint that "obviously" sandboxes to one folder.
- **REVISIT NOTE for the curator:** `rejected.md` has `path-combine-betrayal`,
  declined as a vacuum example - "you can't say who attacks, from where, or
  how." This security framing answers exactly that: the attacker, via a
  filename/path parameter on a file-serving endpoint, by supplying a rooted or
  `..`-traversing value. Flagged openly rather than re-proposed silently; the
  curator judges whether the who/where/how objection is now met.
- **😈 seed:** the naive fix - reject inputs containing `..` - still loses to a
  plain absolute path (`/etc/passwd` has no `..`), and vice versa: two attack
  shapes, and each of the two obvious guards blocks only one.
- **Verified:** ran on .NET 10 (2026-07-22): absolute arg discarded root,
  traversal escaped after normalization, plain name stayed contained.

### host-allowlist-bypass (A4)

- **Twist:** The URL starts with `https://expected.com`, so the allowlist
  passes it - but the request goes to `evil.com`, because everything before the
  `@` in a URL is a username, not the host. Same string, two readers, opposite
  destinations.
- **Mechanic:** in `https://expected.com@evil.com/path`, `expected.com` is the
  userinfo and `evil.com` is the authority - `new Uri(...).Host` returns
  `evil.com`, exactly as the HTTP stack will connect. A hand-rolled check that
  does `url.StartsWith("https://expected.com")` or `url.Contains("expected.com")`
  reads the userinfo and is fooled. The correct check parses with `Uri` and
  compares `Host` by exact (ordinal) equality, never substring.
- **Who hits it:** SSRF/open-redirect guards - "only allow callbacks/webhooks/
  redirects to our domain" - implemented as string matching on the raw URL
  instead of parsing it. A staple of the OWASP SSRF and open-redirect classes.
- **Repro:** `"https://expected.com@evil.com/withdraw".StartsWith("https://expected.com")`
  is true, while `new Uri(that).Host` is `"evil.com"`. Deterministic, pure Uri
  parsing, no network, no packages.
- **Damage:** SSRF (the server makes a trusted-looking request to an
  attacker-controlled host - internal metadata endpoints, credential theft) or
  an open redirect used for phishing - all past a check that "verified the
  domain".
- **😈 seed:** the parser and the string-matcher disagreeing is the whole bug
  family: backslashes, extra `@`, and case tricks each split the URL
  differently for `Uri` than for a human reading `StartsWith` - the allowlist
  and the HTTP client never see the same host.
- **Verified:** ran on .NET 10 (2026-07-22): StartsWith allowlist passed while
  Uri.Host resolved to evil.com.

### the-culture-that-bypassed-auth (A4,6)

- **Twist:** `"admin".ToUpper() == "ADMIN"` is not always true. Under a Turkish
  locale, `"admin".ToUpper()` is `"ADMİN"` (dotted capital I), so the
  reserved-name / blocklist check silently fails to match - the security gate
  is off, but only on machines with the "wrong" culture.
- **Mechanic:** `ToUpper`/`ToLower`/culture-sensitive `String.Equals` use
  `CultureInfo.CurrentCulture`. In `tr-TR`, `i` uppercases to `İ` (U+0130) and
  `I` lowercases to `ı` (U+0131) - the Turkish dotted/dotless-I rule. A
  security comparison that case-folds through the current culture gives a
  different answer per locale. `ToUpperInvariant` / `StringComparison.Ordinal`
  are the locale-independent forms and the fix.
- **Who hits it:** case-insensitive security decisions written with the default
  overloads - reserved-username blocklists, extension/scheme allowlists, role
  and header comparisons - deployed to servers whose OS culture isn't the
  developer's.
- **Repro:** pin the culture in code so the demo is deterministic (the allowed
  "code fixes the environment" pattern): `CultureInfo.CurrentCulture =
  new CultureInfo("tr-TR")`; then `"admin".ToUpper() != "ADMIN"` while
  `"admin".ToUpperInvariant() == "ADMIN"`. Show a reserved-name blocklist that
  rejects the invariant form and waves through the culture form. No packages.
- **Damage:** an attacker registers a reserved name (or slips a blocked
  extension/role past the filter) on any tr-TR deployment - a security control
  that passes every test in the developer's locale and is simply absent in
  another.
- **DISTINCTION from a rejected item:** `rejected.md` has `turkish-i-login`,
  declined as primer-level - but that was about *typing a password in the wrong
  keyboard layout*. This is a different mechanic: culture-sensitive case
  folding in a security comparison (the classic "Turkish-I" string bug), not a
  typing mistake. Flagged so the curator can judge them as separate things.
- **😈 seed:** it never reproduces for the developer - it depends on the
  server's OS locale, so it passes CI, passes review, ships, and fails only in
  the one region whose culture exposes it.
- **Verified:** ran on .NET 10 (2026-07-22): under tr-TR, `"admin".ToUpper()`
  did not equal `"ADMIN"` while `ToUpperInvariant` did.

## Seeds

Not yet a full candidate - brainstorm before proposing.

- **unsalted-hash-reveals-duplicates** (A5) - bare SHA-256 with no salt makes
  identical passwords produce identical hashes: the leaked table shows which
  accounts share a password - the hash hid the value but not the collisions.
