---
id: "0036"
title: Eliding await inside a using block
category: async
tags: [async, using, ObjectDisposedException]
rule: "never elide `await` inside a **using** or try/finally block"
---

# #0036 - Eliding await Inside a using Block

## 💥 Symptom

Data access starts throwing `ObjectDisposedException: Cannot access a disposed
object` - a connection, a `DbContext`, an `HttpClient` - from a method that looks
obviously correct. It *has* a `using`. It passed review; someone even tidied it
up by deleting a "redundant" `await`. Worse, it is intermittent: it only fails
when the call actually suspends - a cache miss, a slow query, real network
latency - so it sails through tests and fast local runs and only shows up under
load, pointing at a line that was "just cleaned up".

## 🔍 The Offending Code

```csharp
Task<string> GetCustomerName()
{
    using var conn = FakeConnection.Open();
    return QueryAsync(conn, gate); // 💥 no await: 'using' disposes conn the instant we return
}
```

The `await` was removed on purpose - `return await QueryAsync(...)` shortened to
`return QueryAsync(...)`, a real optimization that skips one state machine.

## 🧠 What's Actually Going On

`using` is `try/finally` in disguise: the compiler rewrites it so the connection
is disposed in a `finally` that runs when the **method** exits. In the awaited
version, `return await QueryAsync(...)`, the method does not exit until the query
completes - the `await` suspends it, holds the `finally` open, and only then
disposes.

Drop the `await` and the method stops being `async` at all. It calls
`QueryAsync`, which runs synchronously up to its own first `await`, hands back an
*incomplete* `Task`, and returns it. `GetCustomerName` now exits immediately - so
its `finally` fires and disposes the connection right there, while the returned
task is still mid-flight. Moments later the round-trip completes, `QueryAsync`
resumes, reaches for the connection it was handed - and it has been dead since
the instant the helper returned.

The broken mental model is that `return await x` and `return x` are the same
because they hand back the same result. They are the same *only* when nothing
after the return point matters. A `using`, a `catch`, or a `finally` is exactly
"something after the return point": the `await` was load-bearing, holding the
method's scope open across the suspension, and eliding it quietly tore that scope
down early.

## ✅ The Fix

Keep the `await` whenever the method has a scope - `using`, `try`, `catch`,
`finally` - that must outlive the task:

```csharp
async Task<string> GetCustomerName()
{
    using var conn = FakeConnection.Open();
    return await QueryAsync(conn, gate); // await keeps the 'using' scope alive to here
}
```

Full version in [Good.cs](Good.cs). When each spelling is right:

| Shape | When it's the right call |
|---|---|
| `return await Work(...)` inside `using` / `try` / `catch` / `finally` | Required. The scope must stay alive until the task finishes, and only `await` does that |
| `return Work(...)` (bare, no `await`) in a plain pass-through method | Correct, and marginally faster - it skips a state machine - **when nothing follows the return**: no disposal, no catch, no finally |
| `await using` over an `IAsyncDisposable` | Same rule with async cleanup - still awaited, never elided |

Eliding the await is a genuine optimization; it is just only safe in the flat
pass-through case. It is the mirror image of the other way to break "async all
the way", blocking the chain with `.Result` in
[0035-the-pool-that-ate-itself](../../async/0035-the-pool-that-ate-itself/): one
removes an await that guarded a scope, the other refuses to await at all.

## 😈 The Even Worse Sibling

Swap the `using` for a `try/catch`. Now the elided `return` hands the task back
before the method's `catch` is ever in scope, so when the work faults, your
`catch` - the one written to log it, retry it, translate it - **never runs**. The
exception surfaces at whoever awaits the caller, wearing the callee's stack but
having sailed straight past the handler that was supposed to own it. The `using`
version at least crashes loudly with an `ObjectDisposedException`; the
`try/catch` version fails silently in the sense that matters most - your error
handling simply did not execute, and the log line you would grep for was never
written. The crash in this exhibit is the *lucky* outcome.

## 🎓 Advanced Nuance

The advice to "remove the redundant await" is real and everywhere: performance
posts recommend it, and ReSharper/Rider ship a "Redundant `await`" suggestion.
It is correct - `await` does add a state machine - and it is dangerous precisely
because it is *usually* right. The good tooling is scope-aware and leaves the
await alone inside a `using`/`try`; a human doing the cleanup by hand is not, and
the diff looks like pure tidying.

The real trigger is the `finally`, and `using` is only its most common face. Any
of these keeps the await mandatory: `try/finally`, `try/catch`, a `using`
declaration, an `await using`, or a `lock`-like scope. If your method's body ends
in `return await`, check what encloses it before you "simplify": if the answer is
"nothing", eliding is free; if it is a scope, the await is holding the building
up.

## 🔎 How to Find It in Your Codebase

- Grep for a bare returned task inside a scoped method: a `return
  \w+Async(` with no `await`, in a method that also contains `using`, `try`, or
  `catch`. Regex `return\s+\w+Async\(` finds the candidates; the enclosing scope
  decides guilt.
- The strongest structural tell: a method that returns `Task`/`Task<T>` but is
  **not** marked `async` and contains a `using` or `try`. That combination is the
  bug's natural habitat.
- Treat any "remove redundant await" quick-fix or review suggestion as
  scope-sensitive. Inside a `using`/`try`/`catch`/`finally`, decline it; in a flat
  pass-through, take it. The tooling that offers it does not always know which one
  you are in.
- Watch the resource helpers hardest - anything wrapping a `DbContext`,
  connection, transaction, `Stream`, or `HttpResponseMessage` - because there the
  disposed object is exactly what the returned task still needs.
