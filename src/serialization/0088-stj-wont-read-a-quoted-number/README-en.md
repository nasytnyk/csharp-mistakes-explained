---
id: "0088"
title: System.Text.Json won't read a quoted number
category: serialization
tags: [serialization, System.Text.Json, numbers]
rule: "never expect `System.Text.Json` to read a quoted number - `\"3\"` **won't bind** to an int"
---

# #0088 - System.Text.Json Won't Read a Quoted Number

## 💥 Symptom

The integration worked against your own test payloads and then threw the moment a
real partner sent traffic: `JsonException: The JSON value could not be converted
to ... Path: $.Quantity`. The JSON is well-formed and the field is present - the
only difference is that the number arrived as `"3"` instead of `3`. One pair of
quotes, and the entire object fails to deserialize.

## 🔍 The Offending Code

```csharp
string json = """{ "Quantity": "3", "UnitPrice": "9.99" }""";

var order = JsonSerializer.Deserialize<Order>(json); // 💥 "3" is a string, not a number

record Order(int Quantity, decimal UnitPrice);
```

## 🧠 What's Actually Going On

`System.Text.Json` is strict about JSON types by default: a JSON *string* token
(`"3"`) is not a JSON *number* token (`3`), and the converter for an `int`/`decimal`
property only accepts the number token. When it meets a quoted value where a
number is expected, it does not try to parse the text - it throws `JsonException`,
and because the failure happens mid-object, the *whole* deserialization fails, not
just that one field. Newtonsoft.Json, by contrast, quietly parses `"3"` into an
`int`, so code migrated from Newtonsoft (or written by someone used to it) carries
the assumption that quoted numbers "just work."

The broken belief is "it's a valid number in the JSON, so it will bind." JSON's
type system distinguishes strings from numbers, and STJ honors that distinction
strictly rather than coercing. The trap is that the wire format is often outside
your control: JavaScript's `JSON.stringify` keeps numbers unquoted, but hand-built
payloads, form encoders, `BigInt`/decimal libraries that stringify to preserve
precision, and many partner APIs send numbers as strings. Your own tests use clean
numeric JSON and pass; the first client that quotes its numbers takes the endpoint
down.

## ✅ The Fix

Opt into reading numbers from strings with `JsonNumberHandling.AllowReadingFromString`.

```csharp
var options = new JsonSerializerOptions
{
    NumberHandling = JsonNumberHandling.AllowReadingFromString,
};

var order = JsonSerializer.Deserialize<Order>(json, options); // "3" -> 3
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| `NumberHandling = AllowReadingFromString` (global) | The source may quote any number - set it once on the shared `JsonSerializerOptions` so every numeric property tolerates a quoted value. |
| `[JsonNumberHandling(...)]` on the property/type | Only some fields are quoted (a precision-preserving `decimal`, an ID) - annotate just those, leaving the rest strict. |
| `AllowReadingFromString \| WriteAsString` | You must round-trip - read quoted numbers *and* write them back quoted (e.g. to preserve `long`/`decimal` precision for a JS consumer). |
| Fix the producer | You own both ends - emit real JSON numbers so neither side needs the relaxed mode; reserve string-quoting for values that genuinely need it. |

## 😈 The Even Worse Sibling

A hard `JsonException` at least stops the bad data at the door - you know the
parse failed. The quieter cousin is the *casing* mismatch, where the number is
unquoted and valid but the property name does not match, so STJ leaves the
property at its default and reports no error at all - a `{"amount": 100}` binding
to an unmatched `Amount` gives you a silent `0`, an order that deserialized
"successfully" at zero price (see
[0012-zero-priced-order](../0012-zero-priced-order/)).
And `AllowReadingFromString` has its own sharp edge: it makes the parse *more*
lenient, so a genuinely malformed `"3abc"` still throws, but an empty string
`""` for a nullable number can now bind in ways you did not intend - loosening the
type gate is a trade, not a free win.

## 🎓 Advanced Nuance

- **It is asymmetric by default.** STJ *writes* numbers unquoted and *reads* only
  unquoted numbers unless you opt in; `AllowReadingFromString` changes only the
  read side. If a consumer needs quoted numbers on the wire, add `WriteAsString`
  explicitly.
- **The failure is whole-object, not per-field.** Because the exception aborts the
  deserialization, one quoted number fails the entire payload - you do not get a
  partially-populated object with just that field defaulted, you get nothing.
- **`JsonNumberHandling.Strict` is the default, and `AllowNamedFloatingPointLiterals`
  is separate.** Reading `"NaN"`/`"Infinity"` is a *different* flag; allowing
  quoted integers does not allow quoted `NaN`, so mixing float-literal payloads
  needs its own opt-in.

## 🔎 How to Find It in Your Codebase

- Grep for `JsonSerializer.Deserialize`/`ReadFromJsonAsync` on DTOs with numeric
  (`int`, `long`, `decimal`, `double`) properties whose JSON comes from an external
  partner, a browser form, or a system known to stringify numbers.
- Reproduce with a payload that quotes the numbers (`"3"` not `3`) - if it throws
  `JsonException: The JSON value could not be converted`, this is it.
- Symptom-side: an endpoint that passes internal tests but 400/500s for a specific
  client, deserialization failures that name a numeric `$.Path`, integrations that
  broke after switching from Newtonsoft.Json to System.Text.Json.
- Decide deliberately: relax with `AllowReadingFromString` where the wire format is
  outside your control, or fix the producer to emit real JSON numbers where you own
  it.
