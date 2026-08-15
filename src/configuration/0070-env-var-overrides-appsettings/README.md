---
id: "0070"
title: an environment variable silently overrides appsettings
category: configuration
tags: [configuration, appsettings, environment-variables]
rule: "never assume **appsettings.json** is final - env vars override it"
---

# #0070 - An Environment Variable Silently Overrides appsettings

## 💥 Symptom

You change a value in `appsettings.json`, deploy, and nothing happens - the app runs on the old
number as if the file were never touched. No error, no warning, no log line. The setting is right
there in the JSON, the deploy succeeded, and the effective value is still whatever it was.
Somewhere, something you forgot about is winning.

## 🔍 The Offending Code

```csharp
var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")   // App:RateLimit = 100
    .AddEnvironmentVariables()         // 💥 App__RateLimit=10 lives here, and later providers win
    .Build();
// config.GetValue<int>("App:RateLimit") is 10, not 100
```

## 🧠 What's Actually Going On

`IConfiguration` is a *stack of providers*, and reading a key returns the value from the **last**
provider that has it. `AddJsonFile("appsettings.json")` then `AddEnvironmentVariables()` means
environment variables override the JSON - by design, so a deployment can set values without
editing files. The catch is that the override is total and silent: an environment variable named
`App__RateLimit` (note the `__`, which the provider maps to the `:` separator) sets
`App:RateLimit`, shadows the JSON, and nothing announces that it did. Your appsettings change is
applied - and then immediately overridden by a higher layer you were not looking at.

The broken belief is "the value is in appsettings.json, so that's the value." appsettings.json is
one layer near the bottom; environment variables, `appsettings.{Environment}.json`, User Secrets,
and command-line args all sit above it and win. The file you edited is real; it is just not the
last word.

## ✅ The Fix

Know the precedence, and when a change "does nothing," ask *which provider* actually supplies the
key - `GetDebugView()` prints the effective value and its source for every key:

```csharp
Console.WriteLine(config.GetDebugView()); // App:RateLimit=10 (EnvironmentVariablesProvider)
// then fix it where it actually lives: remove the stale env var, or set the value in the winning layer
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `config.GetDebugView()` | Diagnosing "my change did nothing" - it lists every key, its effective value, and the provider that won; the fastest way to find the shadowing layer. |
| Own each key in one layer | Decide where a setting lives - a default in appsettings, or a per-environment override - and don't duplicate it across layers you'll forget. |
| Keep the standard order on purpose | JSON, then env vars, then command line is the *intended* precedence (deploy-time and run-time override the checked-in default); rely on it deliberately, not by accident. |
| Log bound options at startup | Bind to an options object and log it once on boot - the effective numbers in the log make a silent override visible the moment the app starts. |

## 😈 The Even Worse Sibling

An env var is at least greppable once you think to look. The quieter overrides are the framework's
own layered defaults. `appsettings.{Environment}.json` is stacked on top of `appsettings.json`
automatically, so a value in `appsettings.Development.json` shadows the base file on your machine
and *vanishes* in production - the same key reads two different values depending on
`ASPNETCORE_ENVIRONMENT`, and neither file shows the other. `launchSettings.json` sets environment
variables that apply only under the IDE / `dotnet run`, so a setting behaves one way from the IDE
and another from the published app. And User Secrets are layered in for the Development environment
only, so a value that works on every developer's machine is simply absent in the deployed one.
Same mechanic - a higher provider wins - but the winning layer is invisible unless you already know
it exists.

## 🎓 Advanced Nuance

- **Environment variables use `__`, not `:`.** The `:` hierarchy separator is not legal in
  environment-variable names on every platform, so the provider translates a double underscore to
  `:` - `App__RateLimit` becomes `App:RateLimit`. Setting `App:RateLimit` as the variable name
  instead relies on non-portable behavior that fails on Windows.
- **The last provider wins per *key*, not per file.** Providers do not replace one another
  wholesale; each key resolves independently, so a partial override leaves some keys from the JSON
  and some from the env - a configuration that exists in no single source, assembled at runtime.
- **`reloadOnChange` re-reads the file, not the precedence.** Editing appsettings.json on a running
  host reloads it, but if a higher provider still supplies the key, the reload changes nothing
  visible - reinforcing "my edit did nothing" while the file genuinely did reload.

## 🔎 How to Find It in Your Codebase

- When a config change has no effect, call `configuration.GetDebugView()` (or dump it at startup)
  and read the provider named next to the key - the winning layer is right there.
- Grep your deployment for environment variables and `appsettings.{Environment}.json` files that
  set the same keys as your base `appsettings.json` - duplicated keys across layers are where
  silent overrides live.
- Symptom-side: a value correct in the file but wrong at runtime; a setting that differs between the
  IDE and the published app (launchSettings), or between Development and Production (env-specific
  files, User Secrets).
- Decide the owning layer for each setting, log bound options at startup, and treat
  `GetDebugView()` as the first stop whenever appsettings "doesn't take."
