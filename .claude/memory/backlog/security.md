# 🔒 security

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

### secrets-in-the-query-string (A5)

- **Twist:** an API key in the URL is "just passing a parameter" - until it
  turns up in the server's access log, the proxy log, the browser history, and
  the `Referer` header sent to third-party sites, in plaintext, even over HTTPS.
- **Mechanic:** TLS encrypts the URL in transit, but the receiving server logs
  the full request line (method + path + query) by default, as do proxies and
  CDNs; the browser keeps the URL in history and sends it as `Referer` to any
  resource the page loads. A secret in the query is a secret written to a dozen
  places. Carry secrets in a header (`Authorization`) or the body, never the URL.
- **Who hits it:** hand-rolled API clients and quick integrations that append
  `?api_key=...` / `?token=...`; signed download links with the token in the query.
- **Repro:** build a request URL with `?api_key=SECRET`; the URL string (what the
  access log records) contains the secret; the header form does not. Deterministic,
  no packages.
- **Damage:** the key leaks to everyone who can read a log or browser history -
  ops, a compromised proxy, an analytics script that receives the `Referer` - and
  stays there long after the key is rotated.
- **😈 seed:** HTTPS lulls you ("it's encrypted") - but encryption protects the
  wire, not the log the server writes the instant it decrypts.
- **Verified:** ran on .NET 10 (2026-08-13): the query-string URL contains the
  secret; a header carries it out of the URL.

### session-cookie-without-httponly-secure (A5)

- **Twist:** a session cookie set with default options is readable by any script
  on the page and sent over plain HTTP - `HttpOnly` and `Secure` both default to
  false, so the auth token is one XSS away from theft and rides out in cleartext.
- **Mechanic:** `System.Net.Cookie` (and ASP.NET Core `CookieOptions`) default
  `HttpOnly=false` and `Secure=false`; without HttpOnly the cookie is visible to
  `document.cookie` (any XSS exfiltrates the session), and without Secure it is
  sent on `http://` requests (a network attacker reads it). `SameSite` unset
  weakens CSRF defense too. Auth cookies need HttpOnly + Secure + SameSite.
- **Who hits it:** anyone setting a session/auth cookie by hand with default
  options.
- **Repro:** `new Cookie("session", token)` - HttpOnly false, Secure false.
  Deterministic, no packages.
- **Damage:** XSS steals the session (HttpOnly would have hidden it from script);
  a plain-http request leaks it on the wire (Secure would have withheld it).
- **😈 seed:** the cookie works perfectly in every test - it authenticates - so
  nothing flags that it is also script-readable and cleartext-sendable; the
  missing flags are invisible until an attacker uses them.
- **Verified:** ran on .NET 10 (2026-08-13): new Cookie defaults HttpOnly=false,
  Secure=false.

## Seeds

Not yet a full candidate - brainstorm before proposing.
