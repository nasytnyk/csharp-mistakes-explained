---
id: "0067"
title: returning exception details to the client
category: security
tags: [security, exceptions, information-disclosure]
rule: "never return an exception to the **client** - even `.Message` leaks internals; log it, return a trace id"
---

# #0067 - Returning Exception Details to the Client

## 💥 Symptom

An error response, in production, reads like a stack trace: a file path, a SQL statement, a
server name, a full exception dump. A user - or anyone poking the endpoint - sends a bad request
and gets back `/srv/app/config/db-secret.conf`, `Server=prod-sql-07;Database=...`, or the exact
line of code that threw. Nothing crashed; the app politely handed its internals to the caller.

## 🔍 The Offending Code

```csharp
catch (Exception ex)
{
    return BadRequest($"Error: {ex.Message}"); // 💥 the message ships internals to the client
}
```

## 🧠 What's Actually Going On

An exception's `Message` is written for developers, not strangers - it names whatever the runtime
needed to describe the failure, and that is routinely sensitive. `FileNotFoundException.Message`
carries the full path; a `SqlException` names the server, the database, and often the query; a
config or auth error can echo a connection string or a key. Returning it - or worse,
`ex.ToString()`, which adds the type and stack trace - turns every error into a free reconnaissance
report: the caller learns your filesystem layout, your database topology, your framework versions,
and where the seams are, without running a single exploit.

The broken belief is "it's just the message, not the whole stack, so it's safe." The *message
alone* leaks. And this ships so often because returning `ex.Message` is genuinely useful while
you're developing - it's how you debug - so it goes in "temporarily," survives review because it
reads like helpful error handling, and reaches production because nothing fails when it does.

## ✅ The Fix

Log the full detail where only you can read it; return the client a generic message and a
correlation id that ties the two together:

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "request {TraceId} failed", traceId); // full detail, server-side
    return Problem(detail: "An unexpected error occurred.", extensions: new() { ["traceId"] = traceId });
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| Generic message + a trace/correlation id | The default - the client gets "something went wrong, ref X," support looks up X in the logs, and no internals cross the boundary. |
| `ProblemDetails` (RFC 7807) | An API that needs structured errors - return a type/title/status the client can act on, with `detail` kept free of internals. |
| A curated message for *expected* failures | Validation and business errors the caller *should* see ("quantity must be positive") - intentional, safe messages, not raw exception text. |
| Framework error handling, prod-configured | ASP.NET Core's Developer Exception Page is dev-only for this reason; `UseExceptionHandler` returns a generic body in production - don't re-open the leak by catching and echoing. |

## 😈 The Even Worse Sibling

The leak you can see is the lucky one. The quiet version is the *unhandled* exception a
misconfigured app renders as a full developer error page - the Developer Exception Page left on in
production, or a custom `500` view that prints `ex.ToString()` - so the stack trace, a source
snippet, and the environment ship on every unhandled error with no `catch` in sight. The subtle
inverse bites too: strip too much and you swallow the failure - `catch (Exception) { return Ok(); }`
- leaking nothing and *knowing* nothing, because it never reached your logs either. The target is
narrow: full detail to the log, a generic acknowledgement to the client, and never the two
swapped. Same client boundary as
[0066-the-server-that-trusted-the-request](../0066-the-server-that-trusted-the-request/) - that one
guards what you *accept* from the caller; this guards what you *reveal* to them.

## 🎓 Advanced Nuance

- **`ex.Message` is unstable and localized - it is not an API.** Beyond the leak, exception text
  changes across runtime versions and cultures, so clients that parse it break, and you have
  coupled callers to prose meant for a log. Return a stable machine-readable code, not the message.
- **The correlation id is the whole trick.** A random `traceId` on the response and in the log
  entry lets support and the user reproduce an incident without the response carrying any detail -
  you keep debuggability and lose the disclosure.
- **The leak often lives in shared middleware, not the `catch` you're reading.** A global exception
  filter that logs *and* returns `ex.ToString()`, a serializer that includes exception data, a
  `ProblemDetails` pipeline that echoes `Exception.Message` - audit the error pipeline, not just
  individual call sites.

## 🔎 How to Find It in Your Codebase

- Grep for `ex.Message`, `ex.ToString()`, `.StackTrace`, and an `Exception` reaching a response -
  `return BadRequest(ex.Message)`, `Content(ex.ToString())`, an error DTO built from an exception.
- Confirm detailed errors are dev-only (`app.Environment.IsDevelopment()`) and that a production
  `UseExceptionHandler` returns a generic body.
- Symptom-side: error responses containing file paths, `Server=` / `Data Source=`, type names, or
  `at Namespace.Method(...)` stack frames; support tickets that quote internal identifiers.
- Return a generic message plus a correlation id, log the exception server-side, and reserve
  specific messages for intentional, safe validation and business errors.
