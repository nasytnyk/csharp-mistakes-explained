// Exhibit #0059: the fix

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
buffer.Position = 0;        // rewind before reading it back - reads start from the beginning
long readFrom = buffer.Position;
buffer.CopyTo(destination); // now copies every byte we wrote

Console.WriteLine($"Uploaded {destination.Length} bytes");

// Self-audit: the upload must carry the JSON we just wrote, not an empty body.
if (destination.Length != writtenBytes)
{
    throw new InvalidOperationException(
        $"wrote {writtenBytes} bytes but uploaded {destination.Length}");
}

Console.WriteLine("Upload carried the full report. As it should be.");

record Report(int Id, string Customer, decimal Total);
