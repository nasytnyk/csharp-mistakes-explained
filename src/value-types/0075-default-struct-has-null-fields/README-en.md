---
id: "0075"
title: a default struct has null fields
category: value-types
tags: [value-types, struct, default]
rule: "never rely on a struct's ctor for `default` or arrays - reference fields come back **null**"
---

# #0075 - A Default Struct Has Null Fields

## 💥 Symptom

A `NullReferenceException` fires from a value the type system swears is fully constructed - a struct
whose constructor "always" news up its list, yet `.Items` is null. The crash lands on the first
`.Add`, far from where the struct was created, and the struct itself isn't null (a value type can't
be) - only its fields are.

## 🔍 The Offending Code

```csharp
struct Cart { public List<string> Items; public Cart() => Items = new(); }

var carts = new Cart[100];    // 💥 array elements are default(Cart): no constructor ran
carts[0].Items.Add("SKU-1");  // Items is null -> NullReferenceException
```

## 🧠 What's Actually Going On

`default(T)` and array allocation produce a value by **zeroing memory** - they run no constructor
and no field initializers. For a struct, every field becomes its zero: numeric fields are `0`,
`bool` is `false`, and every **reference** field (`List<T>`, `string`, a nested class) is `null`.
`new Cart()` runs the parameterless constructor and initializes `Items`; `new Cart[n]` and
`default(Cart)` do not - they hand you a `Cart` that *looks* constructed (a real value, not null)
but never executed the code that fills its fields.

The broken belief is "the constructor guarantees `Items` is set." It guarantees that only for
`new Cart()`. A struct can be created *without* its constructor - `default`, `new T[n]`, an
unassigned field of struct type, an `out` parameter - and every such path skips straight to zeroed
memory. The type is non-nullable and looks fully built; the null is one field down, where nothing
made you look.

## ✅ The Fix

Construct every element - do not rely on the struct's constructor for `default` / array paths:

```csharp
var carts = new Cart[100];
for (int i = 0; i < carts.Length; i++)
    carts[i] = new Cart(); // run the constructor per slot, so Items is a real list
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| Construct each element (`arr[i] = new Cart()`) | You need a struct array with initialized fields - fill it explicitly, because `new T[n]` never runs your constructor. |
| Make it a `class` | The type owns reference state that must exist - a class array is `[null]`, an honest signal you must `new` each item, and you cannot get a half-built instance from `default`. |
| Lazy / guaranteed access | Expose the list through a property that initializes on first use (`Items ??= new()`), so even a `default` value hands back a real list. |
| A value with no reference fields | If it is small and truly value-like, avoid mutable reference fields altogether - then `default` is already a complete, valid value. |

## 😈 The Even Worse Sibling

The `NullReferenceException` is the *loud* half of this bug. Its silent twin is the field that comes
back a wrong value instead of null: a `struct` with `public decimal Rate = 1.0m;` (or
`public bool Enabled = true;`), initialized in code and materialized through `default` or an array,
comes back `Rate = 0` and `Enabled = false` - no crash, just a multiplier that quietly zeroes every
total and a feature that silently defaults off, only on the path that used an array. Same root -
`default` skips construction - opposite fear rung: the reference field throws where you can see it,
the value field ships a wrong number where you cannot. The one that crashes is the one you are lucky
to have.

## 🎓 Advanced Nuance

- **A struct with field initializers will not compile without a declared constructor.**
  `struct Cart { public List<string> Items = new(); }` is a C# error unless you also write a
  constructor - and even then, the initializer runs only on `new Cart()`, never on `default` /
  array. The language makes you write the constructor; it cannot make every creation path call it.
- **`default` is unavoidable for value types.** You can discourage misuse, but you cannot stop
  `default(Cart)`, `new Cart[n]`, `List<Cart>` growth, or a `Cart` field left unassigned - the
  runtime hands out the zero value for all of them. Every BCL struct is the same: `default(Guid)` is
  `Guid.Empty`, `default(DateTime)` is `0001-01-01` (see
  [0054-new-guid-is-empty](../0054-new-guid-is-empty/)) - `default` is the zero value, never null or
  "unset." If a struct cannot survive being all-zeros, it should be a class.
- **`out` parameters and generic `default(T)` hit it too.** `Dictionary.TryGetValue` sets a struct
  `out` to `default` on a miss; a generic method returning `default(T)` returns the zeroed struct -
  both give you the null-field value with no visible `new` anywhere.

## 🔎 How to Find It in Your Codebase

- Grep for `struct` types with reference-type fields (`List<>`, `string`, arrays, a class) and a
  constructor that initializes them - then look for `new ThatStruct[`, `default(ThatStruct)`, and
  `TryGetValue` / `out` uses that skip the constructor.
- Symptom-side: `NullReferenceException` on a field of a struct value that "cannot be null"; a crash
  on the first array element used, the first `TryGetValue` miss, or the first unassigned field of
  struct type.
- For value-like data that must own a collection, prefer a `class`, or expose the collection through
  a property that initializes lazily so `default` still works.
- If it must stay a struct, populate arrays explicitly (`arr[i] = new(...)`) and never assume a
  `default` instance ran your constructor.
