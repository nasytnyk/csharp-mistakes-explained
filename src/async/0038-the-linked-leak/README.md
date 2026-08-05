---
id: "0038"
title: A linked CancellationTokenSource never disposed
category: async
tags: [async, CancellationTokenSource, memory-leak]
rule: "never create a **linked** `CancellationTokenSource` without disposing it"
---

# #0038 - A Linked CancellationTokenSource Never Disposed

## 💥 Symptom

A long-running service leaks memory in a slow, maddening way: the working set
climbs a little with every request and never comes back down. Restart it and the
graph flattens, then starts the same climb. A memory dump shows thousands of
per-request objects still alive - request state, buffers, whole object graphs -
but the retained-by view points at no code you wrote. The path that keeps them
alive runs entirely through framework internals, so it looks like the runtime
itself is hoarding your requests. It is the textbook "restart cures it" leak, and
the cause is one missing `using`.

## 🔍 The Offending Code

```csharp
CancellationToken appStopping = host.ApplicationStopping;

// per request:
var linked = CancellationTokenSource.CreateLinkedTokenSource(appStopping); // 💥 never disposed
linked.CancelAfter(TimeSpan.FromSeconds(30));
linked.Token.Register(() => request.Cleanup());
// ... serve the request with linked.Token, then return - dropping `linked`
```

## 🧠 What's Actually Going On

The rooting runs the opposite direction from most people's mental model. You
picture the short-lived linked source holding a reference *up* to the long-lived
app token - harmless. It is the reverse.

`CreateLinkedTokenSource(appStopping)` has to make the linked source cancel when
the app token does, so it *registers a callback on the app token*. That
registration lives in the app token's own internal list, and it references the
linked source. So the long-lived token now holds the short-lived one:
`appStopping` -> registration -> linked CTS. And the linked CTS holds everything
*you* registered on it - `linked.Token.Register(() => request.Cleanup())` - whose
closure captures the request's object graph. The full chain is:

```
appStopping (process-lifetime)  ->  linked CTS  ->  your Register callback  ->  request state
```

Every link in that chain is a strong reference, and the head of it lives for the
whole process. Until the app token is cancelled (shutdown) or the linked CTS is
disposed, none of it can be collected. `Dispose()` is what removes the linked
source's registration from the app token, snapping the first link and freeing the
rest. Skip it, and each request pins its own graph until the process exits - one
slow drip per request, exactly matched to traffic.

## ✅ The Fix

Dispose the linked source. A `using` is almost always enough, because the linked
source is only needed for the lifetime of the request:

```csharp
using var linked = CancellationTokenSource.CreateLinkedTokenSource(appStopping);
linked.CancelAfter(TimeSpan.FromSeconds(30));
linked.Token.Register(() => request.Cleanup());
// ... serve the request; `using` disposes and unhooks it on the way out
```

Full version in [Good.cs](Good.cs). Disposing also stops any `CancelAfter` timer
and releases your registrations. Choosing the shape:

| Approach | When it's the right call |
|---|---|
| `using var linked = CreateLinkedTokenSource(...)` | The default - the linked token is scoped to one request/operation and dies with it |
| Dispose in a `finally` (or store it and dispose on completion) | The linked source must outlive a single lexical scope - a long-running operation you cancel elsewhere |
| Don't link: pass the app token *and* your own token separately | You only need to *observe* the app token, not fold it into one combined token - then there is nothing extra to dispose |

This is the same shape as two leaks the museum already exhibits: a registration on
a long-lived object never removed
([0010-immortal-subscriber](../../events/0010-immortal-subscriber/)) and a
disposable created per request but never disposed
([0014-container-hoarder](../../di-lifetimes/0014-container-hoarder/)).

## 😈 The Even Worse Sibling

The leak is the quiet part. The loud part comes at shutdown. When the app token
is finally cancelled - a graceful drain, a deploy - *every* leaked registration
fires. All those thousands of stale per-request cleanup callbacks run at once,
against request state that has been dead for hours, at the single worst moment:
the process is trying to exit. What looked like a memory graph slowly sloping up
turns into a burst of exceptions, slow finalizers, and a shutdown that misses its
deadline and gets killed hard. The leak you could see coming was the lucky half.

## 🎓 Advanced Nuance

It is the *link* that leaks, not the source. A plain
`new CancellationTokenSource()` you forget to dispose is nearly free - it holds a
timer only if you called `CancelAfter`, and is otherwise collected normally
because nothing long-lived roots it. The linked source is different precisely
because the parent token roots it, and that is by design: cancellation has to flow
downward, so the parent must keep a handle on every child.

One level finer, `CancellationTokenRegistration` is itself `IDisposable`. Calling
`longLivedToken.Register(callback)` and never disposing the returned registration
is the identical leak without any CTS in sight - a callback (and its captures)
pinned to a long-lived token. Disposing the linked CTS disposes the registrations
it owns; disposing the registration handle is the manual equivalent. The rule
underneath both: anything you attach to a token that outlives you, you must detach
- and the link a threaded [0016-token-tourism](../../async/0016-token-tourism/)
token creates is no exception.

## 🔎 How to Find It in Your Codebase

- Grep for `CreateLinkedTokenSource(` and check each hit is a `using` or is
  disposed in a `finally`: `CreateLinkedTokenSource\(` with no `using` on the
  line is the prime suspect.
- **CA2000** ("Dispose objects before losing scope") flags a
  `CreateLinkedTokenSource` result that is neither disposed nor returned - turn it
  on; it catches this directly. **IDE0067**/IDE0068 (dispose ownership) overlap.
- Also audit `longLivedToken.Register(...)` where the token is an
  application/host token: if the returned `CancellationTokenRegistration` is
  discarded, the callback and its captures leak for the token's life.
- In review, the tell is a linked source used only through `.Token` and then left
  to go out of scope - if nothing disposes it, the app token is quietly keeping
  the whole request alive.
