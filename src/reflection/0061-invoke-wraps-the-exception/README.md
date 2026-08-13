---
id: "0061"
title: MethodInfo.Invoke wraps your exception
category: reflection
tags: [reflection, exceptions, MethodInfo]
rule: "never `catch` exception around `MethodInfo.Invoke` - it is **wrapped** in `TargetInvocationException`"
---

# #0061 - MethodInfo.Invoke Wraps Your Exception

## 💥 Symptom

A handler that rejects bad input with a clean 400 everywhere else crashes as a 500 - but only
when it's called through reflection. The `catch (ValidationException)` that works on the direct
path silently doesn't fire behind a `MethodInfo.Invoke`; the domain exception the handler threw
arrives as something else entirely, the specific catch is skipped, and the error falls through
to the generic "unexpected error" path. The logs blame a `TargetInvocationException` nobody
wrote.

## 🔍 The Offending Code

```csharp
try
{
    method.Invoke(handler, new object[] { order }); // handler throws ValidationException
}
catch (ValidationException ex) // 💥 never runs - Invoke wrapped it in TargetInvocationException
{
    return Reject(ex.Message);
}
```

## 🧠 What's Actually Going On

When a method invoked through `MethodInfo.Invoke` throws, reflection catches that exception and
re-throws it as the `InnerException` of a `TargetInvocationException`. So the exception that
reaches your `catch` is not the `ValidationException` the handler threw - it is a
`TargetInvocationException` wrapping it. `catch (ValidationException)` doesn't match, the wrapper
falls through to whatever broader handler exists (`catch (Exception)`, or nothing), and the
domain error is mishandled as a generic crash. `catch`, `is`, exception filters - anything keyed
to the real type - all miss, because the real type is one layer down in `.InnerException`.

The broken belief is "the exception I catch is the exception that was thrown." Through `Invoke`
it isn't: reflection interposes a wrapper so a failure *inside* the method is distinguishable
from a failure to *find or bind* it - and the price is that every exception from the target
changes type on the way out.

## ✅ The Fix

Tell `Invoke` not to wrap, with the `BindingFlags.DoNotWrapExceptions` overload - the target's
exception then propagates with its real type and its original stack trace intact:

```csharp
method.Invoke(handler, BindingFlags.DoNotWrapExceptions, binder: null, new object[] { order }, culture: null);
// now catch (ValidationException) matches
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `BindingFlags.DoNotWrapExceptions` | You control the `Invoke` call and want the natural exception to propagate - cleanest, keeps the real type and stack (.NET Core 2.1+). |
| Catch `TargetInvocationException`, rethrow inner with `ExceptionDispatchInfo` | A layer you don't own does the `Invoke` - `ExceptionDispatchInfo.Throw(tie.InnerException)` re-raises the real one without losing its stack. |
| `catch (TargetInvocationException tie) when (tie.InnerException is ValidationException v)` | You must handle the domain type but can't change the call - filter on the inner and read `v`. |
| Don't invoke by reflection at all | A delegate, an interface, or a source-generated dispatcher calls the method directly - the exception is never wrapped in the first place. |

## 😈 The Even Worse Sibling

The instinct that "fixes" it plants a subtler bug. Catching the wrapper and re-throwing its inner
with a bare `throw tie.InnerException;` does restore the type - and *erases the stack trace*,
resetting it to that line so the exception now appears to originate in your dispatcher, not in the
handler that actually failed (the same amnesia as
[0005-throw-ex-stack-amnesia](../../exceptions/0005-throw-ex-stack-amnesia/)); use
`ExceptionDispatchInfo.Capture(tie.InnerException).Throw()` to keep the original trace. And there
is a quieter cousin: `TargetInvocationException` only wraps exceptions from *inside* the method.
The reflection call itself can still throw *un*wrapped - `ArgumentException` for a bad parameter,
`TargetParameterCountException`, `TargetException` for the wrong receiver - so a
`catch (TargetInvocationException)` that assumes "all Invoke failures look alike" misses the ones
that came from binding rather than execution.

## 🎓 Advanced Nuance

- **`DoNotWrapExceptions` is the modern behavior you have to ask for.** It arrived in .NET Core
  2.1; before it, unwrapping was the only option. On a current runtime, prefer the flag and
  delete the try/catch/rethrow dance.
- **The wrapper exists to disambiguate "the method failed" from "I couldn't call it."** A
  `TargetInvocationException` means your code ran and threw; a `TargetParameterCountException` or
  `ArgumentException` from `Invoke` means it never ran. Catching `Exception` around `Invoke`
  throws that signal away.
- **Delegates called normally don't wrap.** `Delegate.DynamicInvoke` *does* wrap, same as
  `Invoke`, but a compiled delegate called directly - `((Action)d)()`, or a `Func<>` built with
  `CreateDelegate` - propagates the real exception. If you invoke the same method often, binding a
  delegate once is faster *and* wrap-free.

## 🔎 How to Find It in Your Codebase

- Grep for `.Invoke(` on a `MethodInfo` / `MethodBase` (and `Delegate.DynamicInvoke`) and check
  the surrounding `catch` blocks - any `catch (SomeSpecificException)` around a reflective invoke
  is suspect unless `DoNotWrapExceptions` is set.
- Symptom-side: a domain exception handled correctly on the direct path but turning into a generic
  500 / "unexpected error" only when routed through a reflective dispatcher, mediator, or plugin
  host; logs full of `TargetInvocationException`.
- Grep for `catch (TargetInvocationException)` followed by `throw ...InnerException` - that rethrow
  likely drops the stack trace; switch it to `ExceptionDispatchInfo`.
- Prefer `DoNotWrapExceptions`, or a delegate/interface call over `Invoke`, wherever a specific
  exception type needs to survive the call.
