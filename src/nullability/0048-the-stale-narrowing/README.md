---
id: "0048"
title: A stale narrowing survives a method call
category: nullability
tags: [nullability, flow-analysis, NullReferenceException]
rule: "never trust a field's null-check to survive a **method call**"
---

# #0048 - A Stale Narrowing Survives a Method Call

## 💥 Symptom

A `NullReferenceException` on a line the compiler explicitly approved. No `!`, no
warning, and a null-check for that exact field sitting two lines up. It happens in a
class the team considers proven null-safe - fully nullable-annotated, every warning
fixed - so the investigation begins by doubting the compiler, then the runtime,
then the framework, arriving last at the one thing that is actually wrong: a helper
call between the check and the use quietly set the field back to null.

## 🔍 The Offending Code

```csharp
if (_currentUser != null)                 // narrows _currentUser to non-null
{
    FinishAudit();                        // ...which sets _currentUser = null
    Use(_currentUser.ToUpperInvariant()); // 💥 NRE, and the compiler never warned
}
```

## 🧠 What's Actually Going On

Nullable flow analysis narrows `_currentUser` to non-null inside the `if`. But it
does **not** model the side effects of method calls. It cannot know what
`FinishAudit()` does to the field - so instead of pessimistically re-checking, it
optimistically assumes the narrowing still holds after the call. This is a
deliberate, documented soundness trade-off: re-invalidating every field after every
method call would drown real code in warnings, so the analyzer trusts that a field
stays as narrowed until it sees a *direct assignment in the same method body*. A
mutation hidden inside a callee is invisible to it.

So the null-check was a snapshot - the value of the field at that instant - and the
analyzer treated it as a standing promise. `FinishAudit()` reaches back and clears
the field; the promise is now false; the dereference throws. And the build stayed
green because the analysis was never built to watch a called method reassign your
state. The broken belief is that a passed null-check keeps holding; it only holds
until something the analyzer can't see changes the field. Like
[0046-null-forgiving-lies](../../nullability/0046-null-forgiving-lies/), it is a
warning-free NRE - there the guarantee is a lie you told, here it is a promise the
compiler made and could not keep.

## ✅ The Fix

Snapshot the field into a local before the work. A local cannot be reassigned by a
method call, so narrowing it is sound - the value you validated is the value you
use:

```csharp
var user = _currentUser;      // capture the validated value
if (user != null)
{
    FinishAudit();            // clears the field, but `user` is untouched
    Use(user.ToUpperInvariant());
}
```

Full version in [Good.cs](Good.cs). The options:

| Approach | When it's the right call |
|---|---|
| Copy the field to a local, narrow and use the local | The default - the local holds the value you checked; no callee can reach it |
| Re-read and re-check the field after the call | You need the field's *current* value, not the snapshot - then handle both outcomes explicitly |
| Keep the field stable for the operation | The deeper fix - don't have helpers null the field mid-method; mutate shared state at defined points, not in passing |

## 😈 The Even Worse Sibling

Replace the helper with an `await`. Now the mutation does not have to be in your
code at all: the method suspends between the `if (_currentUser != null)` and the
use, and any continuation, event handler, or second thread can null the field while
you are parked. The flow analysis - never designed to model concurrency - still
vouches for the dereference. The single-threaded version at least has its culprit,
`FinishAudit`, in the same file; the async version's culprit can be a callback in a
component you have never opened. And because it is a race, it passes every test and
only shows up under real concurrency - a warning-free NRE whose cause is not even in
the method, on shared mutable state touched without synchronization, the root of
[0003-race-on-shared-counter](../../async/0003-race-on-shared-counter/).

## 🎓 Advanced Nuance

The trade-off is specific to **fields and properties**, not locals. A local's
narrowing is sound because a local can only change through an assignment the
analyzer can see (or a `ref`/`out` pass, which it does account for). Fields are
shared mutable state it cannot follow across a call without becoming unusably noisy,
so it chooses optimism - and documents that nullable analysis is a best-effort lint,
not a proof.

You can teach the analyzer about your *own* methods with attributes -
`[MemberNotNull(nameof(_field))]` on an `Init()` that assigns it, or
`[MemberNotNullWhen(true, ...)]` on a `TryGet` - but there is no attribute for the
opposite claim, "this call *might* null the field." The default is trust, and only a
snapshot or a re-check overrides it. A property with a side-effecting getter is
worse still: the analyzer assumes two reads return the same value, and a getter that
does work can hand back non-null once and null the next time, invisibly.

## 🔎 How to Find It in Your Codebase

- Look for a field null-checked and then dereferenced with a **method call or an
  `await` in between**: `if (_field != null) { ...something...; _field.X }`. The
  gap is where the narrowing goes stale.
- Suspect cleanup, reset, dispose, logout, and audit helpers especially - anything
  whose job is to *clear* state - and any `await` sitting between a guard and a use
  of the same field.
- No analyzer flags it; this is the analyzer's own documented blind spot, so it is a
  review rule. The safe pattern is: check a field, then either use it immediately
  with nothing in between, or snapshot it to a local first.
- In review, a field dereference after a guard is only safe if *nothing* between them
  could reassign it - and "nothing" must include every method called and every
  suspension point.
