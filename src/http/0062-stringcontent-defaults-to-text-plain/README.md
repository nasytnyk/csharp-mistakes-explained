---
id: "0062"
title: StringContent defaults to text/plain
category: http
tags: [HTTP, HttpClient, StringContent]
rule: "never post JSON with `new StringContent(json)` - it's **text/plain**, set `application/json`"
---

# #0062 - StringContent Defaults to text/plain

## 💥 Symptom

A POST that looks correct is rejected with `415 Unsupported Media Type` - or, worse, accepted
with an empty body the server never read. The JSON is right there in the request; you can print
it and it's valid. Yet the API that works fine from Postman or curl refuses the same payload from
your `HttpClient`, and the only difference is a header nobody set on purpose.

## 🔍 The Offending Code

```csharp
var content = new StringContent(json); // 💥 Content-Type: text/plain; charset=utf-8
await client.PostAsync("orders", content);
// the API requires application/json -> 415, before it ever parses the body
```

## 🧠 What's Actually Going On

`new StringContent(text)` has to put *some* `Content-Type` on the request, and its default is
`text/plain; charset=utf-8` - not `application/json`. The string can be perfectly valid JSON; the
header says otherwise. A server that content-negotiates by media type - which is most JSON APIs,
and every ASP.NET Core `[ApiController]` with a `[FromBody]` parameter - rejects the request with
`415` *before* deserialization runs, because it was told the body is plain text. Nothing about
the JSON is wrong; the envelope is mislabeled.

The broken belief is "`StringContent(json)` sends JSON." It sends a string and labels it text.
The media type is a separate decision `StringContent` makes for you, and its default is the
safe-for-text, wrong-for-everything-else `text/plain`.

## ✅ The Fix

Set the media type explicitly, or use a content type that serializes and labels in one step:

```csharp
var content = new StringContent(json, Encoding.UTF8, "application/json"); // labeled correctly
// or skip the manual serialization entirely:
await client.PostAsJsonAsync("orders", order);   // System.Net.Http.Json - serializes AND labels
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `new StringContent(json, Encoding.UTF8, "application/json")` | You already have a JSON string and just need it labeled - one extra argument fixes the header. |
| `JsonContent.Create(obj)` / `PostAsJsonAsync(url, obj)` | You have an object, not a string - it serializes with System.Text.Json *and* sets application/json; no manual header, fewer moving parts. |
| `new StringContent(text)` on purpose | The body really is plain text - a log line, a CSV, a token - then text/plain is correct; leave it. |
| A specific media type the API documents | Some endpoints want `application/vnd.api+json`, `application/x-ndjson`, or a charset variant - pass exactly what they ask for. |

## 😈 The Even Worse Sibling

`415` is the *lucky* outcome - loud and immediate. The quiet version is an endpoint that doesn't
enforce the media type: it reads the body anyway, your JSON parses, and everything works in dev
and test. Then the same code meets a stricter gateway, a WAF, or a proxy that routes by
`Content-Type`, and it starts failing in one environment only - the header that was wrong all
along finally met something that checks it. The mirror case is quieter still: post form fields
(`application/x-www-form-urlencoded`) mislabeled as text/plain and a model binder silently binds
nothing, so every field arrives as its default and the request "succeeds" with an empty object.
The wrong `Content-Type` either gets you rejected or gets you silently misread; which one depends
entirely on how strict the other side happens to be.

## 🎓 Advanced Nuance

- **The charset default is opinionated too.** `StringContent` appends `; charset=utf-8` and
  encodes as UTF-8 unless you pass a different `Encoding`. Usually right - but an API that expects
  a specific charset, or rejects the parameter entirely, needs it set or stripped by assigning
  `content.Headers.ContentType` directly.
- **`Content-Type` is a *content* header, not a request header.** It lives on `content.Headers`,
  never on `request.Headers` or `client.DefaultRequestHeaders` - trying to
  `DefaultRequestHeaders.Add("Content-Type", ...)` throws `InvalidOperationException`, because the
  type describes the body, not the request.
- **`PostAsJsonAsync` uses web defaults, not your global options.** The `System.Net.Http.Json`
  helpers serialize with `JsonSerializerDefaults.Web` (camelCase, case-insensitive) unless you
  pass `JsonSerializerOptions` - convenient, but a surprise if you assumed PascalCase on the wire.

## 🔎 How to Find It in Your Codebase

- Grep for `new StringContent(` and check each for a media-type argument - a one-argument
  `StringContent` carrying JSON, XML, or form data is mislabeled as text/plain.
- Prefer `PostAsJsonAsync` / `JsonContent.Create` for object bodies; reserve raw `StringContent`
  for genuinely plain text, and when you use it for anything else, pass the media type.
- Symptom-side: `415`s that only your code produces (Postman and curl work because they set the
  header); model-bound endpoints receiving all-default objects from a request that "posted fine."
- In tests, assert `request.Content.Headers.ContentType.MediaType` on the outgoing request - a
  stub handler that checks the media type catches this whole class of bug before it ships.
