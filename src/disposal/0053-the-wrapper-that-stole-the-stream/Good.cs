// Exhibit #0053: the fix

// The same two-pass CSV import - but the header helper tells its StreamReader NOT to
// take ownership of the caller's stream, so the stream survives for the second pass.

using System.Text;

var csv = "id,name\n1,Ada\n2,Grace\n";
using var upload = new MemoryStream(Encoding.UTF8.GetBytes(csv));

string header = ReadHeader(upload);
Console.WriteLine($"Header line: {header}");

// Second pass: rewind to the top and read the body.
upload.Position = 0;
string body = new StreamReader(upload).ReadToEnd();
int rows = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

Console.WriteLine($"Lines read on the second pass: {rows}");

if (rows != 3)
{
    throw new InvalidOperationException($"expected 3 lines on the second pass, read {rows}");
}

Console.WriteLine("Import read the full upload. As it should be.");

// A helper that just reads the first line - wrapping the caller's stream to do it.
static string ReadHeader(Stream stream)
{
    // leaveOpen: true - read the line but do NOT take ownership of the caller's stream.
    using var reader = new StreamReader(
        stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
    return reader.ReadLine() ?? "";
}
