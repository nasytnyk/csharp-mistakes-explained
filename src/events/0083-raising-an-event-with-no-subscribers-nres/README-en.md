---
id: "0083"
title: raising an event with no subscribers
category: events
tags: [events, delegates, null]
rule: "never raise an event as `E(args)` - with no subscribers it is a **null delegate**; use `E?.Invoke(args)`"
---

# #0083 - Raising an Event With No Subscribers

## 💥 Symptom

The feature worked on every machine it was tested on, then threw
`NullReferenceException` in production - from inside the publisher, on a line that
just raises an event. Nothing was null in the obvious sense: the service exists,
the order is valid, the event is declared. The one thing missing is a
*subscriber*, and on the path that ran, nobody had attached a handler.

## 🔍 The Offending Code

```csharp
public event EventHandler<string>? OrderPlaced;

public void PlaceOrder(string id, decimal amount)
{
    // ... persist the order ...
    OrderPlaced(this, id); // 💥 no handlers -> OrderPlaced is null -> NullReferenceException
}
```

## 🧠 What's Actually Going On

An event is backed by a multicast delegate field, and that field is `null` until
the first `+=`. Raising the event is really *invoking the delegate*, and invoking
a `null` delegate is a null dereference - `OrderPlaced(this, id)` on an empty
event is `null.Invoke(...)`. Each `+=` sets the field to a non-null combined
delegate and each `-=` can set it back toward `null`, so "does this throw?"
depends entirely on whether anyone is currently subscribed - state the publisher
does not control and usually cannot see.

The broken belief is "the event is declared, so raising it is safe." Declaring an
event only creates the slot; it does not put a handler in it. The reason this
survives testing is cruel: tests almost always attach a handler (that is how they
observe the event), so the delegate is non-null and the raise works - the exact
condition that hides the bug. Production then runs a path where the subscriber
has not been wired yet, was removed, or belongs to a component that is disabled,
and the same line that passed every test dereferences null. The compiler even
warns (`CS8602`, "dereference of a possibly null reference") on the nullable
event, and that warning is precisely the one people wave away.

## ✅ The Fix

Raise the event with the null-conditional operator, so an empty invocation list
is a no-op instead of a crash.

```csharp
public void PlaceOrder(string id, decimal amount)
{
    // ... persist the order ...
    OrderPlaced?.Invoke(this, id); // no subscribers -> no-op
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| `E?.Invoke(args)` | The standard raise - null-conditional invocation is the idiom for every event, and it is thread-safe against a handler unsubscribing between the null check and the call. |
| A protected `OnX` method | A class others derive from - wrap the raise in `protected virtual void OnOrderPlaced(...)` that does the `?.Invoke`, so every call site and subclass raises it safely. |
| Seed the event with an empty handler | You want the field never-null - `public event EventHandler<string> OrderPlaced = delegate { };` makes a bare `OrderPlaced(this, id)` safe, at the cost of one always-present no-op subscriber. |
| A capture-then-invoke pair | Pre-`?.` style or an added null check - copy the delegate to a local first (`var h = OrderPlaced; if (h != null) h(...)`) to avoid a race where `-=` nulls it mid-check; `?.Invoke` already does this for you. |

## 😈 The Even Worse Sibling

A crash the moment there are no subscribers is loud and immediate - you find it
fast. The quieter cousin is the copy-then-check written *wrong*: `if (OrderPlaced
!= null) OrderPlaced(this, id)` reintroduces a race - a handler can unsubscribe on
another thread between the check and the call, and the raise NREs anyway, but now
only occasionally, only under load, and never in the debugger. Nastier still is
the reverse failure the null-conditional cannot save you from: once a subscriber
*does* exist, a multicast event invokes handlers in order, and if one throws, the
handlers after it never run (see
[0052-one-handler-kills-the-rest](../0052-one-handler-kills-the-rest/)) - so "make
the raise safe" is only half the contract; the invocation of *other people's*
handlers is the other half.

## 🎓 Advanced Nuance

- **`?.Invoke` reads the field once.** The null-conditional operator evaluates
  `OrderPlaced` a single time into a hidden temporary, then null-checks and
  invokes that copy - which is exactly the thread-safe "capture then call" pattern
  people used to write by hand, now built in.
- **The nullable annotation is the tell.** A `field`-backed event is
  `EventHandler?` because it starts null; the `CS8602` warning on
  `OrderPlaced(this, id)` is the compiler pointing straight at this bug. Treat
  that warning as a to-do, not noise.
- **`event` restricts callers, not the declaring type.** Outside code can only
  `+=`/`-=`; the type that declares the event is the only one that can invoke it -
  so the fix lives at the raise site, and there is exactly one place per event to
  get it right.

## 🔎 How to Find It in Your Codebase

- Grep for event raises written as a plain call - `SomeEvent(this, ...)` or
  `SomeEvent(...)` - rather than `SomeEvent?.Invoke(...)`; each one throws whenever
  its invocation list is empty.
- Trust the compiler: `CS8602` on an event invocation is this bug; do not suppress
  it with `!` - fix it with `?.Invoke`.
- Symptom-side: `NullReferenceException` originating *inside* a publisher on a
  raise line, crashes that only happen when a feature/module is disabled or during
  startup before subscribers attach, and code that passes tests (which subscribe)
  but fails in environments that do not.
- Standardize on `E?.Invoke(...)` (or a `protected virtual OnX` wrapper) for every
  event raise, so "no subscribers" is always a quiet no-op.
