---
id: "0069"
title: a session cookie with default flags
category: security
tags: [security, cookies, session]
rule: "never ship a session cookie without **HttpOnly** and **Secure** - both default to off"
---

# #0069 - A Session Cookie With Default Flags

## 💥 Symptom

A single XSS bug becomes a full account takeover, or session tokens turn up in a plain-HTTP
request captured on a coffee-shop network - and the cookie was "just a normal session cookie." It
authenticates perfectly, every test passes, and nothing signals that the same cookie is readable
by any script on the page and sent over unencrypted HTTP. The flags that would have stopped both
were never set, because they default to off.

## 🔍 The Offending Code

```csharp
var sessionCookie = new Cookie("session", token); // 💥 HttpOnly = false, Secure = false by default
```

## 🧠 What's Actually Going On

A cookie carries its security in a few flags, and .NET leaves the two that matter most turned
off. `HttpOnly` defaults to `false`, so the cookie is visible to `document.cookie` - which means
any cross-site-scripting bug anywhere on the site can read the session token and ship it to an
attacker; `HttpOnly` is precisely what makes the token invisible to JavaScript and turns "we have
an XSS" into "we have an XSS that can't steal sessions." `Secure` defaults to `false`, so the
browser attaches the cookie to plain `http://` requests too - a stray link, a mixed-content asset,
a forced downgrade - and the token crosses the network in cleartext. `SameSite`, unset, leaves the
cookie riding along on cross-site requests, weakening CSRF defense.

The broken belief is "the framework gave me a secure cookie." It gave you a *working* cookie;
secure is opt-in. And because an insecure cookie authenticates exactly like a secure one, nothing
in normal use - or in the tests - reveals the missing flags. They only start to matter the moment
an attacker is present, which is exactly when you can no longer add them.

## ✅ The Fix

Set the flags explicitly on any cookie that carries authentication or session state:

```csharp
var sessionCookie = new Cookie("session", token)
{
    HttpOnly = true, // invisible to document.cookie - an XSS bug cannot read it
    Secure   = true, // only ever sent over HTTPS
};
// and set SameSite (Strict / Lax) via your framework's cookie options for CSRF defense
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| HttpOnly + Secure + SameSite | The default for auth/session cookies - never touchable by script, never sent in the clear, not attached cross-site. |
| The framework auth cookie handler (ASP.NET Core Identity) | You're in that stack - it sets these correctly for you; don't hand-roll session cookies. |
| `CookieOptions` + `SecurePolicy.Always` | Setting cookies through ASP.NET Core - configure the options: `HttpOnly = true`, `SecurePolicy.Always`, a `SameSiteMode`. |
| A cookie readable by script *on purpose* | A non-sensitive UI preference the front-end must read (theme, locale) - HttpOnly off is fine there, because there is no secret to steal. |

## 😈 The Even Worse Sibling

`HttpOnly` off is the loud risk - one XSS and the session is gone. The quieter one is `Secure` off
on a site that is "HTTPS everywhere anyway": it is, until a single `http://` link, an old bookmark,
a QR code, or an attacker on the network triggers one plain request to your domain - the browser
dutifully attaches the session cookie, in cleartext, *before* any redirect to HTTPS can happen, and
it's captured. Subtler still is the trap in the cookie's *name*: a cookie prefixed `__Host-` or
`__Secure-` is rejected outright by the browser unless it really is `Secure` and correctly scoped -
so those prefixes turn "forgot to set Secure" into a visible, loud failure, but only if you adopt
them; the plain name gives you no such guardrail. The cookie that logs users in flawlessly is the
one whose missing flags nobody ever notices.

## 🎓 Advanced Nuance

- **The defaults are off for backwards compatibility, not because they're safe.** `HttpOnly` and
  `Secure` both default to `false` across `System.Net.Cookie` and ASP.NET Core `CookieOptions`;
  there is no "session cookie" type that flips them - every cookie is the same object, and the
  security is entirely in what you set.
- **`SameSite` needs a value chosen on purpose.** `Strict` blocks the cookie on cross-site
  navigation (safest for pure app sessions); `Lax` allows top-level GETs (needed when users follow
  links into an authenticated area). Unset behaves like `Lax` in modern browsers, but relying on
  the browser default is fragile - state it.
- **`__Host-` / `__Secure-` prefixes are enforced by the browser.** Naming the cookie
  `__Host-session` makes the browser reject it unless it is `Secure`, has no `Domain`, and
  `Path=/` - a name that fails loudly when the flags are wrong, which beats a flag you can silently
  forget.

## 🔎 How to Find It in Your Codebase

- Grep for `new Cookie(`, `new CookieOptions`, and `Response.Cookies.Append` and check each
  auth/session cookie for `HttpOnly = true`, `Secure = true` (or `SecurePolicy.Always`), and a
  `SameSite` value - the defaults are insecure.
- Prefer the framework's auth cookie handler over hand-rolled session cookies; it sets the flags
  for you.
- Symptom-side: session tokens visible in `document.cookie` in the browser console; the cookie
  sent on `http://` requests (check the network tab); a pen-test finding of "cookie without
  HttpOnly / Secure."
- Consider `__Host-` / `__Secure-` name prefixes so a missing `Secure` flag becomes a
  browser-rejected cookie instead of a silent weakness.
