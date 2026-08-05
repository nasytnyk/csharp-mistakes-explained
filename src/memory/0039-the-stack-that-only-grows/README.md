---
id: "0039"
title: stackalloc inside a loop
category: memory
tags: [memory, stackalloc, StackOverflow]
rule: "never `stackalloc` inside a **loop**"
---

# #0039 - stackalloc Inside a Loop

## 💥 Symptom

The nightly batch dies. Not an exception in the log - the process is just *gone*,
exit code 134, and the last line the runtime printed is `Stack overflow.` with a
one-frame stack trace pointing at your loop. There is no recursion anywhere near
it. The `try/catch` wrapped around the job caught nothing; the `finally` that
closes the file never ran; the graceful-shutdown handler never fired. Small test
files sail through. The full run dies partway, every time, at roughly the same
row - and the row moves if you change the buffer size or the thread it runs on.

## 🔍 The Offending Code

```csharp
for (int row = 0; row < 200_000; row++)
{
    Span<byte> scratch = stackalloc byte[1024]; // 💥 not freed each iteration
    Format(row, scratch);
}
```

Someone turned a per-row `new byte[1024]` into a `stackalloc` to skip the heap
allocation. The compiler even warned - **CA2014** - but a warning ships.

## 🧠 What's Actually Going On

`stackalloc` allocates by moving the stack pointer down; the memory is reclaimed
only when the **method returns**. The language scopes the *span variable* - you
cannot name `scratch` outside the loop body - but that scoping has nothing to do
with the memory. The bytes are not released at the closing brace of the iteration;
they sit on the stack until the whole method unwinds.

So each pass through the loop bumps the stack pointer another 1 KB lower and never
lifts it back. Iteration 1 takes 1 KB, iteration 2 takes another, and after
roughly `stackSize / 1024` rows the pointer runs off the end of the stack. That is
a `StackOverflowException`, which since .NET 2.0 **cannot be caught**: the CLR
cannot safely run managed code - not your `catch`, not your `finally`, not an
unhandled-exception hook - on a stack that has no room left, so it fast-fails the
entire process on the spot.

The broken belief is "the span goes out of scope each iteration, so its memory is
freed each iteration." Scope governs the *name*; the method frame governs the
*memory*. Like the closure in
[0038-the-closure-that-held-everything](../../memory/0038-the-closure-that-held-everything/),
the lifetime you got is the one you did not write.

## ✅ The Fix

Allocate once, above the loop, and reuse the one buffer - the stack holds a single
1 KB scratch for the whole run:

```csharp
Span<byte> scratch = stackalloc byte[1024]; // hoisted: one buffer, reused
for (int row = 0; row < 200_000; row++)
{
    Format(row, scratch);
}
```

Full version in [Good.cs](Good.cs). Picking the buffer strategy:

| Approach | When it's the right call |
|---|---|
| Hoist the `stackalloc` above the loop, reuse it | The default - the buffer is a fixed, modest size and each iteration overwrites it. One allocation, no growth |
| `ArrayPool<T>.Shared.Rent(n)` (and `Return`) | The size varies per iteration or is large; keeps it off both the stack and the per-iteration heap |
| A plain `new T[n]` inside the loop | Simplicity wins and the allocation is cheap - the GC reclaims each iteration's array, so it never accumulates |

## 😈 The Even Worse Sibling

The crash is bad; the *silence around* the crash is worse. Because a stack
overflow skips every `finally`, the half-written report file is never flushed or
deleted, the open transaction is never rolled back, and the "job failed, retry"
path never runs - the process vanished before any of it. And the row it dies on
is not fixed: a worker or thread-pool thread gets a much smaller stack than the
main thread (often 256 KB - 1 MB), so wrapping the same loop in a `Task.Run` makes
it overflow *sooner*. A normal exception at least lets your cleanup run; this one
takes the whole process and leaves the mess behind. The loud crash in this exhibit
is the honest part.

## 🎓 Advanced Nuance

Why uncatchable: `StackOverflowException` is treated as a corrupted-process state.
Running a `catch` needs stack; there is none; so from .NET 2.0 on the runtime does
not even try - it terminates. `RuntimeHelpers.EnsureSufficientExecutionStack`
exists to *probe* for room before deep recursion, but it does nothing for a loop
that leaks a little each time. There is no guard you can add; you have to not
allocate in the loop.

The overflow point is environment-dependent, which is what makes it slip through
testing: the main thread's stack is set by the OS/runtime (commonly ~1 MB on
Windows, larger on Linux), thread-pool threads are smaller, and a manually created
`Thread` can be given any size. So "it processed 5,000 rows fine locally" proves
nothing about a server with a different stack and a bigger file. And CA2014 is a
*warning* by default - promote it in `.editorconfig`
(`dotnet_diagnostic.CA2014.severity = error`) so it cannot ship.

## 🔎 How to Find It in Your Codebase

- **CA2014** ("Do not use stackalloc in loops") flags this exact pattern at
  compile time - it fired on the Bad.cs here. Turn it on and set it to `error`;
  that alone closes the hole.
- Grep for `stackalloc` and check the enclosing scope: any hit inside a `for`,
  `foreach`, `while`, or `do` body is the bug, including loops the analyzer might
  miss across a helper method.
- Watch for a second stackalloc smell while you are there: `stackalloc byte[n]`
  where `n` is not a small constant. A data- or caller-controlled length can blow
  the stack in a *single* allocation - guard the size (`n <= 256 ? stackalloc :
  ArrayPool`) or do not stackalloc it at all.
- In review, treat "moved one line" diffs around `stackalloc` with suspicion:
  hoisting it in or out of a loop changes no output and no call site, so the fix
  and the bug look identical unless you already know this rule.
