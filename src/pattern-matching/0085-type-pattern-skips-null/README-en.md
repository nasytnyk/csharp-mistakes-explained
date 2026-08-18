---
id: "0085"
title: type pattern skips null
category: pattern-matching
tags: [pattern-matching, null, switch]
rule: "never rely on a type pattern to catch null - `case T` **skips null**; add a `null` arm"
---

# #0085 - Type Pattern Skips Null

## 💥 Symptom

A `switch` that "handles every type it could get" quietly mishandles exactly one
input: `null`. Strings render, numbers render, and then a row with an absent
optional field either crashes the job or exports a foreign-type marker where an
empty cell belongs. The `string` arm is right there - but the missing value never
reached it.

## 🔍 The Offending Code

```csharp
string Format(object? value) => value switch
{
    string s => s,
    int n    => n.ToString(),
    _        => "<unsupported>", // 💥 null matches no type pattern and lands here
};
```

## 🧠 What's Actually Going On

A type pattern - `string s`, `case string:`, `is int n` - tests whether the value
*is an instance of* that type, and `null` is an instance of no type. `null is
string` is `false`, `null is object` is even `false`; the runtime type test that
every type pattern performs simply cannot match a value that has no object at all.
So a null value fails every typed arm in the switch and falls through to the
discard arm `_`, which was written to mean "some type I did not anticipate" - not
"the value is absent."

The broken belief is "`string s` handles strings, and a null string is still a
string." At the type-system level a `string?` variable can hold null, but a
pattern match is a runtime instance check, and at runtime null is the absence of
an instance, not a string with no characters. The default arm then does whatever
"unexpected type" is supposed to do - throw, log an error, emit a placeholder,
reject the record - and applies it to what is really an ordinary empty field. The
compiler does not warn, because the switch *is* exhaustive: `_` catches null, just
into the wrong branch.

## ✅ The Fix

Match `null` explicitly with the constant `null` pattern, and put it before the
typed arms so the absent case is classified on purpose.

```csharp
string Format(object? value) => value switch
{
    null     => "",           // an absent field renders blank
    string s => s,
    int n    => n.ToString(),
    _        => "<unsupported>",
};
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| A `null` arm | The direct fix - `null => ...` (a constant pattern, the only thing that matches null) gives the absent value its own defined outcome instead of the default. |
| `null or string s` | Null should behave like the string case - combine them, e.g. `null or "" => "empty"`, so absent and empty text share one branch on purpose. |
| Guard at the boundary | The value should never be null here - reject or default it where it enters (a required column, `?? ""`), so the switch only ever sees real instances. |
| Reserve `_` for true unknowns | Keep the discard arm meaning "genuinely unexpected type" - once `null` has its own arm, a hit on `_` is a real signal (log it, alert) rather than a null in disguise. |

## 😈 The Even Worse Sibling

Here `null` at least lands in a visible default arm; the quieter cousin is a
*positional* or *property* pattern that silently rejects null the same way and has
no default to catch it. `case Person("Ada", _):` does not match a null `Person` -
the deconstruction cannot run on null - and `case { Age: > 18 }:` does not match a
null reference either, because there is no object to read `.Age` from. In a
`switch` statement with no `default`, a null just falls out the bottom and
executes nothing, so the "handle this shape" block is silently skipped for exactly
the input most likely to be a bug. And the mirror trap: `case string { Length: > 0
}` skips both null *and* the empty string, so two different "no real text" inputs
take the fallthrough for two different reasons.

## 🎓 Advanced Nuance

- **`null` is the only pattern that matches null.** The constant pattern `null`
  (and `not null`) is the sole way to test for it; every type pattern, positional
  pattern, and property pattern performs a runtime instance check that null fails.
  `var x` also "matches" null - but only because `var` matches everything and does
  no type test at all.
- **A switch with `_` is exhaustive, so no warning fires.** The compiler's
  exhaustiveness check is satisfied the moment `_` is present - it does not know
  you meant `_` for foreign types rather than null. Remove the `_` and the
  compiler will flag the missing null handling (CS8509) - a reason to prefer
  explicit arms over a catch-all where correctness matters.
- **`Nullable<T>` value types match their underlying arm, not a null one.** `int?`
  holding null still fails `case int n` (there is no boxed value), while `int?`
  holding `5` matches `case int n` with `n == 5` - so the same "skips null" rule
  applies, and a null `int?` also needs its own `null` arm.

## 🔎 How to Find It in Your Codebase

- Grep for `switch` expressions and statements over `object`, `object?`, or
  nullable-reference inputs whose arms are all type/property/positional patterns
  with a `_` (or no) default - ask what the null input does, and whether that
  matches intent.
- Look at formatters, serializers, mappers, and dispatchers fed by data readers,
  deserializers, or reflection, where an absent field arrives as null and should
  mean "empty," not "unknown type."
- Symptom-side: a placeholder or error marker showing up in output for records
  with a missing optional field, exports that crash on the first null, "unsupported
  type" logs whose value is actually just absent.
- Add an explicit `null` (or `null or ...`) arm, keep `_` for genuinely
  unexpected types, or drop `_` so the compiler forces you to decide what null
  does.
