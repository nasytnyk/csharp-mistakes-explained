---
id: "0044"
title: A boxed enum is not its number
category: boxing
tags: [boxing, enum, equality]
rule: "never treat a boxed **enum** as equal to its underlying number"
---

# #0044 - A Boxed Enum Is Not Its Number

## 💥 Symptom

A dispatch table silently routes to the wrong branch. A handler is registered for
`Status.Approved`, an approved order comes in, and it falls through to the default
"unknown status" path anyway. Or an equality check reports two values different
that are "obviously the same". The tell: it only happens in production. In tests,
the status is the enum everywhere and the lookup hits; in production the status
arrives from JSON or config as the *number* 1, and the same table cannot find it -
on a value that `(int)` says is identical.

## 🔍 The Offending Code

```csharp
var handlers = new Dictionary<object, string> { [Status.Approved] = "send-receipt" };
object statusFromWire = 1;                 // JSON/config delivered Approved as the number 1

handlers.TryGetValue(statusFromWire, ...); // 💥 miss: boxed int 1 is not Equal to boxed Status.Approved
```

## 🧠 What's Actually Going On

A boxed enum remembers it is an enum. `(object)Status.Approved` carries the runtime
type `Status`, not `Int32`. And `object.Equals` between two boxes starts by
requiring the runtime types to match - so a boxed `Status` and a boxed `int` are
never equal, in either direction, even when both hold the value 1.

The genuinely surprising part is *where* the dictionary lookup fails. You might
expect them to hash to different buckets - they do not. An enum's `GetHashCode` is
its underlying value's hash, so `Status.Approved` and `1` hash to the *same* bucket.
The dictionary walks straight to the right bucket, finds the enum key sitting there,
calls `Equals` to confirm - and `Equals` says no, because the types differ. It is
not a hash miss; it is an equality miss inside the correct bucket.

Meanwhile `(int)(object)Status.Approved` unboxes cleanly to 1, because unboxing is
the one operation that bridges an enum and its underlying type - the exact leniency
that [0041-unbox-must-match-exact-type](../../boxing/0041-unbox-must-match-exact-type/)
documents. So the same pair is *interchangeable by cast and disjoint by Equals*:
`(int)a == (int)b` is true, `a.Equals(b)` is false. The broken belief is that a
boxed enum and its number are the same object-typed value. The cast agrees; nothing
else does.

## ✅ The Fix

Do not let an enum and its number meet as `object`. Key the table by the enum type,
and convert the untyped wire value into that enum once, at the boundary:

```csharp
var handlers = new Dictionary<Status, string> { [Status.Approved] = "send-receipt" };
Status status = (Status)(int)statusFromWire; // parse the number into the domain enum at the edge
handlers.TryGetValue(status, ...);           // one type on both sides - it hits
```

Full version in [Good.cs](Good.cs). Choosing the approach:

| Approach | When it's the right call |
|---|---|
| `Dictionary<TEnum, V>` + convert wire value to the enum at the edge | The default - parse JSON/config/DB numbers into the domain enum once, then every comparison is one type |
| Normalize both sides before comparing | You must keep `object`-typed values - unbox both to `int` (or both to the enum) before `Equals`/lookup |
| `Enum.IsDefined` / `Enum.Parse` at the boundary | Untyped input crossing in - validate and convert to the enum, rejecting junk numbers up front |

## 😈 The Even Worse Sibling

The cast working is the trap's alibi. A reviewer who *is* suspicious tests the
obvious thing - `(int)key == 1` - watches it pass, and concludes the boxed forms are
interchangeable. But `(int)` is the single operation in the language that treats an
enum as its number; every `Equals`, every `Dictionary`/`HashSet`/`Contains`, every
`switch` over the boxed value disagrees with the one check they ran. So the review
"proves" safety with the exact operation that behaves unlike all the others, and
signs off. And nothing throws: the lookup just returns the default, so an approved
order is quietly handled as "unknown" in production while every test - written in
enums - stays green.

## 🎓 Advanced Nuance

This is the mirror image of unboxing's enum exception. Unboxing is uniquely lenient
about enum-versus-underlying, and *nothing else is*: not `Equals`, not `GetType`
dispatch, not dictionary keys, not `is`. The one forgiving operation is precisely the
one people generalize from, which is what makes the rest feel like it must be a bug.

Enum identity includes the enum *type*, not just the value: two boxed enums of
different types with the same underlying number are also unequal -
`((object)Color.Red).Equals((object)Size.Small)` is `false` even though both are 0.
And note the value-equality here is already generous compared to `==`: between the
boxed forms `==` is reference inequality anyway
([0042-boxed-values-are-equal-not-same](../../boxing/0042-boxed-values-are-equal-not-same/)),
so both operators say "different", for two different reasons - `.Equals` because of
the type, `==` because of the reference.

## 🔎 How to Find It in Your Codebase

- Grep for `Dictionary<object,`, `HashSet<object>`, and `object`-typed `.Equals` /
  `.Contains` where an enum and its number could both arrive - code that uses the
  enum, a JSON/config/DB layer that uses the integer. That mix is the bug's habitat.
- Convert untyped input to the domain enum at the boundary (`(TEnum)`, `Enum.Parse`,
  `Enum.IsDefined`) so the two never meet as `object`. Prefer typed keys over
  `object` keys wherever enums are involved.
- No analyzer flags it - the code is valid and only misses at runtime on the numeric
  path. In review, distrust any `(int)key == n` "proof" that the boxed forms match;
  it is the one operation that lies about this. Test the actual `.Equals` or
  dictionary path, which is what production runs.
