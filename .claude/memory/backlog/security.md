# 🔒 security

> Status: **planned**. Canonical hall registry (emoji, display name, opened/planned) is `.claude/memory/halls.md`.
> Entry format and maintenance rules are in `.claude/memory/backlog/README.md`.

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
