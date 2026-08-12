---
id: "0059"
title: the stream you read from the end
category: io
tags: [IO, Stream, MemoryStream]
rule: "rewind before you read - a stream you just wrote is parked at the **end**, so `CopyTo` copies nothing"
---

# #0059 - The Stream You Read From the End

## 💥 Symptom

An upload succeeds and the file that lands is empty. You serialized a report into a
`MemoryStream`, handed it to the uploader - HTTP `StreamContent`, a blob client, a mail
attachment - every call returned success, no exception anywhere, and the body on the other side
is zero bytes. Locally the JSON is obviously in the buffer; you can print the length and see 44.
Yet what shipped is a well-formed nothing.

## 🔍 The Offending Code

```csharp
var buffer = new MemoryStream();
JsonSerializer.Serialize(buffer, report); // writes the JSON, leaves Position at the end
buffer.CopyTo(destination);               // 💥 copies from Position (the end) -> 0 bytes
```

## 🧠 What's Actually Going On

A stream has a single cursor, `Position`, shared by reads and writes. Writing advances it: after
`Serialize` (or `Write`, or a flushed `StreamWriter`) the cursor sits at the end of what you
wrote, so `Position == Length`. `CopyTo`, `ReadToEnd`, and every read start from the *current*
position and run to the end - and there is nothing between the end and the end. So the copy
transfers zero bytes and reports success, because copying nothing is a perfectly valid
operation.

The broken belief is "the stream holds my data, so reading it gives my data back." It holds the
data - the cursor is just parked past all of it. `Position = 0` is the entire fix, and the two
fields that would have told you, `Length` (44) versus `Position` (44), are sitting right there,
checked by nobody. Nothing throws because, from the stream's point of view, nothing is wrong:
you asked it to read from the end, and it faithfully read from the end.

## ✅ The Fix

Rewind before you read what you wrote:

```csharp
JsonSerializer.Serialize(buffer, report);
buffer.Position = 0;                       // or buffer.Seek(0, SeekOrigin.Begin)
buffer.CopyTo(destination);
```

Full version in [Good.cs](Good.cs); the mistake is [Bad.cs](Bad.cs).

| Approach | When it's the right call |
|---|---|
| `stream.Position = 0` before reading | The default for a build-then-read buffer you own - one line, and the intent ("I'm about to read from the start") is explicit. |
| `new MemoryStream(buffer.ToArray())` | Hand the consumer a fresh stream already at 0 - costs a copy, but the producer cannot forget to rewind. |
| Serialize to `byte[]` / `string` | If you don't actually need a stream, `JsonSerializer.SerializeToUtf8Bytes` has no cursor to mismanage. |
| HTTP: `ByteArrayContent` / `JsonContent.Create` | Prefer content types that don't depend on a caller-managed cursor; if you must use `StreamContent`, set `Position = 0` first. |

## 😈 The Even Worse Sibling

The bug is invisible to every refactor that would expose it, so it hides from bisection. Switch
the buffer to a serialize-to-`string` and there is no cursor - it "works," so the serialization
change looks like the fix. Wrap the buffer in a fresh stream, or read it back in another method
that gets a new stream, and the position resets - it "works" there too. So a `git bisect` blames
the commit that touched serialization, never the `Position` nobody sees, and the change that
makes one path pass leaves every other build-then-send path broken. Worse, the empty result is a
*success*: an empty upload, an empty attachment, an empty log entry - the producer logged "sent,"
the consumer stored a valid empty object, and the loss surfaces only later, as the absence of
something that should have been there.

## 🎓 Advanced Nuance

- **It is not only `MemoryStream`.** Any seekable stream shares one cursor: write to a
  `FileStream` and read it back on the *same* handle without rewinding and you read past your own
  bytes - the classic "wrote the file, read it back empty" is this exact cursor, on disk.
- **`Flush` is not `Position = 0`.** When a read comes back empty, people reach for `Flush()` -
  it pushes buffered bytes to the underlying store but does not move the cursor, so the read
  still starts from the end. Flush and rewind solve different problems.
- **`Length` and `Position` are the free diagnostic.** After a write, `Position == Length`;
  before a correct read, `Position == 0`. A one-line `Position == 0` check at the top of any "now
  read it back" path catches this whole class of bug and costs nothing.
- **The write side does not always leave you at the very end.** A partial seek during writing, or
  a buffered `BinaryWriter`, can park the cursor mid-stream. "Rewind to 0" is right when you want
  the whole thing; otherwise be explicit about where the read starts - never assume it is where
  you think you left it.

## 🔎 How to Find It in Your Codebase

- Grep for a `new MemoryStream()` that gets written or serialized into and then handed onward -
  `CopyTo`, `StreamContent`, a client's `Upload(stream)` - with no `Position = 0` / `Seek(0, ...)`
  in between.
- Any `JsonSerializer.Serialize(stream, ...)` or `StreamWriter` write immediately followed by a
  read on the same stream is the shape; the rewind is the missing line.
- Symptom-side: empty HTTP bodies, 0-byte blob/S3 uploads, empty mail attachments, zip entries
  with no content - all reported as success end to end.
- Add `Debug.Assert(stream.Position == 0)` at the entry of read-back helpers, or prefer
  `byte[]`-based APIs (`ByteArrayContent`, `SerializeToUtf8Bytes`) so there is no shared cursor to
  leave in the wrong place.
