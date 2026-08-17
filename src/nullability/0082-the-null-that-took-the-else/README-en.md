---
id: "0082"
title: the null that took the else
category: nullability
tags: [nullability, bool, three-valued-logic]
rule: "never reduce a `bool?` to two branches - null is a **third state**; gate on `== true`"
---

# #0082 - The Null That Took the Else

## 💥 Symptom

A consent gate, a feature flag, an "is blocked" check - written as one condition
with two outcomes - quietly lets the wrong group through. Users who *opted out*
are correctly excluded, users who *opted in* are correctly included, and the users
nobody ever asked - the ones whose flag is still `null` - silently land on the
permissive side. No exception, a clean run, and a marketing blast that just went
to people who never agreed to receive it.

## 🔍 The Offending Code

```csharp
// MarketingConsent is bool?: true = opted in, false = opted out, null = never asked
foreach (var user in users)
    if (user.MarketingConsent != false) // 💥 null != false is true - "undecided" gets mailed
        Send(user);
```

## 🧠 What's Actually Going On

A `bool?` has **three** values - `true`, `false`, and `null` - but an
`if`/`else` has only two branches, so one of the three states has to be quietly
folded in with another. `!= false` is true for both `true` *and* `null`: the
comparison lifts to nullable logic, `null != false` evaluates to `true`, and the
undecided user follows the same path as the consenting one. The code reads like
"everyone who didn't opt out," but `null` never opted *in* either - it just was
never asked, and the two-way test has no place to put "don't know" except one of
the two real branches.

The broken belief is that a flag is a yes/no, so `!= false` means "yes." With a
nullable flag there are two ways to be "not false" - actually true, and not yet
decided - and they usually demand opposite handling: a consent you must *have*
before acting, a block you must *clear* before allowing. Gating on the negative
(`!= false`, `== false`, `!flag`) puts `null` on whichever side happens to be the
default, and for permissions that default is almost always the dangerous one:
unknown consent gets treated as consent, an unset "is verified" passes as
verified, a missing "is blocked" reads as not blocked.

## ✅ The Fix

Test for the one affirmative state you actually require, so `null` and `false`
both fall on the safe side.

```csharp
foreach (var user in users)
    if (user.MarketingConsent == true) // only an explicit yes qualifies
        Send(user);
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| Gate on `== true` (or `== false`) | The default rule - name the exact state that grants the action and require it, so `null` never rides along with the affirmative. |
| Handle all three states explicitly | `null` needs its *own* behavior (prompt again, queue for review, log) - `switch { true => ..., false => ..., null => ... }` forces you to decide, and the compiler flags a missing arm. |
| `flag ?? false` / `GetValueOrDefault()` | You genuinely want "unknown means no" - collapse to a plain `bool` on purpose, at one visible spot, instead of letting a lifted `!=` decide silently. |
| Make the state non-nullable | The value should always be known by the time you check - resolve it at the boundary (a required field, a migration default) so the third state cannot reach this code. |

## 😈 The Even Worse Sibling

Emailing the undecided is a compliance problem; the same shape guarding *access*
is a security one. `if (user.IsBlocked == true) Deny();` looks airtight, yet a
`null` `IsBlocked` - a row migrated before the column existed, a user the risk
check never scored - is not `== true`, so it sails past the guard and is allowed
in. And nullable three-valued logic gets stranger inside boolean algebra:
`null && false` is `false`, but `null || true` is `true`, and `!null` is still
`null` - so a compound guard like `if (!(a || b))` can evaluate to `null`, which
an `if` treats as `false`, quietly taking the else. The bug is never that the flag
had the wrong value; it is that "no value" was a value, and the two-branch test
had nowhere honest to put it.

## 🎓 Advanced Nuance

- **Comparisons lift; they don't throw.** `null != false` does not blow up - the
  `==`/`!=` operators on `bool?` are lifted to return an ordinary `bool`, so the
  mistake compiles cleanly and runs silently. It is `null` used *directly* in an
  `if` (a `bool?` where a `bool` is required) that the compiler rejects - which is
  exactly why people reach for `!= false` and reintroduce the bug.
- **`==`/`!=` are the only lifted operators that stay two-valued.** The logical
  operators follow three-valued (Kleene) logic and can *produce* `null`
  (`null & true == null`), while `==`/`!=` always return a plain `bool`. Mixing
  the two families in one expression is how a guard silently resolves `null` to
  `false`.
- **`GetValueOrDefault()` defaults to `false` for `bool?`.** Convenient, but it
  bakes in "unknown = no" invisibly; prefer it only where that policy is intended
  and obvious, not as a reflex to silence a nullable warning.

## 🔎 How to Find It in Your Codebase

- Grep for `!= false`, `== false`, and `!` applied to `bool?`-typed members
  (consent, `IsVerified`, `IsBlocked`, `IsActive`, feature flags), and ask what
  should happen when the value is `null` - if the answer differs from the `false`
  branch, it is this bug.
- Look at permission and compliance gates especially: any place where "unknown"
  must be treated as *deny* but the check only excludes explicit `false`.
- Symptom-side: actions taken for records that were never explicitly enabled,
  users included in a group they never joined, guards that pass for freshly
  migrated or partially onboarded rows whose flags are still null.
- Prefer `== true` for "must be affirmatively so," an explicit three-way `switch`
  when `null` has its own meaning, or a non-nullable field resolved at the
  boundary so the third state never reaches the decision.
