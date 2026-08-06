---
id: "0048"
title: System.Random for security tokens
category: security
tags: [security, Random, RandomNumberGenerator]
rule: "never build a **security token** with `System.Random`"
---

# #0048 - System.Random for Security Tokens

## 💥 Symptom

Account-takeover reports with no brute force in the logs. Attackers are resetting
other people's passwords, redeeming invite codes that were never sent to them,
walking straight past tokens that "look random" - 32 hex characters, high entropy
at a glance. The token generator uses `System.Random`, and the attacker did not
break it; they reproduced it, by running the same line of code the server did.

## 🔍 The Offending Code

```csharp
var rng = new Random(seed);   // seed derived from a timestamp / id / counter
var bytes = new byte[16];
rng.NextBytes(bytes);         // 💥 the "random" token is a pure function of `seed`
return Convert.ToHexString(bytes);
```

## 🧠 What's Actually Going On

`System.Random` is a **deterministic** pseudo-random generator. The seed fully
determines the entire output sequence: same seed, same "random" bytes, on every
machine, forever - that reproducibility is a feature, meant for simulations,
sampling, and games, and it is exactly what makes it unfit for secrets. A token
built from `new Random(seed)` is not a secret; it is a public function of the seed.

So the attacker does not need to break any cryptography, because there is none. All
they need is the seed - and seeds are guessable. `new Random(accountCreatedAt)`,
`new Random(Environment.TickCount)`, `new Random(userId)`, `new Random(requestNo)`
each pin the token to a value the attacker can read off a profile page or
brute-force across a small window. They run `new Random(sameSeed)`, call
`NextBytes`, and get your server's token byte for byte. The broken belief is that
"random-looking" means "unpredictable". `System.Random` is neither secret nor
cryptographic; the fix is the generator that is - `RandomNumberGenerator` - and the
class name is essentially the entire fix.

## ✅ The Fix

Use the cryptographic RNG for anything an attacker must not predict. It is
unseeded and unpredictable by construction:

```csharp
var bytes = RandomNumberGenerator.GetBytes(16); // cryptographically secure
return Convert.ToHexString(bytes);
```

Full version in [Good.cs](Good.cs). Which generator, for what:

| Use | Reach for |
|---|---|
| Tokens, keys, salts, nonces, temp passwords, OTPs, session ids | `RandomNumberGenerator` (`GetBytes`, `GetInt32`) - unpredictable, no seed to leak |
| Simulations, sampling, shuffles, jitter, procedural content, test data | `System.Random` / `Random.Shared` - fast, and its reproducibility is a feature here |
| A unique-but-not-secret id | A GUID for uniqueness - but never treat "unique" as "unguessable"; a secret still needs the crypto RNG |

## 😈 The Even Worse Sibling

It looks completely fine. The output is high-entropy hex, it passes every glance,
it logs like a real token, and it only reproduces if you feed the same seed - so no
test that generates a single token ever notices, and even one that generates two
will not compare them. Meanwhile the seed does not have to be a timestamp to be
weak: seed from a per-process counter, a user id, or `TickCount` and you have
narrowed the token to a brute-forceable set. The attacker never needs your source
code, because the algorithm is public and identical on every runtime; they need the
seed, and it is sitting in a "Created" field on the account. A security control that
passes review, passes tests, and hands the attacker a deterministic answer.

## 🎓 Advanced Nuance

An honesty note, because the folklore overshoots: modern parameterless
`new Random()` is **not** simply time-seeded - since .NET Core it draws a strong,
per-instance seed, so "just call `new Random()`" is a smaller hole than the classic
`new Random(timestamp)`. But it does not matter, because the *algorithm* is the
problem, not the seeding: `System.Random` is a public, non-cryptographic PRNG whose
internal state can be reconstructed from a handful of observed outputs, after which
every future value is predictable. So even an unguessable seed does not save a token
stream an attacker can watch. `Random.Shared` inherits all of this. And `Guid.NewGuid()`
is version-4 random but is **not** documented as cryptographically secure across
implementations - fine for uniqueness, wrong for secrecy. For anything an adversary
must not predict, there is exactly one right tool, and it is `RandomNumberGenerator`.

## 🔎 How to Find It in Your Codebase

- Grep for `new Random(`, `Random.Shared`, and the obsolete `RNGCryptoServiceProvider`
  anywhere near `token`, `secret`, `key`, `password`, `salt`, `nonce`, `otp`,
  `code`, `session`, or `reset`. `System.Random` in any authentication or
  authorization path is the finding.
- Enable **CA5394** ("Do not use insecure randomness") - it flags `System.Random`
  in security-sensitive code, but it is off by default, so turn the security rules
  on and treat it as an error.
- The fix is almost always a one-line swap to `RandomNumberGenerator.GetBytes` /
  `GetInt32`; the hard part is spotting the call, not fixing it.
- In review, treat "random" and "unpredictable" as different claims. Ask of any
  security value: could an attacker who knows the inputs reproduce this? For
  `System.Random`, the answer is always yes.
