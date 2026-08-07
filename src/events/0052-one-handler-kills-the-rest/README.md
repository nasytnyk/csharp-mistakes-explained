---
id: "0052"
title: One throwing handler kills the rest
category: events
tags: [events, delegates, exceptions]
rule: "never raise an event unguarded - one throwing handler aborts the whole **invocation list**"
---

# #0052 - One Throwing Handler Kills the Rest

## 💥 Symptom

Orders are placed, confirmation emails go out, analytics tick up - but the audit log
has holes. Not random holes: exactly the orders where some *other*, unrelated
subscriber happened to fail. A flaky analytics call throws, and the compliance record
for that order silently never gets written. The publisher logged "a subscriber failed"
and moved on, with no idea it had also dropped every handler registered after the one
that threw.

## 🔍 The Offending Code

```csharp
service.OrderPlaced += o => SendEmail(o);        // runs
service.OrderPlaced += o => UpdateAnalytics(o);  // 💥 throws (503)
service.OrderPlaced += o => WriteAudit(o);        // never runs

// in the publisher:
try { OrderPlaced?.Invoke(order); }
catch (Exception ex) { log.Warn($"a subscriber failed: {ex.Message}"); } // catches ONE, hides the rest
```

## 🧠 What's Actually Going On

A multicast delegate raises its subscribers by walking the **invocation list** in
subscription order, synchronously, on the publisher's single thread. `OrderPlaced?.Invoke(order)`
is *one* method call that runs them one after another on the same stack. When a handler
throws, that exception propagates straight out of `Invoke` and the walk stops right
there: every subscriber *after* the one that threw is skipped, and the exception
surfaces at the publisher's raise line - in code that has no notion of which subscribers
exist or how many were left un-run.

So the publisher's `catch` is a trap dressed as safety. It sees one exception and
assumes it isolated one bad subscriber - but the invocation list already aborted, and
"carry on" now means "carry on having silently dropped the rest." The broken belief is
"each handler is independent; one failing just fails itself." They are not independent:
they share one thread and one stack, and the first throw ends the entire broadcast.

## ✅ The Fix

Raise the handlers yourself, isolating each in its own `try`/`catch`, so one failure
cannot stop the others:

```csharp
var handlers = OrderPlaced;
if (handlers is null) return;

foreach (Action<string> handler in handlers.GetInvocationList())
{
    try { handler(order); }
    catch (Exception ex) { log.Warn($"a subscriber failed handling {order}: {ex.Message}"); }
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| Iterate `GetInvocationList()`, `try`/`catch` each handler | The default when the publisher must notify *all* subscribers regardless of one failing - audit, analytics, email fan-out. |
| Collect the failures, throw an `AggregateException` after the loop | You need every handler to run *and* the caller to learn some failed - run all, then don't swallow. |
| Keep `Invoke`, guarantee handlers never throw | If the subscribers are yours, wrap each handler *body* in its own guard - the contract "handlers don't throw" is enforced at the source, publisher stays simple. |
| A real message bus / mediator (channels, MediatR) | Cross-cutting fan-out that needs retries, ordering, or delivery guarantees belongs in infrastructure, not a raw `event`. |

## 😈 The Even Worse Sibling

Who dies is decided by **subscription order** - which, in a DI-wired app, is
**registration order**. The audit handler survives or is skipped based purely on whether
it was `+=`'d before or after the flaky one, and nothing at the raise site shows that
ordering. Reorder two `services.Add...` lines, or slot a new subscriber into the middle,
and you have changed which orders get audited - a behavior change no diff reviewer will
ever recognize as one. And removing the publisher's `catch` is louder but no better: the
handler exception now crashes the whole order-placement call, so one flaky analytics
subscriber takes down checkout for everyone. As usual, the crash is the *lucky* outcome -
at least it is visible; the silent skip is the one that ships holes into the compliance
log.

## 🎓 Advanced Nuance

- **Async handlers escape the guard entirely.** An `async void` subscriber (the only
  async shape an `Action`-typed event accepts) returns to the invocation walk at its
  first `await`; the publisher's `try`/`catch` has already unwound by the time the
  handler's real work throws, so that exception surfaces later on the synchronization
  context and can crash the process - the invocation list "completed" while the work
  failed after the fact. This is the event-fan-out face of
  [0007-async-void](../../async/0007-async-void/).
- **A returning delegate discards all but the last.** For an event whose delegate has a
  return type, a multicast `Invoke` hands you only the *last* subscriber's return value;
  every earlier one is thrown away. Fan-out that needs each result must walk
  `GetInvocationList()` regardless of the exception issue.
- **The stack trace lies about the culprit.** The exception carries the *handler's*
  stack but surfaces at the publisher's `Invoke` line, so "why did placing an order throw
  an analytics 503?" reads as impossible until you remember the subscribers all run on
  the raise thread.

## 🔎 How to Find It in Your Codebase

- Grep for `.Invoke(` / `?.Invoke(` on events and for bare `EventName(args)` raise sites.
  Any that are not wrapped *per handler* are all-or-nothing broadcasts - especially where
  audit, compliance, or outbox handlers subscribe to a shared domain event.
- Look for a publisher-side `try { Raise(); } catch` that logs and continues: it does not
  isolate handlers, it only hides that the list aborted. The isolation has to be *inside*
  the loop over `GetInvocationList()`.
- Any `event Action<...>` whose subscribers do real, independent side effects (email,
  audit, analytics) is a fan-out that should not be all-or-nothing - flag it for
  per-handler isolation or a real bus.
- No analyzer flags "this handler might throw." Treat every subscriber as capable of
  throwing, and every raw `event` fan-out with independent side effects as needing
  isolation.
