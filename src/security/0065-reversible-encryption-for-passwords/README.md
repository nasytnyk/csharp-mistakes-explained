---
id: "0065"
title: encrypting passwords instead of hashing them
category: security
tags: [security, cryptography, passwords]
rule: "never encrypt a password - encryption is **reversible**; hash it with a salted KDF"
---

# #0065 - Encrypting Passwords Instead of Hashing Them

## 💥 Symptom

A password store looks responsible - nothing is in plaintext, every value is AES-256 ciphertext.
Then the database leaks, and the config (or the key-vault reference, or the `appsettings` in the
same repo) leaks with it, and every password is recovered in a single pass. "Encrypted at rest"
turned out to mean "one key away from plaintext," and that key travels with the app.

## 🔍 The Offending Code

```csharp
var stored = Encrypt(password, key); // 💥 reversible - Decrypt(stored, key) == password
// the key lives in config, ships in the deploy, sits in the same backup as the data
```

## 🧠 What's Actually Going On

Encryption is *reversible by design*: it exists so that whoever holds the key can get the
plaintext back. That is precisely the wrong property for a password. You never need to read a
stored password back - you only need to check whether a login attempt matches - so storing
something reversible adds nothing but a single point of total failure: the key. And the key is
rarely as protected as you picture; it lives in the same config, the same secret store, the same
nightly backup as the data it "protects," so a breach that reaches the passwords usually reaches
the key in the same step.

A password should be stored as a **hash**: a one-way function that can *verify* a guess (re-hash
it, compare) but cannot be *inverted* to reveal the original. And not a fast hash - a slow,
salted, purpose-built KDF (PBKDF2, bcrypt, scrypt, Argon2), so that even a leaked hash table
resists offline guessing. The broken belief is "encrypted is safer than plaintext, so we're
fine." Encrypted is safer than plaintext and far weaker than hashed - it keeps a door that
hashing removes entirely.

## ✅ The Fix

Hash with a salted, iterated KDF; verify by re-hashing, never by decrypting:

```csharp
byte[] salt = RandomNumberGenerator.GetBytes(16);
byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations: 100_000, HashAlgorithmName.SHA256, 32);
// store salt + hash; to check a login, re-derive with the same salt and FixedTimeEquals the result
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| PBKDF2 (`Rfc2898DeriveBytes.Pbkdf2`) | In-box, no packages - a solid default; use a high iteration count and a per-user random salt. |
| bcrypt / scrypt / Argon2 (a library) | Stronger, memory-hard resistance to GPU cracking - prefer Argon2id for new systems; needs a package. |
| ASP.NET Core Identity `IPasswordHasher` | You're already in that stack - it does salted, versioned PBKDF2 for you; don't hand-roll. |
| Real encryption (AES-GCM) | For data you must *read back* - documents, tokens, PII you display again. A password is never in this category. |

## 😈 The Even Worse Sibling

Reversible storage is bad; reversible storage that *looks* one-way is worse. Base64 and hex are
encodings, not encryption - a stored value that is just `Convert.ToBase64String(passwordBytes)`
reads as opaque gibberish in the database and decodes to plaintext in one line, yet sails past the
eyeball test that "it isn't plaintext." And plain, unsalted, fast `SHA-256` is the mirror trap
from the other side: it can't be decrypted, so it *feels* safe, but it's fast enough to brute-force
billions of guesses a second, and with no salt identical passwords share a hash - so a rainbow
table or a single crack breaks every account that reused that password. Encoding pretends to
protect and doesn't; a fast unsalted hash refuses to decrypt yet still folds under offline
guessing. "Not plaintext" was never the bar. Same instinct as
[0049-record-tostring-leaks-secrets](../../records/0049-record-tostring-leaks-secrets/): a value
that looks handled while the secret is one trivial step away.

## 🎓 Advanced Nuance

- **You never need the password back - that is the tell.** Any design that decrypts a stored
  password ("to email it to the user," "to compare on login") is reversible by requirement. A
  password reset issues a *new* secret; it never recovers the old one. If a flow needs the
  plaintext, the flow is the bug.
- **Salt defeats precomputation; iterations defeat speed.** A per-user random salt makes rainbow
  tables useless and stops equal passwords from sharing a hash; a high iteration count (or a
  memory-hard KDF) makes each guess expensive. You need both - a salted but single-round hash
  still cracks fast.
- **Verify in constant time.** Compare the derived hash with `CryptographicOperations.FixedTimeEquals`,
  not `==` / `SequenceEqual` - an early-exit comparison leaks, through timing, how many leading
  bytes matched. Here it costs nothing to do right.

## 🔎 How to Find It in Your Codebase

- Grep for `Encrypt` / `Aes` / `ProtectedData` applied to anything named `password` / `pwd` /
  `secret` that represents a user credential - a password you can decrypt is the shape.
- Look for a "recover"/"decrypt password" path, or a login that decrypts the stored value and
  `==`-compares - authentication should re-hash the attempt, never reverse the stored one.
- Flag `Convert.ToBase64String` / hex on a credential (encoding masquerading as protection) and
  bare `SHA256` / `MD5` / `SHA1` on a password with no salt or iteration (a fast hash).
- Prefer a framework password hasher (ASP.NET Core Identity) or PBKDF2 / Argon2 with a random salt
  and a high work factor; store the salt and parameters alongside the hash so you can raise the
  cost later.
