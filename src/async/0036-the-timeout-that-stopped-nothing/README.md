---
id: "0036"
title: A WhenAny timeout that never stopped the work
category: async
tags: [async, Task.WhenAny, CancellationToken]
rule: "never treat a `WhenAny` **timeout** as stopping the work - cancel it"
---

# #0036 - A WhenAny Timeout That Never Stopped the Work

## 💥 Symptom

Reconciliation flags an order that was paid twice. The customer sees two
identical charges a second or two apart; support opens a ticket. You pull the
logs for that order and they look *reassuring*: the first attempt "timed out",
so the service retried, and the retry succeeded. One timeout, one retry, one
success - and yet the card moved twice. There is no exception anywhere, no failed
task, no alert. The timeout did exactly what the code said and the money still
left twice.

## 🔍 The Offending Code

```csharp
var charge  = ChargeCardAsync(order);          // the real work
var timeout = Task.Delay(TimeSpan.FromSeconds(5));

if (await Task.WhenAny(charge, timeout) == timeout)
{
    Log("payment timed out - retrying");       // 💥 charge is still running
    // ...retry, believing the first attempt did nothing
}
```

## 🧠 What's Actually Going On

`Task.WhenAny` completes as soon as the *first* of its tasks does, and hands you
that task so you can see who won. That is all it does. It does not touch the
loser: the `charge` task is not cancelled, not paused, not even looked at.
Nothing in this code path so much as asks it to stop.

So when the delay wins, `charge` is still in flight. The method logs "timed out"
and moves on, while the payment call finishes its network round-trip a moment
later and charges the card - a side effect the caller has already decided did not
happen. Add the near-universal "on timeout, retry" and the retry charges it
*again*: one order, two charges, both logged as normal.

The broken mental model is reading `WhenAny(work, Task.Delay(t))` as "run the
work for at most `t`". It means "tell me which finished first". `Task.Delay` is a
stopwatch, not a leash - racing a stopwatch against your work changes nothing
about the work. "Timeout" here is the caller saying *I stopped watching*, not the
operation saying *I stopped happening*.

## ✅ The Fix

A timeout has to be able to *stop* the thing it is timing, and the only mechanism
for that is a `CancellationToken` the operation actually honors. Give the work a
token, cancel it when time runs out, and check the token before the irreversible
step:

```csharp
async Task ChargeCardAsync(Task gateway, CancellationToken ct)
{
    await gateway.WaitAsync(ct);
    ct.ThrowIfCancellationRequested();   // never charge if we were told to stop
    Charge();
}

if (await Task.WhenAny(attempt1, timeout) == timeout)
    cts.Cancel();                        // stop the abandoned work, not just the waiting
```

Full version in [Good.cs](Good.cs). Better still, most timeouts do not need
`WhenAny` at all - a `CancellationTokenSource` with a deadline *is* the timeout.
Picking the right shape:

| Approach | When it's the right call |
|---|---|
| `new CancellationTokenSource(timeout)`, token passed into the call | The default. The timeout *is* the cancellation; there is no separate loser to abandon |
| `Task.WhenAny(work, delay)` **and `cts.Cancel()` on timeout** | You genuinely need to react to whichever finishes first - but you must still cancel the loser, as here |
| `await work.WaitAsync(timeout, ct)` | Concise "throw if too slow" - but only over work that is **cancellable and idempotent**; on its own it throws without stopping anything (see Advanced Nuance) |
| Ignore the loser | Never, for work with a side effect - that is this exhibit |

Passing the token is only half of it: the operation has to observe it, or you
have a [0016-token-tourism](../../async/0016-token-tourism/) - a cancellation
that no one downstream ever checks.

## 😈 The Even Worse Sibling

Here the abandoned call *succeeds*, so the damage is "only" a double charge that
reconciliation can eventually catch. Now let the abandoned call **fault** instead
- a declined card, a 500 from the gateway. Nobody is awaiting `charge` anymore;
the caller unwound at the timeout. Its exception has no one to surface it, so it
becomes a [0019-forgotten-task](../../async/0019-forgotten-task/): raised on a
pool thread with no observer, it resurfaces minutes later as
`TaskScheduler.UnobservedTaskException` - a crash report pointing at a line of
code that finished running long ago, in a request that already logged success.
The double charge, at least, leaves a trail. The unobserved fault is the same
disappearing act `await Task.WhenAll` pulls in
[0021-whenall-hides-exceptions](../../async/0021-whenall-hides-exceptions/): the
visible loss in this exhibit is the *lucky* outcome.

## 🎓 Advanced Nuance

`Task.WaitAsync(TimeSpan)` (.NET 6+) looks like the fix and is not. It throws a
clean `TimeoutException` when the deadline passes - but the task it was waiting on
keeps running to completion, side effects and all. It is `WhenAny` with tidier
syntax and the same abandoned loser: verified on .NET 10, the timed-out work
still ran its charge after `WaitAsync` had already thrown. It stops your
*waiting*, never the *work*; use it only over operations that are themselves
cancellable and idempotent, and thread a real token through for the actual stop.

The deeper rule: a timeout can only be as real as the cancellation underneath it.
`HttpClient.Timeout` genuinely aborts the request because the handler wires the
timeout to the request's own cancellation - but the moment you wrap `SendAsync`
in your own `WhenAny`/`WaitAsync` to get a *different* timeout, you are back to
abandoning in-flight work unless you also pass and honor a token.

## 🔎 How to Find It in Your Codebase

- Grep for the idiom: `Task.WhenAny(` with a `Task.Delay(` on the same or a
  neighboring line, and `.WaitAsync(` calls that take a `TimeSpan` but no
  `CancellationToken`. Each is a timeout that may not stop anything.
- For every hit, ask the one question that matters: **does a
  `CancellationToken` reach the timed operation, and does the operation check
  it?** If the timed call takes no token, the timeout is decorative.
- The tell in review is a `Task.Delay` whose *only* purpose is to lose a race -
  its completion is inspected, but its token (if any) never travels into the work
  it is supposedly bounding.
- Analyzers are quiet here because every piece is individually legal, so this is a
  design-review check, not a red squiggle. Watch it hardest around payments,
  order placement, and any non-idempotent `POST` - the places a silent second
  run costs real money.
