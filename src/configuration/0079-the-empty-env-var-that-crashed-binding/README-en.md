---
id: "0079"
title: the empty env var that crashed binding
category: configuration
tags: [configuration, environment-variables, binding]
rule: "never let a present-but-empty env var pass for 'unset'"
---

# #0079 - The Empty Env Var That Crashed Binding

## 💥 Symptom

The app runs everywhere - your machine, CI, staging - and then falls over on the
one deploy that set an environment variable to a blank value. Startup throws
`InvalidOperationException: Failed to convert configuration value at
'Worker:Retries' to type 'System.Int32'`, and nobody touched the number. The
`appsettings.json` still says `5`. The env var is "empty," which feels like "not
set" - but to configuration it is a real value of `""`, and `""` is not an `int`.

## 🔍 The Offending Code

```csharp
// appsettings default is 5; a deploy sets WORKER__Retries to a blank value
var config = new ConfigurationBuilder()
    .AddInMemoryCollection(new() { ["Worker:Retries"] = "5" })
    .AddEnvironmentVariables()
    .Build();

int retries = config.GetValue<int>("Worker:Retries"); // 💥 converting "" to int throws
```

## 🧠 What's Actually Going On

Configuration is a flat map of string keys to string values, layered by provider,
**last one wins**. An environment variable that exists but holds an empty string
is a present key with the value `""` - so the environment-variables provider,
which sits above your JSON defaults, overrides `Worker:Retries = "5"` with
`Worker:Retries = ""`. There is no concept of "blank means fall through to the
layer below": present is present. Then `GetValue<int>` (and `Bind`, and options
binding) tries to convert that `""` to `Int32`, fails, and throws
`InvalidOperationException` at startup.

The broken belief is "if I don't give the env var a real value, my
`appsettings.json` default is used." Setting a variable to empty is not the same
as leaving it unset. `export WORKER__Retries=` in a shell, an empty value in a
Kubernetes manifest, a CI variable defined but not filled in - all create a
present, empty override that shadows your default and then blows up the moment the
value is read as anything other than a string. The failure is a config-shape
problem wearing a code stack trace, which is why the investigation starts in the
wrong place.

## ✅ The Fix

Treat a blank value as "not provided": read the raw string, fall back to the
default when it is empty or whitespace, and only parse real text.

```csharp
var raw = config["Worker:Retries"];
int retries = string.IsNullOrWhiteSpace(raw) ? DefaultRetries : int.Parse(raw);
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it fits |
|---|---|
| Blank-check the raw string, then parse | A single value with a sane default - `IsNullOrWhiteSpace(raw) ? fallback : Parse(raw)` turns an empty override back into "unset." |
| Options + `Validate` with a clear message | A whole options object - validate on bind so a bad/blank value fails with "Worker:Retries must be a positive integer," not an opaque converter error. |
| `ValidateOnStart()` (fail fast, on purpose) | You *want* a bad config to stop the app - but with a message that names the setting, deliberately, instead of a converter stack trace deep in a request. |
| Fix the deployment, not just the code | The blank came from CI/manifest - unset the variable (or give it a real value) so it stops shadowing the default; code hardening and config hygiene are both needed. |

## 😈 The Even Worse Sibling

A startup crash is the *lucky* outcome - it is loud, immediate, and stops the bad
config from doing anything. The quiet sibling is the same blank override binding
to a type where `""` is a perfectly valid value. A blank `WORKER__Retries` bound
to a `string` gives you `""`, not your default - and now a downstream `int.Parse`
fails deep in a request, or an empty connection string points at nothing, or a
blank feature-flag string is treated as "off." Worse still, a blank override on a
value that *coerces* silently: an empty string bound where the code does
`raw ?? "default"` sees `""` (not null), keeps the empty, and sails past the
null-coalescing guard you thought protected you. The version that throws at
startup is the one you can find; the ones that flow an empty string into business
logic are the ones that page you at 3am.

## 🎓 Advanced Nuance

- **Empty is not absent, and absent is not empty.** A missing key returns
  `default(T)` from `GetValue<int>` (a silent `0`); a present-but-empty key
  returns `""` and *throws* on conversion. Same "I didn't set it" intent, two
  opposite failure modes - one silent, one loud.
- **`__` is the nesting separator for env vars.** `WORKER__Retries` maps to
  `Worker:Retries` because the environment-variables provider translates double
  underscores to the `:` hierarchy separator (colons are illegal in env names on
  some shells). The empty value overrides the nested key just as a JSON value
  would.
- **Provider order decides who wins.** `AddEnvironmentVariables()` after
  `AddJsonFile(...)` means env vars override JSON - including empty ones. Reorder
  and the blank would lose; but the standard host builder puts environment
  variables last on purpose, so in a real app the blank override wins.

## 🔎 How to Find It in Your Codebase

- Grep for `GetValue<`, `.Get<`, and `.Bind(` over settings that come from the
  environment, and ask what happens if that variable is present but empty - if
  the answer is "converter throws," it is this bug.
- Audit deployment surfaces: CI/CD variable definitions, Kubernetes/Compose env
  blocks, and shell profiles where `VAR=` (with nothing after the `=`) sets a
  real, empty value rather than leaving it unset.
- Symptom-side: startup `InvalidOperationException: Failed to convert
  configuration value at '...'` on exactly one environment, with the JSON default
  visibly correct; config that "works locally" but dies in the pipeline that
  templated a blank in.
- Normalize blanks to unset at the read boundary (or validate options on start
  with named messages), and keep deployment configs from defining variables they
  do not actually fill in.
