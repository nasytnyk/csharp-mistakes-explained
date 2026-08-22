---
id: "0087"
title: readonly array isn't readonly
category: collections
tags: [collections, readonly, immutability]
rule: "never trust `readonly` to freeze an array - it guards the **reference**, not the elements"
---

# #0087 - readonly Array Isn't readonly

## 💥 Symptom

A shared table of defaults - retry delays, tax rates, allowed statuses - is
declared `static readonly`, so it reads as immutable and untouchable. Then one
request "adjusts" an entry for its own use, and every *other* request quietly
starts seeing the adjusted value. Nothing reassigned the field, no exception
fired, and the `readonly` keyword is sitting right there - yet the global defaults
have been rewritten under everyone.

## 🔍 The Offending Code

```csharp
static class Config
{
    public static readonly int[] RetryDelays = { 1, 2, 4 }; // "immutable" defaults
}

Config.RetryDelays[0] = 30; // 💥 compiles - readonly guards the reference, not the elements
```

## 🧠 What's Actually Going On

`readonly` on a field means the *field* cannot be reassigned after construction -
`Config.RetryDelays = somethingElse` is a compile error. It says nothing about the
object the field points to. An array is a mutable reference type, so
`RetryDelays[0] = 30` does not touch the field at all; it reaches through the
(unchanged) reference and writes into the array's storage. The one array instance
is shared by every reader of `Config.RetryDelays`, so a single element assignment
edits the defaults process-wide.

The broken belief is "`readonly` makes it immutable." `readonly` freezes exactly
one thing - the reference held in the field - and freezes it against reassignment,
not against mutation of what it references. For a value type (`readonly int Max`)
that happens to mean fully immutable, which is where the confidence comes from; but
for any reference type - array, `List<T>`, a mutable class - `readonly` locks the
door and leaves every window open. A `public static readonly` array is therefore
one of the most deceptively shared mutable globals in the language: it looks like a
constant and behaves like a public field anyone can rewrite.

## ✅ The Fix

Expose something that has no in-place setter, so element assignment does not even
compile and a "tweak" must build its own copy.

```csharp
using System.Collections.Immutable;

static class Config
{
    public static readonly ImmutableArray<int> RetryDelays = ImmutableArray.Create(1, 2, 4);
}

ImmutableArray<int> burst = Config.RetryDelays.SetItem(0, 30); // a new array; the shared one is untouched
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| `ImmutableArray<T>` / `ImmutableList<T>` | Shared defaults that must never change in place - assignment does not compile, and `SetItem`/`Add` return a new instance so a caller's tweak is its own copy. |
| `ReadOnlyCollection<T>` / `IReadOnlyList<T>` | You keep a private mutable list and expose a read-only *view* - callers cannot `Add`/set through the view (though you can still mutate the backing list intentionally). |
| Hand out a copy | Callers legitimately need to mutate their result - return `array.ToArray()` / `list.ToList()` so each gets a private copy and the source is safe. |
| A method, not a field | The values are computed or per-call - a `GetRetryDelays()` that returns a fresh array each time removes the shared instance entirely. |

## 😈 The Even Worse Sibling

An `int[]` at least shows the mutation at the element you wrote. The same
`readonly`-is-not-deep trap hides more quietly one level in: a `readonly` field
holding a *mutable object* (`readonly Settings Config` with settable properties, or
`readonly List<Order> Pending`) lets callers change the object's state - `Config.Timeout =
0`, `Pending.Clear()` - while the field stays `readonly` and the compiler stays
silent. And `const` does not save you either, because you cannot declare a `const`
array or object at all - only compile-time primitives - so `static readonly` is the
tool people reach for precisely where it gives the least protection. The mirror bug
is exposing that same shared array through a *property getter* that returns it
directly, so even a type that looks encapsulated leaks a writable handle to its
internal state.

## 🎓 Advanced Nuance

- **`readonly` is shallow by definition.** It constrains the field's binding, not
  the referenced object's contents - there is no transitive/deep-`readonly` in C#.
  Depth comes only from the *type* being immutable (`ImmutableArray<T>`, a
  `readonly record struct`, a class with no setters), not from the modifier.
- **`ImmutableArray<T>` is a struct wrapping the array.** Its immutability is real
  because it exposes no mutators and copies on change - but note a *default*
  `ImmutableArray<T>` (never initialized) is `default`, and touching it throws
  `NullReferenceException`; initialize it (as the field does) rather than leaving it
  `default`.
- **`readonly` still helps against one real bug.** It does prevent accidental
  reassignment of the field (swapping the whole array for another), which is worth
  having - it just is not, and never was, a statement about the elements.

## 🔎 How to Find It in Your Codebase

- Grep for `static readonly` (and `readonly`) fields whose type is an array,
  `List<T>`, `Dictionary<,>`, or any mutable class - each is a shared object the
  `readonly` does not protect from mutation.
- Look for element/`Add`/`Clear`/property writes through such fields, especially in
  request-scoped or per-item code paths "adjusting" a shared default.
- Symptom-side: a global default that drifts over time, values that differ between
  requests with no code that reassigns them, "someone changed the config" bugs that
  reproduce only after a specific earlier operation ran.
- Expose shared, must-not-change data as `ImmutableArray<T>`/`IReadOnlyList<T>`, or
  hand out copies; reserve `readonly` on a mutable reference type for cases where
  in-place mutation is genuinely intended.
