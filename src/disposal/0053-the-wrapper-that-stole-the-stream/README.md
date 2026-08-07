---
id: "0053"
title: The wrapper that stole the stream
category: disposal
tags: [disposal, streams, IDisposable]
rule: "never let a **wrapper** own a stream you still need - pass `leaveOpen: true`"
---

# #0053 - The Wrapper That Stole the Stream

## 💥 Symptom

A two-pass read over one stream throws `ObjectDisposedException` on the second pass -
"Cannot access a closed Stream" - and the stack trace points at your own
`stream.Position = 0`, a line that could not possibly have closed anything. Between the
two passes sits an innocent helper that "just reads the header." It looks
textbook-correct in review. It also quietly closed the stream out from under you.

## 🔍 The Offending Code

```csharp
string header = ReadHeader(upload);   // wraps `upload` in a StreamReader, reads one line
upload.Position = 0;                    // 💥 ObjectDisposedException

static string ReadHeader(Stream stream)
{
    using var reader = new StreamReader(stream); // owns `stream` - disposing the reader disposes it
    return reader.ReadLine() ?? "";
}
```

## 🧠 What's Actually Going On

`StreamReader` - like `StreamWriter`, `BinaryReader`/`BinaryWriter`, `GZipStream`,
`CryptoStream`, and most stream wrappers - **takes ownership of the inner stream by
default**. Disposing the wrapper cascades inward: its `Dispose` calls `Dispose` on the
stream you handed it. The `using` in `ReadHeader` is doing exactly what it should -
releasing the reader - and in doing so it releases *your* stream too, because the reader
considers it its own.

Ownership transfer is the default, and the one knob that prevents it - `leaveOpen: true` -
lives at the very end of the longest constructor overload, so nobody reaches it by
accident. The helper's author saw "wrap a stream, read a line, dispose the reader" and
wrote the obvious, correct-looking code. The caller kept using a stream that a method it
called had already closed. Nothing in either signature hints that passing a stream to
`StreamReader` is handing over its life.

## ✅ The Fix

Tell the wrapper it does not own the stream - `leaveOpen: true`:

```csharp
static string ReadHeader(Stream stream)
{
    using var reader = new StreamReader(stream, Encoding.UTF8,
        detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
    return reader.ReadLine() ?? "";
}
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `leaveOpen: true` on the wrapper | The default answer whenever the wrapped stream outlives the wrapper - a caller's stream, a two-pass read, a stream you keep using. |
| Let the wrapper own it | The wrapper *is* the stream's whole life - you created the stream only to feed this reader/writer and never touch it again. Ownership transfer is then a feature. |
| Whoever creates the stream disposes it | State it explicitly: the method that `new`s the stream `using`s it; helpers that borrow it never dispose (and pass `leaveOpen`). |
| `File.ReadLines` / `File.ReadAllText` | If the source is a path, skip the manual wrapper - the framework opens and closes its own stream. |

## 😈 The Even Worse Sibling

On the write path, `GZipStream` welds two behaviors into one `Dispose`: flushing the
compression trailer that *finalizes* the archive, and *closing* the output stream. Forget
`leaveOpen` and disposing the wrapper closes the `FileStream` you were writing to; skip
disposing the wrapper to keep the stream open and the archive is never finalized - a
truncated, corrupt gzip. Through the default constructor the two are inseparable, so the
fix for "my output stream got closed" reintroduces "my archive is corrupt," and back
again. `leaveOpen: true` - dispose the wrapper to flush the trailer, keep the stream open -
is the only spelling that gets both right.

## 🎓 Advanced Nuance

- **This is [0026-dispose-what-you-dont-own](../../disposal/0026-dispose-what-you-dont-own/)
  from the other side.** There, *your* code disposed a dependency it was handed and should
  not have; here a *library* type disposes a stream it was handed and did not create. The
  same ownership axiom - "dispose only what you own" - broken by the wrapper instead of by
  you.
- **The default is not universal, and neither is the parameter position.** `StreamReader`
  and `StreamWriter` default to owning; `CryptoStream`, `DeflateStream`, and `GZipStream`
  each expose `leaveOpen` in a different overload slot; `HttpClient` spells the same idea
  `disposeHandler`. Check each wrapper's ownership rather than assuming a house rule.
- **Double-dispose is usually fine, which is why this hides.** `MemoryStream.Dispose` is
  idempotent, so a `using` on *both* the wrapper and the stream never throws - the bug only
  bites when something touches the stream *between* the wrapper's dispose and the outer
  one, which is exactly the two-pass pattern. A single-pass helper that wraps, reads, and
  returns looks identical and never fails.

## 🔎 How to Find It in Your Codebase

- Grep for `new StreamReader(` / `new StreamWriter(` / `new GZipStream(` / `new BinaryReader(`
  whose argument is a `Stream` parameter or field the method did not create - those are
  borrowed streams that need `leaveOpen: true`.
- Look for a stream used *after* a helper call or a `using` block wrapped around it: a
  reader/writer disposed, then the original stream rewound (`Position = 0`), read, or
  written again.
- Any method taking a `Stream` parameter and wrapping it in a `using`d reader/writer
  without `leaveOpen` is disposing its caller's resource - a contract most callers will not
  expect.
- No analyzer flags cross-ownership disposal; CA2000 ("dispose objects before losing
  scope") actually pushes you *toward* the `using` that triggers this. Treat "who owns this
  stream" as a review question at every wrapper.
