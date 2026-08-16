---
id: "0080"
title: env array needs indexed keys
category: configuration
tags: [configuration, environment-variables, arrays]
rule: "never pack an array into one env var - give each item an indexed key (`KEY__0`, `KEY__1`)"
---

# #0080 - An Env Array Needs Indexed Keys

## 💥 Symptom

The allowlist is right there in the environment -
`ALLOWEDHOSTS=payments.acme.com,billing.acme.com,mail.acme.com`, three hosts,
comma-separated like every other tool wants. The app binds it to `string[]`,
and the array comes back **empty**. No error, no warning - just zero elements
where three were configured, and whatever the empty list means downstream (block
everything, or allow everything) now silently governs production.

## 🔍 The Offending Code

```csharp
// ALLOWEDHOSTS=payments.acme.com,billing.acme.com,mail.acme.com
var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

string[] allowed = config.GetSection("AllowedHosts").Get<string[]>() ?? []; // 💥 empty
```

## 🧠 What's Actually Going On

Configuration has no notion of a comma-separated list. An array is stored as
individual keys with numeric indices - `AllowedHosts:0`, `AllowedHosts:1`,
`AllowedHosts:2` - and the array binder reconstructs `string[]` by reading those
indexed children. A single environment variable named `ALLOWEDHOSTS` creates one
key, `AllowedHosts`, whose *value* is the entire string
`"payments.acme.com,billing.acme.com,mail.acme.com"`. That key has a scalar value
and **no indexed children**, so `Get<string[]>()` finds nothing to bind and
returns null; the `?? []` turns that into an empty array. The comma is just a
character inside one string - the binder never treats it as a separator.

The broken belief is "a list in an env var is comma-separated, like a `PATH`."
Every array-shaped setting the .NET configuration system reads comes from
*multiple keys*, one per element. For environment variables the hierarchy
separator `:` is spelled `__`, so the array lives in `ALLOWEDHOSTS__0`,
`ALLOWEDHOSTS__1`, `ALLOWEDHOSTS__2` - three variables, not one. Cram the whole
list into a single variable and the binder sees a scalar, hands you nothing, and
the failure is silent because "no indexed children" is not an error, it is just an
empty result.

## ✅ The Fix

Give each element its own indexed key. For environment variables that means one
variable per entry, suffixed `__0`, `__1`, `__2`.

```bash
ALLOWEDHOSTS__0=payments.acme.com
ALLOWEDHOSTS__1=billing.acme.com
ALLOWEDHOSTS__2=mail.acme.com
```

```csharp
string[] allowed = config.GetSection("AllowedHosts").Get<string[]>() ?? [];
// -> ["payments.acme.com", "billing.acme.com", "mail.acme.com"]
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| Indexed env keys (`KEY__0`, `KEY__1`, ...) | The canonical representation - each element is its own key, and it round-trips through every provider (JSON array, env, command line) the same way. |
| A JSON array in appsettings | The values live in a file rather than the environment - `"AllowedHosts": ["a", "b"]` binds to `string[]` directly, no indices to hand-write. |
| Split a scalar yourself, on purpose | You truly want one comma-joined variable - read it as a `string` and `value.Split(',')` explicitly, so the parsing is visible and intended, not assumed. |
| Validate the count on startup | Any array-from-config - assert a non-empty (or expected-size) result at boot, so a mis-shaped variable fails loudly instead of binding to `[]`. |

## 😈 The Even Worse Sibling

Here an empty allowlist is at least *conservative* - if the code reads "empty
means allow nothing," you get a loud outage rather than a breach. The dangerous
mirror is code that reads an empty allowlist as "no restrictions configured, allow
everything": the same silent binding failure now opens every outbound host instead
of closing them, and the security control you thought you shipped was never
loaded. And the partial version bites too - set `ALLOWEDHOSTS__0` and
`ALLOWEDHOSTS__2` but skip `__1`, and the binder stops at the first gap, silently
truncating the array to a single element; the list is not empty, so a count-based
sanity check passes, yet two of your three hosts are missing.

## 🎓 Advanced Nuance

- **`__` is the env spelling of `:`.** Environment-variable names cannot contain
  `:` on some shells, so the provider maps double underscore to the hierarchy
  separator. `ALLOWEDHOSTS__0` becomes the config key `AllowedHosts:0`; a single
  underscore does nothing special.
- **Indices must be contiguous from 0.** The array binder reads `:0`, `:1`, `:2`
  until the first missing index, then stops - a gap silently truncates the array
  rather than skipping the hole, so `:0` + `:2` yields a one-element array.
- **JSON arrays flatten to the same indexed keys.** `"AllowedHosts": ["a","b"]`
  in appsettings is stored internally as `AllowedHosts:0=a`, `AllowedHosts:1=b` -
  which is exactly why an env override of `AllowedHosts__1` replaces the second
  element in place rather than the whole array.

## 🔎 How to Find It in Your Codebase

- Grep for `.Get<string[]>()`, `.Get<List<`, and array/collection properties bound
  from configuration, then check how the corresponding value is set in the
  environment - a single comma-joined variable is this bug.
- Audit deployment surfaces (Kubernetes, Compose, CI variables) for env entries
  that pack a list into one comma- or semicolon-separated value where the code
  expects an array.
- Symptom-side: an array setting that binds to empty (or to one element) with no
  error, allowlists/recipient-lists/endpoint-lists that are silently blank in one
  environment, and config that works from `appsettings.json` (a real JSON array)
  but not from its env override.
- Prefer indexed keys or a JSON array; if a delimited scalar is unavoidable, split
  it explicitly in code and validate the resulting count at startup.
