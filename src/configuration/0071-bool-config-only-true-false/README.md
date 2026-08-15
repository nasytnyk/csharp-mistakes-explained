---
id: "0071"
title: a config bool accepts only true or false
category: configuration
tags: [configuration, boolean, type-conversion]
rule: "never write a config bool as **1** or yes - only `true`/`false` parse"
---

# #0071 - A Config Bool Accepts Only true or false

## 💥 Symptom

A feature flag flipped to on in `appsettings.json` takes the whole app down instead. The value
looks obviously true - `"Enabled": 1` - and yet the service throws
`InvalidOperationException: Failed to convert configuration value ... to type 'System.Boolean'`
the moment it reads the flag. The config file is right there, the intent is unmistakable, and the
app refuses to start (or dies on the first request that touches the setting).

## 🔍 The Offending Code

```csharp
// appsettings.json: "Features": { "NewCheckout": 1 }
bool enabled = config.GetValue<bool>("Features:NewCheckout"); // 💥 "1" is not true/false - throws
```

## 🧠 What's Actually Going On

The configuration binder converts a `bool` by running the string through `bool.Parse`, and
`bool.Parse` accepts exactly two spellings: `"true"` and `"false"` (case-insensitive, whitespace
trimmed). Nothing else parses. `"1"`, `"0"`, `"yes"`, `"no"`, `"on"`, `"off"`, `"y"`, `"n"` all
throw `FormatException`, which the binder surfaces as `InvalidOperationException` at the read site.
Providers stringify every value, so a JSON *number* `1` and a JSON *string* `"1"` are identical to
the binder - both arrive as `"1"`, both throw.

The broken belief is "1 means true, everybody knows that." Every *other* config surface trained
you into it - shell environment variables, INI files, YAML, feature-flag UIs, and other languages
all treat `1`/`yes`/`on` as truthy - so `1` feels like the obvious way to switch something on.
.NET's binder does not agree: it wants the two literal words and rejects the rest, loudly.

## ✅ The Fix

Write the two literals the binder understands - `true` or `false`:

```csharp
// appsettings.json: "Features": { "NewCheckout": true }
bool enabled = config.GetValue<bool>("Features:NewCheckout"); // parses cleanly
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `true` / `false` in the config | The default - write the literal the binder parses; in JSON prefer the native boolean `true`, not the number `1` or the string `"true"`. |
| A custom `TypeConverter` / manual mapping | You genuinely must accept `1`/`yes`/`on` (a shared config format, operator habit) - convert `"1"` yourself, don't rely on `bool.Parse`. |
| An `enum` instead of a `bool` | The setting has more than two states, or you want `Off`/`On`/`Auto` - an enum binds by name and documents the options. |
| Validate options at startup | Bind to an options class with `ValidateOnStart()` so a bad bool fails fast at boot with a clear message, not deep in a request. |

## 😈 The Even Worse Sibling

The crash is the *lucky* outcome - it's loud, immediate, and names the key. The quiet trap is the
same file mixing spellings: JSON's native `true` binds fine, so `"A": true` works while `"B": 1`
throws, and a reviewer scanning the file sees two "booleans" that look equally valid. Worse is
*where* it throws: `GetValue<bool>` runs when the code reads the flag, which is often deep inside a
service on the first request that hits that path - not at config load - so a typo in a rarely-used
flag sails through startup and every smoke test, then takes down one endpoint hours later on the
on-call engineer's clock. And the near-miss that doesn't throw is its own problem: a `bool` bound
from a *missing* key stays at its default `false` silently (a different exhibit) - so the same
setting can crash when present-and-wrong, or vanish when absent, but almost never tells you it was
misconfigured.

## 🎓 Advanced Nuance

- **`bool.Parse` is the converter, and it is strict on purpose.** It is not the lenient
  `Convert.ToBoolean` (which turns the *number* `1` into `true`); the config pipeline goes through
  `TypeConverter` -> `bool.Parse`, which only knows `true`/`false`. Knowing the converter tells you
  exactly which values work.
- **JSON number vs JSON string vs env var are all the same string to the binder.** `1`, `"1"`, and
  an environment variable `Features__NewCheckout=1` are indistinguishable by the time the binder
  sees them - all `"1"`, all throwing - so "it's a real JSON number" buys you nothing here.
- **`bool?` does not soften it.** A nullable `bool` still parses through the same converter; `"1"`
  throws rather than binding to `null`. Nullable buys you "absent" versus "present," not "lenient."

## 🔎 How to Find It in Your Codebase

- Grep your `appsettings*.json` (and env vars, and CI variables) for boolean-intent keys set to
  `1`, `0`, `"yes"`, `"on"`, `"true"` (the quoted string) - anything that isn't a bare JSON `true`
  or `false` under a key you bind to `bool`.
- Bind config to an options class and call `ValidateOnStart()` so a bad boolean fails at boot with
  the key named, instead of `InvalidOperationException` deep in a request path.
- Symptom-side: `Failed to convert configuration value ... to type 'System.Boolean'` in logs;
  feature flags that "don't turn on"; an endpoint that throws only when a specific flag is read.
- If a config source must use `1`/`yes`, convert it explicitly (a custom `TypeConverter` or a
  manual map) rather than binding straight to `bool`.
