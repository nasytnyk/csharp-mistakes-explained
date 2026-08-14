---
id: "0062"
title: a timeout is a cancellation
category: http
tags: [HTTP, HttpClient, cancellation]
rule: "never read `OperationCanceledException` as a user cancel - an HttpClient **timeout** is the same type; check `InnerException`"
---

# #0062 - A Timeout Is a Cancellation

## 💥 Symptom

A resilience layer that retries on failure quietly stops retrying timeouts. Under load - exactly
when the server is slow and a retry would help most - requests that time out are logged as
"cancelled by user" and dropped: no retry, no alert. Nobody cancelled anything; the user was
not even on the screen. The retry policy everyone trusts turns out to be the component eating
the failures.

## 🔍 The Offending Code

```csharp
try
{
    await httpClient.GetAsync(url, userToken);
}
catch (OperationCanceledException) // 💥 a HttpClient.Timeout throws this too, not just user cancels
{
    return Abort("user cancelled - do not retry");
}
```

## 🧠 What's Actually Going On

`HttpClient.Timeout` is implemented as a cancellation: when it elapses, the client cancels the
request internally and the call throws `TaskCanceledException` - which derives from
`OperationCanceledException`, the *exact same type* a caller's `CancellationToken` produces when
the user cancels. The type system draws no line between "the server took too long" and "the user
changed their mind," so a `catch (OperationCanceledException)` written for user-cancel silently
absorbs every timeout as well.

The broken belief is "OperationCanceledException means someone cancelled." It also means *time
ran out* - and those two deserve opposite responses: a user cancel should stop, a timeout should
usually retry or alert. Classifying by exception *type* alone gets one of them wrong every time,
and because retry policies are written to *not* retry cancellations, the case it gets wrong is
the transient failure you most wanted to survive.

## ✅ The Fix

Since .NET 5 the two carry different payloads - a timeout's exception has an `InnerException` of
`TimeoutException`. Inspect it instead of trusting the type:

```csharp
catch (OperationCanceledException ex)
{
    return ex.InnerException is TimeoutException
        ? Retry("timeout")          // HttpClient.Timeout fired
        : Abort("user cancelled");  // the caller's token fired
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `ex.InnerException is TimeoutException` | The .NET 5+ signal that `HttpClient.Timeout` (not a token) fired - the simplest reliable discriminator. |
| Compare the token - `userToken.IsCancellationRequested` | You hold the caller's token and want "did *this* token cancel?" - robust across cancellation sources, HTTP or not. |
| Your own `CancellationTokenSource` with `CancelAfter` | You want per-attempt timeouts you fully control - your linked token fires, and you know it means "timeout" because you set it. |
| A resilience library (Polly `HandleTransientHttpError` + timeout policy) | It already classifies timeouts vs cancellations correctly - prefer it to hand-rolled catch logic. |

## 😈 The Even Worse Sibling

The confusion flows straight into telemetry. Cancels are "user behavior" and timeouts are
"incidents," but one `catch` funnels both into whichever counter it was written for - so the
outage dashboard stays flat and green while users watch requests fail, because every timeout was
booked as a voluntary cancel. It runs the other direction too: a retry policy that *does* retry
`OperationCanceledException` now retries genuine user cancellations, re-issuing work the user
explicitly stopped - burning quota and, if the call isn't idempotent, double-submitting. The one
conflated type means every policy is wrong for one of the two cases; you only get to choose
which. Same shape as [0015-cancellation-eaten-by-catch](../../exceptions/0015-cancellation-eaten-by-catch/):
a cancellation quietly caught by code that meant to catch something else.

## 🎓 Advanced Nuance

- **Before .NET 5 there was no payload to inspect.** The `InnerException = TimeoutException`
  distinction was added in .NET 5; on older runtimes the only tell was checking whether your own
  token was the one signalled. Code that predates the change often still classifies by type,
  carried forward untouched.
- **`TaskCanceledException` *is* an `OperationCanceledException`.** Catching the base type is
  correct for cancellation in general; the bug is *acting* on it without asking which token
  fired. Narrowing to `catch (TaskCanceledException)` buys nothing here - the timeout throws
  exactly that.
- **A linked token blurs the source.** If you pass a `CreateLinkedTokenSource(userToken, ...)`
  token into the call, the exception's `CancellationToken` is the *linked* token, not either
  original - so `ex.CancellationToken == userToken` is false even for a real user cancel. Check
  `userToken.IsCancellationRequested` rather than token identity when a linked source is in play.

## 🔎 How to Find It in Your Codebase

- Grep for `catch (OperationCanceledException` and `catch (TaskCanceledException` around HTTP
  calls (`GetAsync`, `SendAsync`, `PostAsync`) - each one that maps straight to "cancelled"
  without inspecting `InnerException` or the token is suspect.
- Symptom-side: retry/circuit-breaker metrics showing near-zero timeouts against a known-slow
  dependency; "user cancelled" counts that rise with load rather than with actual user action.
- Look in retry and 401-refresh middleware especially - that is where "don't retry cancellations"
  is a deliberate rule, and where a timeout wearing the cancel's type does the most damage.
- Prefer a resilience library, or an explicit `InnerException is TimeoutException` / token check,
  over a bare type catch wherever timeout and cancel must diverge.
