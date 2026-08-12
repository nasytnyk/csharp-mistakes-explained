---
id: "0054"
title: new Guid() is empty, not new
category: value-types
tags: [value-types, Guid, default]
rule: "never call `new Guid()` - it is empty; use `Guid.NewGuid()`"
---

# #0054 - new Guid() Is Empty, Not New

## 💥 Symptom

Entities are colliding. Two orders created seconds apart carry the *same* id -
`00000000-0000-0000-0000-000000000000` - and the repository, keyed by id, keeps only the
last one. Deduplication silently merges unrelated records; a cache returns the wrong
entity; a foreign key points at whichever row won the overwrite. Every id looks like a
real GUID - it is the right shape - it is just the *same* GUID everywhere, and nobody ever
wrote the word "empty."

## 🔍 The Offending Code

```csharp
var order = new Order { Id = new Guid(), /* ... */ }; // 💥 Guid.Empty, not a fresh id
```

## 🧠 What's Actually Going On

`Guid` is a struct, and `new Guid()` invokes its **default (parameterless) constructor**,
which - exactly like `default(Guid)` - produces the all-zeros value `Guid.Empty`. It does
not generate anything. Producing a random GUID is a separate, *static* call:
`Guid.NewGuid()`. The two read almost identically - `new Guid()` versus `Guid.NewGuid()` -
and one of them is the most convincing-looking way in the language to hand out a duplicate
id.

Nothing warns you: `new Guid()` is valid, returns a real `Guid`, and prints in the familiar
`8-4-4-4-12` shape. It is only when two of them meet - as a dictionary key, in a `HashSet`,
through `Distinct()`, or against a unique DB index - that "unique id" turns out to mean "the
same id every time." The broken belief is "`new` makes a new one." For a *reference* type
`new` allocates a fresh object; for `Guid` (a *value* type) `new` just zero-initializes, and
zero is a single fixed, shared sentinel.

## ✅ The Fix

Call the factory that actually generates a value:

```csharp
var order = new Order { Id = Guid.NewGuid(), /* ... */ };
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `Guid.NewGuid()` | You need a fresh, random (v4) id now - the default for entity ids created in code. |
| Let the database generate it | The column is `uniqueidentifier DEFAULT NEWID()` / `NEWSEQUENTIALID()` - don't set `Id` in code at all; read it back after insert. |
| `Guid.CreateVersion7()` (.NET 9+) | You want time-ordered GUIDs for index locality - still a real generator, never `new Guid()`. |
| `Guid.Empty`, written by name | You genuinely mean "no id yet." Spell it `Guid.Empty` so the intent is unmistakable and greppable - not `new Guid()`. |

## 😈 The Even Worse Sibling

`Guid.Empty` is a *valid* value everywhere, so it fails late and quietly. It sails past
`!= null` checks - a `Guid` is a non-nullable struct, so "unset" cannot be null, it is just
zero. It inserts fine until it reaches a *unique* index, where the **second** row throws - so
the first empty-id record saves, the failure surfaces on an unrelated later insert, and the
stack trace blames the database, not `new Guid()`. And an all-zero id is a perfectly good
foreign key: child rows happily reference `Guid.Empty`, wiring themselves to whatever parent
last claimed it, so a "successful" import can silently attach every orphan to one phantom
record.

## 🎓 Advanced Nuance

- **It is the general value-type `new` rule wearing a GUID.** `new int()` is `0`,
  `new DateTime()` is `0001-01-01`, `new TStruct()` is `default(TStruct)` - the parameterless
  `new` on any struct just zero-initializes. `Guid` is the case where "zero" looks most like a
  legitimate value, which is why this one bites hardest.
- **`default(Guid)`, `= new Guid()`, and an unassigned field are byte-identical.** A `Guid`
  property never set, `default(Guid)`, and `new Guid()` are all the same `Guid.Empty`. So the
  bug also shows up with *no* offending line at all - forget to assign `Id` and you get the
  exact same collision as writing `new Guid()`.
- **No compiler warning; analyzers are only catching up.** Some style rulesets surface a
  "did you mean `Guid.NewGuid()`?" hint on `new Guid()`, but it is off by default - a
  constructor call that returns the zero value is entirely legal C#.

## 🔎 How to Find It in Your Codebase

- Grep for `new Guid()` - nearly every occurrence that is not explicitly meant to be empty is
  a bug. Replace with `Guid.NewGuid()`, or `Guid.Empty` written by name when empty is the
  intent.
- Check entity `Id` assignments in constructors, object initializers, and mappers; and scan
  logs for ids that come out as `00000000-0000-0000-0000-000000000000`.
- Symptom-side: unique-index violations on the *second* insert, dedup or cache that merges
  distinct records into one, foreign keys pointing at an all-zero id.
- Guard the boundary: reject `Guid.Empty` where a real id is required
  (`if (id == Guid.Empty) throw ...`), or let the database own id generation so code never
  spells the id at all.
