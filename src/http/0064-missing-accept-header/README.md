---
id: "0064"
title: no Accept header lets the server choose
category: http
tags: [HTTP, HttpClient, content-negotiation]
rule: "never expect JSON without an `Accept` header - the server sends its **default** format instead"
---

# #0064 - No Accept Header Lets the Server Choose

## 💥 Symptom

Your JSON parse throws "`'<' is an invalid start of a value`," or the deserialized object comes
back all-defaults - on a response the API insists is fine. The same URL returns clean JSON in
Postman or the browser's network tab, but from your `HttpClient` the body is XML (or HTML, or
something else). Nobody changed the endpoint; the request just never said what it wanted back.

## 🔍 The Offending Code

```csharp
var body = await client.GetStringAsync(url); // 💥 no Accept header - server sends its default
var order = JsonSerializer.Deserialize<Order>(body);
// the API content-negotiates and defaults to XML -> reading it as JSON fails
```

## 🧠 What's Actually Going On

`HttpClient` sends no `Accept` header unless you add one. `Accept` is how a client states which
representations it can handle; without it, a content-negotiating server is free to return whatever
it considers its default - and for plenty of APIs (ASP.NET Core with the XML formatters enabled,
older WCF/SOAP-flavored services, anything driven by `[Produces]` over multiple formatters) that
default is not JSON. So the bytes arrive fine, the status is `200`, and the body is simply in a
format you did not ask for. Reading XML as JSON then throws, or - where the shapes overlap loosely
- binds to defaults.

The broken belief is "the API returns JSON, so I'll get JSON." The API returns JSON *when asked*,
and asking is the `Accept` header. curl, Postman, and browsers tend to send a permissive `Accept`
(or the server's default happens to suit them), which is why the request works everywhere except
your code.

## ✅ The Fix

Tell the server what you accept - set `Accept: application/json`, or use a helper that sets it for
you:

```csharp
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
var body = await client.GetStringAsync(url);
// or skip the manual read entirely - GetFromJsonAsync sets Accept: application/json itself:
var order = await client.GetFromJsonAsync<Order>(url);
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `GetFromJsonAsync` / `PostAsJsonAsync` | Object in, object out - the `System.Net.Http.Json` helpers set `Accept: application/json` and deserialize for you; one less header to forget. |
| `DefaultRequestHeaders.Accept.Add("application/json")` | You use `GetStringAsync`/`GetAsync` and parse yourself, or reuse one client for many JSON calls - set it once on the client. |
| Per-request `Accept` on an `HttpRequestMessage` | Different endpoints want different formats from the same client - set `request.Headers.Accept` per call rather than globally. |
| An explicit / quality-weighted `Accept` | The API speaks a vendor type (`application/vnd.api+json`) or you want a preference order (`application/json, text/plain;q=0.9`) - build the header by hand. |

## 😈 The Even Worse Sibling

The `JsonException` is the lucky outcome - loud, and pointing near the truth. The quiet version is
a format whose field names overlap your JSON property names loosely enough that a lenient reader -
a `dynamic`, a hand-rolled parser, an XML-to-object mapper someone added "just in case" - extracts
*some* values and leaves the rest at defaults, so the order deserializes to `Total = 0` and ships,
no error, from a body that was never JSON. And the mirror is the send side,
[0062-stringcontent-defaults-to-text-plain](../0062-stringcontent-defaults-to-text-plain/):
`Content-Type` says what you are sending, `Accept` says what you will take back, and dropping
either lets the wire format drift from what your code assumes - one earns a `415`, the other earns
you the wrong format with a `200`.

## 🎓 Advanced Nuance

- **`GetFromJsonAsync` sets `Accept` for you; `GetStringAsync`/`GetAsync` do not.** Moving from the
  `System.Net.Http.Json` helpers to a manual read "to add a log line" can silently reintroduce this
  bug - the helper was carrying the header.
- **No `Accept` means "anything," not "JSON."** An absent `Accept` is treated as `*/*`: the client
  claims to accept every media type, so the server is fully within spec to send its default. The
  header is not optional politeness; it is the negotiation.
- **A stricter server answers `406 Not Acceptable` instead.** One that cannot satisfy your `Accept`
  returns `406` rather than its default - so the same missing or too-narrow header surfaces as a
  JSON parse failure on one API and a `406` on another, for the identical client bug.

## 🔎 How to Find It in Your Codebase

- Grep for `GetStringAsync` / `GetAsync` followed by `JsonSerializer.Deserialize` (or
  `JsonDocument.Parse`) with no `Accept` set on the client or request - the manual read that forgot
  to negotiate.
- Prefer `GetFromJsonAsync` / `PostAsJsonAsync` for JSON endpoints; when you read manually, set
  `DefaultRequestHeaders.Accept` once on the client.
- Symptom-side: `JsonException` ("`'<' is an invalid start of a value`") on a response that looks
  fine in Postman; DTOs deserialized to all-defaults from a `200`; intermittent `406`s.
- In tests, assert the outgoing `request.Headers.Accept` on JSON calls - a stub handler that
  returns XML unless `application/json` is accepted catches the missing header before it ships.
