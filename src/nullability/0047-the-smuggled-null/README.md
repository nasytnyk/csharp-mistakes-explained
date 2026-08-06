---
id: "0047"
title: A JSON null smuggled into a non-nullable property
category: nullability
tags: [nullability, System.Text.Json, NullReferenceException]
rule: "never assume deserialization respects your **non-nullable** annotations"
---

# #0047 - A JSON Null Smuggled Into a Non-Nullable Property

## 💥 Symptom

A `NullReferenceException` on a DTO property the type system swears is non-null -
`string CustomerId`, no `?`, even an initializer. The crash is nowhere near the
API boundary; it is deep in business logic, hours after the request came in, and
the value that is null is one that "cannot be null". Pull the offending request
and the payload reads `{ "CustomerId": null, ... }` - a client serialized an
absent field as JSON `null`, and the deserializer wrote it straight into the
non-nullable property without a word.

## 🔍 The Offending Code

```csharp
var order = JsonSerializer.Deserialize<Order>(payload)!; // payload: {"CustomerId": null}
// order.CustomerId is null, though its type is a non-null string
UseCustomer(order.CustomerId); // 💥 NRE downstream, warning-free at compile time

class Order { public string CustomerId { get; set; } = ""; /* ... */ }
```

## 🧠 What's Actually Going On

Nullable reference annotations are a **compile-time** feature, erased from the
assembly. `System.Text.Json` deserializes through reflection at runtime, where
those annotations no longer exist, so by default it does not consult them at all.
An explicit JSON `null` is a perfectly valid value to assign to a `string`
property as far as the runtime is concerned - it overwrites the property, *even
one with an initializer*, and hands you back an object in a state the type system
forbids. The `= ""` default you wrote is set by the constructor and then flattened
by the incoming `null`.

Nothing warns, at any point. The property's declared type is `string`, so every
downstream `order.CustomerId.Whatever()` is non-null to the compiler and compiles
clean. The null rides along, forbidden but real, until the first dereference turns
it into an NRE far from the boundary that let it in. This is the same gap
[0046-null-forgiving-lies](../../nullability/0046-null-forgiving-lies/) opens from
the other direction - there you tell the compiler a lie; here the wire does - and
both work because the annotation is a story the runtime never hears, the way it
does not hear it for boxing either
([0043-nullable-boxes-to-nothing](../../boxing/0043-nullable-boxes-to-nothing/)).

## ✅ The Fix

Make the deserializer enforce the annotations. `RespectNullableAnnotations`
(.NET 9+) turns a non-nullable property into an actual runtime check, so the null
is rejected at the boundary with a clear `JsonException` instead of smuggled inside:

```csharp
var options = new JsonSerializerOptions { RespectNullableAnnotations = true };
var order = JsonSerializer.Deserialize<Order>(payload, options); // throws on {"CustomerId": null}
```

Full version in [Good.cs](Good.cs). The options, layered:

| Approach | When it's the right call |
|---|---|
| `RespectNullableAnnotations = true` on the shared `JsonSerializerOptions` | The default going forward - one setting turns every non-nullable property into a boundary check; catch the `JsonException` and return a 400 |
| Validate after deserializing (guard/`DataAnnotations`) | Pre-.NET 9, or when you want to collect all violations into one error rather than fail on the first |
| Make the property honestly nullable (`string?`) and handle the null | The field really is optional - stop lying about its type and deal with the absence explicitly |
| A custom converter / contract | You need per-type rules the global switch cannot express |

## 😈 The Even Worse Sibling

`required` is the fix everyone reaches for, and it closes the wrong door. It guards
*presence*: a missing `CustomerId` throws `JsonException`, good - but
`{ "CustomerId": null }` supplies the member, so `required` is satisfied and the
null lands anyway (verified). It feels like null-safety and is only
missing-member-safety. And the deeper danger is not the crash: an NRE at least
stops the line. If the forbidden null is *persisted* first - written to the
database, forwarded to the next service - every downstream consumer now inherits a
value the type system says is impossible, and the crash surfaces in a component
that never touched the wire. A positional record makes it quieter still: a missing
member binds `default` (null) through the constructor with no error at all.

## 🎓 Advanced Nuance

`RespectNullableAnnotations` is **off by default** and always will be, for backward
compatibility - reading it as "the runtime got stricter" is the trap; you have to
opt in, per `JsonSerializerOptions`, everywhere you deserialize. Before .NET 9 there
was no switch at all: the only defenses were a custom converter or a validation pass
after binding.

The annotation gap is not unique to `System.Text.Json`. `Newtonsoft.Json` ignores
NRT the same way and has no equivalent global switch - you reach for
`[JsonProperty(Required = Required.Always)]` (again, presence, not null) or a custom
contract resolver. Any reflection-driven binder - config, an ORM materializing a
row, a mapper - is in the same position: it sets your properties at runtime, where
`string` and `string?` are the same type. The compile-time "non-null" guarantee
stops precisely at the boundary where data enters from outside the compiler's sight.

## 🔎 How to Find It in Your Codebase

- Audit every deserialization boundary - `JsonSerializer.Deserialize`, model binding,
  message consumers - whose DTO has non-nullable reference properties. Each is a
  place the wire can plant a null the type system forbids.
- Turn on `RespectNullableAnnotations` in the shared `JsonSerializerOptions` (and in
  ASP.NET Core's JSON options) and handle the `JsonException`. `required` is **not**
  a substitute - it stops missing members, not explicit nulls.
- No analyzer catches this; it is a runtime/configuration gap, not a code smell in
  the DTO. It is a boundary-hardening and test rule.
- Test each DTO with two payloads: the member **missing** and the member explicitly
  **null**. Fixtures that always send a value never exercise either path - the same
  blind spot that hides most nullable bugs.
