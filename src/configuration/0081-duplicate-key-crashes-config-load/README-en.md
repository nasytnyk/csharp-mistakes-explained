---
id: "0081"
title: a duplicate key crashes config load
category: configuration
tags: [configuration, json, appsettings]
rule: "a duplicate key in appsettings is not 'last wins' - the JSON provider **refuses to load the file**"
---

# #0081 - A Duplicate Key Crashes Config Load

## 💥 Symptom

The service will not start on one deploy. Startup throws
`InvalidDataException: Failed to load configuration from file
'.../appsettings.json'`, and the first instinct is a path or permissions problem -
the message points squarely at the file. The file is fine. Somewhere inside it,
a merge (or a copy-pasted block) left the same key defined twice, and the JSON
configuration provider will not load a file with a duplicate key at all.

## 🔍 The Offending Code

```jsonc
{
  "Database": {
    "MaxConnections": 10,
    "CommandTimeout": 30,
    "MaxConnections": 100   // 💥 same key again - the whole file fails to load
  }
}
```

```csharp
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build(); // throws InvalidDataException here
```

## 🧠 What's Actually Going On

The JSON configuration provider flattens the document into a dictionary of
`Section:Key` paths, and a dictionary cannot hold the same key twice. When it
walks the object and reaches `MaxConnections` a second time, it does not overwrite
and it does not keep the last value - it throws `FormatException: A duplicate key
'Database:MaxConnections' was found`, which the provider wraps in
`InvalidDataException: Failed to load configuration from file '...'` and lets
propagate out of `Build()`. The whole file is rejected; not one key from it loads.

The broken belief comes from JSON itself: the JSON spec allows duplicate object
members and most parsers silently keep the last one, so "duplicate key = last
wins" is a reasonable habit from JavaScript, APIs, and log tooling. The .NET
configuration provider deliberately does not follow that - an ambiguous key is
treated as a corrupt file, not a resolvable one. And the exception you actually
see names the *file*, while the useful detail (which key) is one level down in
`InnerException`, so the investigation starts at "is the path right?" instead of
"which key is doubled?"

## ✅ The Fix

Resolve the duplication so each key appears exactly once - usually a merge
conflict or a pasted block that needs cleaning up.

```jsonc
{
  "Database": {
    "MaxConnections": 100,
    "CommandTimeout": 30
  }
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| Keep each key once | The direct fix - decide which value is correct and delete the other; a duplicate is never what you meant. |
| Read `InnerException` for the key | Diagnosing the crash - the top-level message names the file, but `InnerException` says `A duplicate key '...' was found`; log or unwrap it to jump straight to the line. |
| A JSON schema / lint in CI | Prevention - a validator or a "no duplicate keys" lint on `appsettings*.json` catches the merge artifact before it ships. |
| Split env-specific overrides into their own file | The duplicate came from stacking base + environment values in one file - put the override in `appsettings.{Environment}.json`, where a repeated key legitimately *replaces* across files instead of colliding within one. |

## 😈 The Even Worse Sibling

A hard crash at startup is the *good* outcome here - it is loud, it is immediate,
and it stops a config you cannot reason about from running. The nastier case is a
duplicate that does *not* collide within a single file: define `MaxConnections` in
`appsettings.json` and again in `appsettings.Production.json`, and configuration
does exactly what it is built to do - the later provider wins, silently. That is
correct and intended, but it means a value you thought you set in the base file is
quietly overridden by an environment file you forgot about, with no error to point
you there (see [0070-env-var-overrides-appsettings](../0070-env-var-overrides-appsettings/)).
The within-a-file duplicate screams; the across-files duplicate whispers, and the
whisper is the one that ships wrong numbers.

## 🎓 Advanced Nuance

- **The check is case-insensitive.** Configuration keys are compared
  case-insensitively, so `"MaxConnections"` and `"maxconnections"` in the same
  object also collide and throw - they are the same key as far as the provider is
  concerned.
- **JSON arrays are indexed keys under the hood, so they cannot "duplicate."**
  `[ "a", "b" ]` becomes `:0`, `:1`; there is no way to write the same index
  twice, which is why lists never hit this error the way named object members do
  (see [0080-env-array-needs-indexed-keys](../0080-env-array-needs-indexed-keys/)).
- **Comments and trailing commas are allowed; duplicate keys are not.** The
  provider tolerates JSONC niceties (`//` comments, trailing commas), so a file
  can look lenient and still be rejected for the one thing it is strict about - a
  repeated key.

## 🔎 How to Find It in Your Codebase

- When a deploy throws `InvalidDataException: Failed to load configuration from
  file '...'`, read `InnerException` immediately - it names the duplicated key and
  turns a "why won't config load" hunt into a one-line fix.
- Grep `appsettings*.json` (and any custom JSON config) for keys that appear twice
  in the same object, especially around recent merge conflicts or pasted blocks.
- Add a duplicate-key lint or JSON-schema validation step in CI so the corrupt
  file fails the pipeline instead of the production boot.
- Use `appsettings.{Environment}.json` for overrides rather than repeating a key
  in one file - across files a repeated key legitimately replaces; within a file
  it crashes.
