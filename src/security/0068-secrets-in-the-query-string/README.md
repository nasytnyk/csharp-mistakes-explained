---
id: "0068"
title: a secret in the query string ends up in the logs
category: security
tags: [security, HttpClient, secrets]
rule: "never put a secret in the **query string** - it's logged server-side even over HTTPS; use a header"
---

# #0068 - A Secret in the Query String Ends Up in the Logs

## 💥 Symptom

An API key has to be rotated because it leaked - and no one can find where. It was never printed,
never committed, never in an error message. It was in a URL: `GET /reports?api_key=sk_live_...`.
That one line is now sitting in the web server's access log, the load balancer's log, the CDN's
log, a few engineers' browser history, and the `Referer` header every third-party script on the
page received. HTTPS was on the whole time.

## 🔍 The Offending Code

```csharp
var response = await client.GetAsync($"reports/monthly?api_key={apiKey}"); // 💥 secret in the URL
```

## 🧠 What's Actually Going On

TLS encrypts the request *in transit* - including the URL - so "it's HTTPS, it's fine" feels
right. But the URL is decrypted the instant it reaches the other end, and from there it is treated
as routing metadata, not as a secret: the web server writes the full request line (method, path,
and query) to its access log by default, and so does every proxy, load balancer, and CDN in front
of it. On the client side, the browser stores the URL in history and autocomplete and sends it in
the `Referer` header to any resource the page loads - including third-party analytics and ad
scripts. A secret in the query string is a secret copied, in plaintext, into a dozen systems built
to log URLs precisely because URLs were never meant to be sensitive.

The broken belief is "HTTPS protects the URL, so a token in the query is safe." Encryption protects
the wire; it does nothing about the log the receiver writes the moment it decrypts. The query
string is the one part of the request that everyone logs and no one treats as a secret.

## ✅ The Fix

Carry secrets in a request header - the `Authorization` header exists for exactly this - or in the
body of a POST; never in the URL:

```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, "reports/monthly");
request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey); // not logged with the URL
var response = await client.SendAsync(request);
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `Authorization: Bearer <token>` header | The default for API keys and tokens - standard, and access logs record the URL, not the headers. |
| A custom header (`X-Api-Key`) | The API defines its own key header - still out of the URL and the logs; use what the service documents. |
| The body of a POST | A secret that is data, not auth (a one-time code, a link token you're redeeming) - keep it in the body, not the query. |
| A short-lived, single-use token *if* it must be in a URL | Signed download / reset links sometimes must put a token in the query - then make it expire fast and burn on first use, so a logged copy is worthless minutes later. |

## 😈 The Even Worse Sibling

The API key is the obvious case; the quiet leaks are the tokens you don't think of as secrets. A
password-reset link, a magic-login link, a signed document URL - all routinely carry their token in
the query, all land in the same logs and the same browser history, and any one of them is a full
account takeover for whoever reads the log or is handed the link. And `Referer` makes it worse than
logging: the moment a page loaded with a secret in its URL pulls in a third-party script or image,
the browser hands that secret to a server you do not control, automatically. Rotating the key fixes
the API case; you cannot rotate a reset link someone already used, and you cannot un-send a
`Referer`. Same secret-in-a-URL, but the token variants leak *off* your systems entirely. It is the
same secret-reaches-a-log ending as [0049-record-tostring-leaks-secrets](../../records/0049-record-tostring-leaks-secrets/),
arrived at through the URL instead of a log statement.

## 🎓 Advanced Nuance

- **`https` does not exempt the URL from logging.** TLS encrypts the URL on the wire, so people
  conclude it is protected end to end. It is protected until the first hop that terminates TLS -
  your own load balancer - which logs it in the clear; every hop after re-logs it.
- **`Referer` leakage is automatic and cross-origin.** A page at `https://app/confirm?token=...`
  that loads any external asset sends `Referer: https://app/confirm?token=...` to that asset's host
  by default. `Referrer-Policy: no-referrer` / `strict-origin` mitigates it, but the real fix is to
  keep the secret out of the URL so there is nothing to leak.
- **Logs outlive rotation and spread.** Access logs are shipped to aggregators, retained for
  months, and read by more people and systems than the database is - a secret that reaches a log
  has a far larger blast radius, and a far longer life, than one that stayed in a header the logger
  was configured to redact.

## 🔎 How to Find It in Your Codebase

- Grep for `?api_key=`, `?token=`, `?access_token=`, `?key=`, `?password=`, and `&sig=`-style
  parameters in URL construction (`GetAsync($"...?...")`, `UriBuilder.Query`,
  `QueryHelpers.AddQueryString`) - any secret assembled into a query is the shape.
- Move API keys and tokens to the `Authorization` (or a documented custom) header; put redeemable
  secrets in a POST body.
- Symptom-side: keys that leak with no obvious source (check the access logs - they are probably in
  the URLs); reset / magic links that still work after they should have been used once.
- For links that must carry a token, make it single-use and short-lived, and set a strict
  `Referrer-Policy` on any page that renders a secret in its URL.
