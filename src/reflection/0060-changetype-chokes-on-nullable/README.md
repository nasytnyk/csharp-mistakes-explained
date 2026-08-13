---
id: "0060"
title: Convert.ChangeType chokes on Nullable<T>
category: reflection
tags: [reflection, Convert, Nullable]
rule: "never `Convert.ChangeType` into a **nullable** type - unwrap it with `Nullable.GetUnderlyingType` first"
---

# #0060 - Convert.ChangeType Chokes on Nullable&lt;T&gt;

## 💥 Symptom

A hand-rolled mapper - CSV into objects, a config binder, a query-string binder - has worked for
months, then one import crashes with `InvalidCastException`. It isn't a malformed file: the
values are all valid. Diff the crashing row against a good one and nothing structural differs -
the only change is that one *optional* column, always empty in the test data, finally arrived
with a value. That value is what blows up.

## 🔍 The Offending Code

```csharp
foreach (var prop in typeof(OrderLine).GetProperties())
{
    var value = Convert.ChangeType(cell, prop.PropertyType); // 💥 throws when PropertyType is int?
    prop.SetValue(target, value);
}
// Qty (int) converts fine; Discount (int?) -> InvalidCastException
```

## 🧠 What's Actually Going On

`Convert.ChangeType` converts between types that implement `IConvertible` - `int`, `long`,
`decimal`, `DateTime`, `bool`, `string`, the whole primitive zoo. `Nullable<T>` does *not*
implement `IConvertible`, and `ChangeType` has no special case to unwrap it, so
`ChangeType("10", typeof(int?))` has no conversion path and throws `InvalidCastException` - the
same `"5"` that converts cleanly into an `int`, `decimal`, or `DateTime` column dies the moment
the target is the nullable *wrapper* around one of them.

The broken belief is "ChangeType handles primitives, and `int?` is basically an `int`." The value
inside is basically an int; the *type* `int?` is a distinct struct that `ChangeType` doesn't know
how to produce. And it only ever surfaces on an optional column that actually has data, because a
null or absent cell skips the conversion entirely - which is exactly the column your fixtures
leave empty.

## ✅ The Fix

Unwrap the nullable to its underlying type before converting; boxing does the rest, because a
boxed `int` assigns straight into an `int?` property:

```csharp
var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType; // int? -> int, int -> int
var value = Convert.ChangeType(cell, target);
prop.SetValue(obj, value);   // the boxed int lands in the int? property
```

`Nullable.GetUnderlyingType` returns the `T` inside a `Nullable<T>`, and `null` for a
non-nullable type - so `?? prop.PropertyType` leaves ordinary columns untouched. Full version in
[Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `Nullable.GetUnderlyingType(t) ?? t` before `ChangeType` | The general fix for a reflective mapper - one line covers nullable and non-nullable columns. |
| Handle the empty/null cell first | An empty optional cell should map to `null`, not be converted - short-circuit before `ChangeType` so you set null instead of coercing `""` to `0`. |
| `TypeDescriptor.GetConverter(t)` | Richer than `IConvertible` (enums, `Guid`, custom types) and it already understands `Nullable<T>` and any `TypeConverter` you defined. |
| A real mapper (Dapper, `System.Text.Json`, a source generator) | If you're rebuilding one, the library already unwraps nullables, caches reflection, and handles culture - hand-rolled `ChangeType` loops exist mainly to meet this bug. |

## 😈 The Even Worse Sibling

The crash is the lucky outcome. Where `ChangeType` throws loudly on `int?`, its treatment of
*missing* values fails the opposite, silent way: `Convert.ChangeType(null, typeof(int))` returns
`0` (no throw), so a mapper that "helpfully" treats an absent cell as null quietly writes `0`
into every empty required number - and `0` is a real quantity, a real price, a real discount.
Culture rides along just as invisibly: `ChangeType("1,50", typeof(decimal))` is `150` under an
invariant culture and `1.5` under a comma-decimal one, so the same file imports as two different
numbers on two machines, neither of them erroring. The `InvalidCastException` in this exhibit at
least stops the line; the sibling ships a plausible wrong number into the database.

## 🎓 Advanced Nuance

- **`(int?)x` in C# is a cast; `ChangeType(.., typeof(int?))` is a lookup.** The compiler knows
  how to wrap an `int` in `Nullable<int>`; `ChangeType` only asks the target whether it is
  `IConvertible`, and `Nullable<T>` isn't - so a conversion the language does for free at compile
  time has no runtime path through `Convert`.
- **The boxing that makes the fix work is the flip side of
  [0043-nullable-boxes-to-nothing](../../boxing/0043-nullable-boxes-to-nothing/).** A
  `Nullable<int>` with a value boxes to a plain boxed `int` (a null one boxes to `null`), which is
  exactly why `SetValue` can drop a boxed `int` into an `int?` property with no wrapper in sight.
- **`Convert.ChangeType` cannot see your custom conversions.** It only recognizes `IConvertible`;
  an `implicit operator` or a `TypeConverter` you wrote is invisible to it. Use
  `TypeDescriptor.GetConverter(t)` when columns include enums, `Guid`, or your own value types -
  it unwraps nullables *and* honors your converters.

## 🔎 How to Find It in Your Codebase

- Grep for `Convert.ChangeType(` and check whether the target type can be a `Nullable<T>` - a
  `PropertyInfo.PropertyType`, a `Type` column, anything reflective. If it can, it needs the
  `GetUnderlyingType` unwrap.
- The tell is a mapper written and tested against required columns only; add a test that fills an
  *optional* (`int?`, `decimal?`, `DateTime?`) column with a value - that is the row production
  will crash on.
- Symptom-side: an import/parse that "crashes on this one file" with `InvalidCastException` from
  deep in `System.Convert`, where the good and bad files differ only by an optional field being
  populated.
- Prefer a real deserializer or `TypeDescriptor.GetConverter` over a hand-rolled `ChangeType`
  loop; if you keep the loop, unwrap nullables and decide explicitly what an empty cell means
  before converting.
