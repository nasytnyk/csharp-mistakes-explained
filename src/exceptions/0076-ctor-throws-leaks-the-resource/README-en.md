---
id: "0076"
title: a throwing constructor leaks the resource
category: exceptions
tags: [exceptions, IDisposable, constructors]
rule: "never throw in a constructor after acquiring - `using` never runs, so **release** what you took"
---

# #0076 - A Throwing Constructor Leaks the Resource

## 💥 Symptom

A connection pool (or file-handle pool, or semaphore) drains under load and no
one can find the leak. Every `using` is in place, every `Dispose` looks correct -
yet slots go out and never come back. It happens only on the failure path: the
constructor of a resource-owning object acquires its slot, then hits a bad
argument or a failed connect and throws, and the `using` you wrapped it in
disposes nothing.

## 🔍 The Offending Code

```csharp
using var conn = new PooledConnection(host); // 💥 ctor takes a slot, then throws - conn is never assigned

sealed class PooledConnection : IDisposable
{
    public PooledConnection(string host)
    {
        Pool.Take();                       // acquire
        if (string.IsNullOrEmpty(host))    // then validate -> throws with a slot already taken
            throw new ArgumentException("host is required");
    }
    public void Dispose() => Pool.Return();
}
```

## 🧠 What's Actually Going On

`using var x = new T(...)` disposes `x` at the end of its scope - but only if `x`
was ever assigned. When a constructor throws, the `new` expression never
completes, so the variable is never assigned, and there is nothing for `using`
(or a later `Dispose`) to clean up. Any resource the constructor already acquired
before it threw - a pool slot, a file handle, a lock, a native buffer - is now
held by an object that does not exist, with no reference anywhere and no Dispose
that will ever run. `using` protected you against every failure *after*
construction and none *during* it.

The broken belief is "`using` guarantees Dispose, so the resource is safe." It
guarantees Dispose only for a *successfully constructed* object. A constructor
that acquires a resource and then does anything that can throw - validate an
argument, open a socket, read config - has a window where the resource is live
but the object is not, and an exception in that window leaks it silently, only on
the error path your happy-path tests never exercise.

## ✅ The Fix

Make the constructor exception-safe: if anything after acquiring can throw,
release what you took before letting the exception out.

```csharp
public PooledConnection(string host)
{
    Pool.Take();
    try
    {
        if (string.IsNullOrEmpty(host))
            throw new ArgumentException("host is required");
        // open the socket, read config, whatever else can throw
    }
    catch
    {
        Dispose();   // release the slot, then rethrow
        throw;
    }
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| `try { ... } catch { Dispose(); throw; }` in the ctor | The constructor must acquire and then do throwing work - guard it so any failure releases what was taken before propagating. |
| Acquire last / validate first | The only thing that can throw is validation - check arguments *before* you take the resource, so a throw happens with nothing acquired. |
| A static factory method | Move acquisition out of the constructor - `Connection.Open(host)` acquires and, on failure, releases and throws; the ctor just stores an already-acquired handle. |
| Don't own the resource | Take the resource as a constructor *parameter* (dependency injection) - the caller owns its lifetime, and a throw in your ctor leaks nothing you acquired. |

## 😈 The Even Worse Sibling

The pool slot at least has a `Dispose` you failed to reach; an *unmanaged*
resource acquired in a throwing constructor has no safety net at all. If the
constructor allocates a native handle (a file, a socket, `Marshal.AllocHGlobal`)
and then throws, the finalizer is the only thing that could free it - and an
object whose constructor threw is **not registered for finalization**, so even
the finalizer never runs. The handle leaks until the process exits. The
field-initializer version hides the acquire entirely: `private readonly Stream
_s = File.OpenRead(path);` runs *before* the constructor body, so if a later
field initializer or the body throws, `_s` is open, unreferenced, and unclosed -
a leak with no visible acquire in the constructor at all.

## 🎓 Advanced Nuance

- **A failed constructor suppresses finalization.** The runtime does not call the
  finalizer of an object whose constructor threw - it was never fully
  constructed - so the finalizer-based cleanup people rely on as a backstop does
  not fire here. The `try/catch`-release *in the constructor* is the backstop.
- **`using` binds Dispose to a *variable*, not to the `new`.** The leak is
  precisely that the variable was never assigned. Moving acquisition out (a
  factory that returns an already-built, resource-holding object) restores the
  guarantee: the caller's `using` then wraps a fully constructed object.
- **Object initializers extend the window past the constructor.** `new
  Conn(host) { Timeout = Compute() }` runs `Compute()` *after* the constructor
  returns but *before* the variable is assigned - if it throws, a
  successfully-constructed `Conn` is leaked, because `using` / assignment never
  happened. The safe object was built and then dropped on the floor.

## 🔎 How to Find It in Your Codebase

- Grep for constructors that acquire (`Open`, `Connect`, `Rent`, `Acquire`, `new
  SemaphoreSlim`, `AllocHGlobal`, `File.Open*`) and then do anything that can
  throw - validation, another acquire, I/O - without a `try/catch` that releases.
- Look at `IDisposable` types with real work in the constructor, and at field
  initializers that open resources (`= File.OpenRead(...)`, `= new HttpClient()`)
  on a type whose construction can fail elsewhere.
- Symptom-side: pool / handle / semaphore leaks that grow only under error load;
  a resource count that drifts up in exactly the runs where inputs were invalid;
  leaks that vanish when the failing input is removed.
- Prefer acquiring through a factory (or taking the resource as a parameter) over
  acquiring inside a constructor that can throw; when you must acquire there,
  wrap the post-acquire work in `try { ... } catch { Dispose(); throw; }`.
