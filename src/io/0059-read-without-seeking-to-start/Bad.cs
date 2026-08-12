// Exhibit #0059: a stream you just wrote is parked at the end - read it back and get nothing

#:property PublishAot=false

using System.Text.Json;

// Export a report to JSON in memory, then "upload" it by copying the stream to the destination
// (an HTTP body, a blob, a mail attachment - all "hand this stream to someone" flows).
var report = new Report(Id: 1002, Customer: "Acme", Total: 149.50m);

var buffer = new MemoryStream();
JsonSerializer.Serialize(buffer, report); // writes the JSON - and leaves Position at the end

long writtenBytes = buffer.Length;
Console.WriteLine($"Wrote {writtenBytes} bytes; stream Position is {buffer.Position}");

var destination = new MemoryStream();
long readFrom = buffer.Position;
buffer.CopyTo(destination); // 💥 copies from the current Position (the end) - nothing follows it

Console.WriteLine($"Uploaded {destination.Length} bytes");

// Self-audit: the upload must carry the JSON we just wrote, not an empty body.
if (destination.Length != writtenBytes)
{
    throw new InvalidOperationException(
        $"wrote {writtenBytes} bytes but uploaded {destination.Length} - CopyTo started from Position " +
        $"{readFrom} (the end of what we wrote), so it copied nothing; the buffer needed Position = 0 first");
}

Console.WriteLine("Upload carried the full report.");

record Report(int Id, string Customer, decimal Total);
