---
id: "0040"
title: A Span over a List that grew
category: memory
tags: [memory, CollectionsMarshal, Span]
rule: "never keep a `Span` over a **List** that can still grow"
---

# #0040 - A Span Over a List That Grew

## 💥 Symptom

In-place updates start silently vanishing - but only once the data gets big
enough. A batch job that adjusts every item through a fast span view works on the
sample file and on every unit test, then loses writes in production. No exception,
no error, no warning: the list and the "fast view" over it simply disagree, and
the divergence begins exactly when the collection crosses some size it never hit
in testing. Read the list back and your update is not there; the write went
*somewhere*, just not where the list is looking.

## 🔍 The Offending Code

```csharp
Span<int> live = CollectionsMarshal.AsSpan(scores);   // view over the backing array
scores.Add(50);                                       // 💥 List reallocates; `live` now aliases the OLD array
for (int i = 0; i < live.Length; i++) live[i] += 100; // writes land in the abandoned buffer
```

## 🧠 What's Actually Going On

`CollectionsMarshal.AsSpan(list)` does not copy anything - it hands you a `Span<T>`
straight over the `List<T>`'s private backing array. That is the whole point: zero
allocation, direct in-place access.

But a `List<T>` does not own one array for life. When `Add` is called with
`Count == Capacity`, the list allocates a **new, larger** array, copies the
elements across, and swaps its internal pointer to the new one. The old array is
now orphaned - and your span still points at it. Writes through the span mutate
that dead buffer; reads return its stale contents. The list, now living on the new
array, never sees either. Two views of "the same data" that silently forked at the
moment of a single `Add`.

The trap is that this looks exactly like the safe thing. Enumerate a list and
mutate it and you get a loud `InvalidOperationException` - the enumerator bumps a
version and checks it, as in
[0001-modify-while-enumerating](../../collections/0001-modify-while-enumerating/).
The span path has **no such guard**. `CollectionsMarshal` is the documented "I
know what I am doing" escape hatch; its contract says items must not be added or
removed while the span is in use, and nothing enforces it. Break the contract and
there is no version check, no exception - just an alias pointing at a buffer the
list walked away from.

## ✅ The Fix

Take the span only after the list has stopped changing size, and never hold it
across an `Add`:

```csharp
scores.Add(50);                                       // finish growing first...
Span<int> live = CollectionsMarshal.AsSpan(scores);   // ...then view the final buffer
for (int i = 0; i < live.Length; i++) live[i] += 100;
```

Full version in [Good.cs](Good.cs) - the only change from Bad.cs is the order of
those two lines. Choosing the approach:

| Approach | When it's the right call |
|---|---|
| Take `AsSpan` *after* the last size change | The default - do all adds/removes, then span the settled list for the in-place pass |
| Re-take `AsSpan` after every `Add`/`Insert`/`Remove` | You must interleave growth and span work - treat the span as valid only until the next size change |
| `list.EnsureCapacity(finalCount)` before spanning | You know the final size up front - pre-size so the array never moves (still breaks if you exceed it) |
| Use `list[i]` instead of a span | Correctness over the micro-optimization - the indexer always reads the list's current array |

## 😈 The Even Worse Sibling

Two ways this gets nastier. First, it is not only lost *writes* - it is stale
*reads*. Read through the span after the grow and you get the pre-`Add` snapshot,
so any downstream total, hash, or decision runs on data that quietly diverged from
the list, and looks perfectly consistent with itself. Second, the irony for a
memory exhibit: the orphaned array cannot be collected while your span is alive, so
the "zero-allocation" optimization now pins **two** copies of the data - the dead
buffer the span holds and the live one the list grew into. The trick you reached
for to avoid an allocation just doubled your retention and corrupted your writes at
the same time.

## 🎓 Advanced Nuance

`Add` is not the only trigger - anything that reallocates moves the array:
`AddRange`, `Insert` past capacity, `EnsureCapacity` when it actually grows, even
assigning `Capacity`. And the mirror surprise: `Remove` and `Clear` do *not*
reallocate (they keep the buffer), so a span survives them by length - except
`Clear` zeroes the elements, so your span now reads all-default values over live
indices. "Only grow invalidates the span" is itself a half-truth.

This is a property of `List<T>` specifically, because it can swap its buffer. A
`Span<T>` over a plain `T[]` is safe - arrays never resize. The same
`CollectionsMarshal` caution applies to its siblings:
`GetValueRefOrNullRef`/`GetValueRefOrAddDefault` hand out a `ref` into a
`Dictionary`'s storage that is invalidated by the very next insert. The whole
class is refs and spans into live collection internals, valid only until the
collection's shape changes.

## 🔎 How to Find It in Your Codebase

- Grep for `CollectionsMarshal.AsSpan(` and check that the same list is not
  `Add`/`AddRange`/`Insert`/`Remove`/`Clear`-ed, and its `Capacity` not touched,
  while the span is still in use - especially across a method call that might do so.
- No analyzer flags this: it is the explicit low-level escape hatch, so the
  contract ("do not resize while the span lives") is entirely on you. Treat every
  `AsSpan` over a `List` as a promise not to grow it.
- Watch spans that outlive their safe window: stored in a field, returned, or
  passed to a helper that appends. The bug hides at the *unrelated* line that adds
  one more item, not at the span itself.
- The same review rule covers `CollectionsMarshal.GetValueRefOrNullRef` over a
  `Dictionary`: the `ref` is dead the instant anything inserts.
