---
id: "0049"
title: A record's ToString leaks its secrets
category: records
tags: [records, ToString, logging]
author: palkotnyk
rule: "never put a **secret** in a record - its `ToString` prints every member"
---

# #0049 - A Record's ToString Leaks Its Secrets

## 💥 Symptom

A security review finds passwords, API tokens, and PII sitting in plaintext in the
logs - and in exception messages, and in the error tracker. Nobody wrote
`log.Info(password)`. The code logs a whole `Credentials` object, the object is a
`record`, and its generated `ToString` prints every field. The record started
innocent - a `Credentials(User)` DTO - and someone later added a `Password` field.
That one-line change, waved through review, is where the leak began.

## 🔍 The Offending Code

```csharp
record Credentials(string User, string Password, string ApiToken);

logger.LogInformation("Login attempt: {Creds}", creds); // 💥 or $"{creds}" - both call the generated ToString
// -> Login attempt: Credentials { User = alice, Password = hunter2-super-secret, ApiToken = tok_live_abc123 }
```

## 🧠 What's Actually Going On

A record synthesizes a `ToString` that emits **all** of its public properties *and
public fields*, in `Type { A = .., B = .. }` form. Every `$"{creds}"`, every
`logger.LogInformation("{Creds}", creds)`, and every exception message that includes
the object calls it. Nothing in the record marks a member as sensitive - the
generated `ToString` has no notion of secrets, so it prints them exactly like the
username.

That means the leak is not a mistake at the log site; it is baked into the *type*
the moment a secret becomes a member. Adding `string Password` to the record
compiles clean and reviews clean - the log statement still reads
`LogInformation("...", creds)`, with no secret in sight - and the plaintext only
appears at runtime, in the rendered line. The broken belief is "I didn't log the
password." You logged the object, and the object volunteered it. It is the object
half of [0032-interpolated-log-loses-everything](../../logging/0032-interpolated-log-loses-everything/):
there logging an interpolated string dropped the structured fields; here logging a
record adds fields you never meant to include.

## ✅ The Fix

Keep the secret out of what `ToString` renders. A record hands you a hook,
`PrintMembers`, that controls exactly what goes between the braces:

```csharp
record Credentials(string User, string Password, string ApiToken)
{
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"User = {User}, Password = ***, ApiToken = ***");
        return true;
    }
}
// -> Credentials { User = alice, Password = ***, ApiToken = *** }
```

Full version in [Good.cs](Good.cs). Choosing the guard:

| Approach | When it's the right call |
|---|---|
| Override `PrintMembers(StringBuilder)` to redact the secret members | Keep the record and its `Type { .. }` framing, control exactly what is emitted |
| Override `ToString()` entirely | You want full control of the whole string |
| Keep secrets out of the record - a separate holder the logger never sees | The real fix - a secret should not live in a DTO that gets logged or serialized wholesale |
| Log named, chosen fields - `"{User}"`, never `"{Creds}"` | Structured logging: never hand a whole object with secrets to the logger |

## 😈 The Even Worse Sibling

The redaction does not travel, but the leak does. `PrintMembers` is
`protected virtual`, so a **derived** record re-leaks unless it *also* overrides it -
add a `AdminCredentials : Credentials` subtype and the careful redaction on the base
is silently bypassed. Every `with`-copy carries the secret into its own `ToString`, a
**nested** record (`Credentials` inside a bigger `Request` record) leaks through the
outer object's `ToString`, and none of it is only about logs: the same synthesized
string feeds debugger displays, test-assertion failure messages, and any error
tracker that stringifies the object - so the token lands in Sentry or Application
Insights payloads you never wrote a log line for. One member, many exits, and the
redaction guards exactly one of them.

## 🎓 Advanced Nuance

`PrintMembers` is the real seam. On a non-sealed record class its signature is
`protected virtual bool PrintMembers(StringBuilder)`; on a sealed record it is
`private`. It appends the `A = .., B = ..` middle and returns whether anything was
written (that return value controls the spacing); `ToString` wraps its output in
`Type { ... }`, which is why overriding `PrintMembers` keeps the record framing while
overriding `ToString` replaces it. It prints public **fields** as well as properties,
so a plain public field secret leaks identically - there is no accessor to hang an
attribute on.

And there is no attribute the record's `ToString` honors. `Microsoft.Extensions.Compliance`
ships data-classification and redaction, but that is for the *logging* pipeline; the
record's own generated `ToString` ignores every `[Sensitive]`-style marker, because
the type simply does not know which member is a secret. You have to tell it, in
`PrintMembers`, or keep the secret out of the record entirely.

## 🔎 How to Find It in Your Codebase

- Grep for records and DTOs whose members include `Password`, `Secret`, `Token`,
  `ApiKey`, `Pin`, `Ssn`, `ConnectionString`, or similar, and check whether they
  override `PrintMembers` / `ToString`. Any that do not are one interpolation away
  from a leak.
- Grep for whole-object logging: `Log*(..., obj)` and `$"{obj}"` / `{obj}` where
  `obj` is a record carrying a secret. Prefer logging named fields you chose.
- No analyzer flags secret-in-`ToString`; some logging analyzers warn on logging
  non-primitive objects, which is a useful proxy. Treat it as a review rule.
- The moment to add redaction is the commit that adds a sensitive field to a record -
  that PR, not a later incident, is where this is cheap to stop.
