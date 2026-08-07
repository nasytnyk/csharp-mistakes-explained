---
id: "0051"
title: The banned user walked in
category: pattern-matching
tags: [pattern-matching, flags-enum, HasFlag]
author: palkotnyk
rule: "never test a **[Flags]** enum with `is` - a constant pattern is exact equality, not `HasFlag`"
---

# #0051 - The Banned User Walked In

## 💥 Symptom

A banned user is still using the product. The moderation gate is right there -
`if (user.Access is not Access.Banned) Allow();` - and it reads exactly like "let
everyone in except the banned." The audit log shows the `Banned` bit set on the
account the whole time. Nothing failed loudly; the gate quietly decided this
particular banned user was fine, and it will keep deciding that for every banned
user who happens to carry a second flag.

## 🔍 The Offending Code

```csharp
[Flags] enum Access { None = 0, Banned = 1, Muted = 2, Verified = 4 }

Access access = Access.Banned | Access.Muted; // banned, then also muted
bool allowed = access is not Access.Banned;    // 💥 true - the ban just evaporated
```

## 🧠 What's Actually Going On

A constant pattern is **exact equality**, not a bit test. `access is Access.Banned`
compiles to `access == Access.Banned` - a whole-value comparison against the single
constant `1`. The moment the account carries more than one flag its value is
`Banned | Muted == 3`, which is not equal to `1`, so `is Access.Banned` is **false**
and `is not Access.Banned` is **true**. The gate opens.

`HasFlag` - or the bitwise `(access & Access.Banned) != 0` - asks the different,
correct question: "is the Banned *bit* set?" A pattern cannot ask that; there is no
bitwise constant pattern. Both spellings read identically in English ("is not
banned"), which is the whole trap: the language moved from `==` to `is`, an IDE hint
nudged the rewrite, and the meaning silently narrowed from "any value equal to
Banned" - which for a lone-flag account was accidentally correct - to "exactly Banned
and nothing else."

## ✅ The Fix

Ask the bitwise question explicitly:

```csharp
bool allowed = !access.HasFlag(Access.Banned); // or: (access & Access.Banned) == 0
```

Full fix in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `!access.HasFlag(Access.Banned)` | The readable default for `[Flags]` enums - one bit test, negated for the gate. |
| `(access & Access.Banned) == 0` | Hot paths - the same bit test spelled out, sidestepping `HasFlag`'s historical boxing cost. |
| A non-`[Flags]` enum, one state per value | If an account is only ever exactly one of `{Active, Banned, Muted}`, a plain enum makes `is Access.Banned` correct again - and a pattern the right tool. |
| Keep pattern syntax, move the bit test to a `when` guard | In a `switch`, write `_ when access.HasFlag(Access.Banned) => Block()` - the guard asks the bitwise question the constant arm cannot. |

## 😈 The Even Worse Sibling

The same rewrite in reverse locks out the people you most want in. `access is
Access.Admin` as an *admin-only* gate passes only the account whose access is
*exactly* Admin and nothing else - so the moment an admin also earns `Verified`, or
any second flag, the gate rejects them. The more trusted the account, the more flags
it accumulates, the more certainly the gate fails: your most privileged users are the
first to be shut out, and the junior with the single Admin bit is the only one who
ever gets through. A `switch` has the identical blind spot - a `case Access.Banned =>
Block()` arm lets the banned-and-muted user fall straight through to the default
"welcome" branch, no warning, because that arm is the same constant pattern wearing
different syntax.

## 🎓 Advanced Nuance

- **`is` is `==`, and neither masks.** `access is Access.Banned` lowers to a plain
  equality check against the constant - no `HasFlag`, no `&`, no conversion. That is
  the same reason `is` on a flags enum can never express "has this bit."
- **Relational patterns don't rescue it.** `access is >= Access.Banned` compiles
  (enums order by their underlying value) but means "numeric value >= 1," a different
  wrong answer, not a bit test. Only masking or `HasFlag` asks about a specific bit.
- **`None = 0` carries its own version of this.** `access.HasFlag(Access.None)` is
  *always* true - zero is a subset of every value - so a "has no access" check written
  as `HasFlag(None)` never distinguishes anyone. Same enum, adjacent trap: test the
  concept, not the zero.
- **No analyzer flags it.** `is Access.Banned` on a `[Flags]` enum is a perfectly
  legal constant pattern; the compiler cannot know you meant a bit test. This is a
  review rule, not a warning.

## 🔎 How to Find It in Your Codebase

- Grep for `is` / `is not` / `case` patterns whose constant is a member of a `[Flags]`
  enum - `is\s+\w+\.(Banned|Admin|Read|Write|Delete|...)` next to enums marked
  `[Flags]`. Each one is exact equality where you almost certainly meant a bit test.
- Audit `== SomeFlag` / `!= SomeFlag` comparisons on flags enums too - same bug, older
  spelling - and any IDE "use pattern matching" suggestion that rewrites one.
- Test every authorization gate on a `[Flags]` enum with a *combined* value
  (`Banned | Muted`), never just the single flag: the single-flag input passes for
  both the correct and the broken implementation, so it proves nothing.
- The tell in review: a `[Flags]` enum and a constant (pattern or `==`) in the same
  boolean, with no `&` or `HasFlag` anywhere in sight.
