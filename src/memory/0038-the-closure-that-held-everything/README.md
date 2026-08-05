---
id: "0038"
title: A closure that held the whole scope
category: memory
tags: [memory, closures, GC]
rule: "never let a long-lived **closure** share a scope with a large object"
---

# #0038 - A Closure That Held the Whole Scope

## 💥 Symptom

A long-running service leaks memory, and the dump makes no sense. It shows big
buffers still alive - upload payloads, report data, whole request graphs - but
the retained-by path does not lead to any variable you can find in your code. It
ends in a compiler-generated type with an unpronounceable name like
`<>c__DisplayClass4_0`, held by some small, innocent callback that does not even
mention the buffer. The callback captured an `int`. It is somehow keeping eight
megabytes alive. Restart clears it; it climbs again, one kept callback at a time.

## 🔍 The Offending Code

```csharp
byte[] fileBytes = new byte[8 * 1024 * 1024]; // the upload payload
Action validate = () => Verify(fileBytes);    // one lambda captures the big buffer
validate();

// A different lambda, kept alive, capturing only the id:
pending.Add(() => Log(uploadId));             // 💥 same scope -> same closure -> roots fileBytes
```

## 🧠 What's Actually Going On

The C# compiler does not create one closure object per lambda. It creates **one
per scope**, and every variable captured by *any* lambda in that scope becomes a
field on that single object (a "display class"). Two lambdas declared in the same
scope share one instance of it.

So `validate` captures `fileBytes` and the completion callback captures
`uploadId`, and both end up as fields on the *same* display-class object. The
completion callback holds a reference to that object - it has to, to reach
`uploadId` - and through it, transitively, to `fileBytes`. Put the callback
somewhere long-lived (a `pending` list, an event, a cache) and you have rooted
the entire display class, including the 8 MB the callback never reads.

The GC is not wrong: the buffer *is* reachable. The reference just does not exist
anywhere in your source - it lives on a generated field, on a generated type,
reached from a delegate that looks like it only cares about an integer. The
broken belief is "a closure holds the variables I used". It holds the variables
*its whole scope* captured. This is the same capture machinery behind
[0006-closure-over-loop-variable](../../linq/0006-closure-over-loop-variable/),
turned from a wrong value into a retained megabyte.

## ✅ The Fix

Give the big object its own scope, so its captures land on a display class that
dies when the scope does - separate from the one the kept callback pins:

```csharp
WeakReference bufferRef;
{
    byte[] fileBytes = new byte[8 * 1024 * 1024];
    Action validate = () => Verify(fileBytes); // captured in THIS block's closure
    validate();
    bufferRef = new WeakReference(fileBytes);
} // the block's closure is now unreachable

pending.Add(() => Log(uploadId)); // its own scope, its own closure - no fileBytes
```

Full version in [Good.cs](Good.cs). The options, cheapest first:

| Approach | When it's the right call |
|---|---|
| Make the kept lambda `static` (capture nothing; pass state as arguments) | The callback needs only values you can pass in - no capture means no shared closure at all |
| Move the big work into its own method | A separate method is a separate scope by definition; the buffer cannot outlive the call |
| Wrap the big object in its own `{ }` block, as above | You must keep it inline but want its closure to die early - split the scope from the kept lambda's |
| Null the captured local after use | Last resort - it clears the field on the shared display class, but it is fragile and easy to reintroduce |

## 😈 The Even Worse Sibling

Here the callback sat in a `List`, so the buffer dies when the list does. Make it
an **event handler** instead - `orderService.Completed += () => Log(uploadId)` -
and the buffer now lives as long as the *event source*, which
[0010-immortal-subscriber](../../events/0010-immortal-subscriber/) shows can be
forever. Worse still, the display class holds **everything** the scope captured,
not just one buffer: a sibling lambda that grabbed a `DbContext`, a `FileStream`,
or the whole request object pins all of it through the same one small handler.
The larger the method, the more a single kept lambda silently retains - and none
of it appears at the capture site. We had to force a GC to catch it; production
just accumulates it until the restart.

## 🎓 Advanced Nuance

Splitting scopes only helps if the kept lambda and the big object are not
*co-captured*. The compiler chains display classes: if your inner lambda also
touches an outer-scope variable, the inner display class gains a reference to the
outer one, stitching the two lifetimes back together. The reliable cure is
removing the capture, which is why `static` lambdas (C# 9+) are the strongest fix
- the compiler *errors* if a `static` lambda captures anything, turning "I didn't
mean to capture that" into a build failure instead of a dump investigation.

The layout is a Roslyn implementation detail, not a language guarantee, but the
consequence is inherent: a captured variable lives exactly as long as the longest
-lived delegate over its scope. That is not going to change, because it is the
only way closures can work.

## 🔎 How to Find It in Your Codebase

- In a memory dump, sort by retained size and look for `<>c__DisplayClass*` types
  holding large objects; the GC root above them is a delegate
  (`Action`/`Func`/`EventHandler`) that captured something small. That mismatch -
  tiny apparent purpose, huge retention - is the signature.
- In code, flag any lambda that is **stored, subscribed, or queued** and is
  declared in a method that also allocates or processes large data with its own
  lambda. Co-located lambdas share a closure.
- Roslyn ships no rule for over-capture, but heap-allocation tooling surfaces the
  captures: ReSharper/Rider's allocation hints and the older
  ClrHeapAllocationAnalyzer mark every closure allocation - they flag the
  allocation, and the scope tells you what rode along.
- Prefer `static` lambdas by default and let the compiler prove no capture; reach
  for an instance capture only when you mean it.
