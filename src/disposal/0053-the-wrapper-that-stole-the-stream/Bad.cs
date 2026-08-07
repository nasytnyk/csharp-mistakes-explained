// Exhibit #0053: the wrapper that stole the stream

// A CSV import makes two passes over one caller-owned stream: peek the header line
// to detect the format, then rewind and read the whole body.

using System.Text;

var csv = "id,name\n1,Ada\n2,Grace\n";
using var upload = new MemoryStream(Encoding.UTF8.GetBytes(csv));

string header = ReadHeader(upload);
Console.WriteLine($"Header line: {header}");

// Second pass: rewind to the top and read the body.
upload.Position = 0; // 💥 ObjectDisposedException - ReadHeader's StreamReader disposed our stream
string body = new StreamReader(upload).ReadToEnd();
int rows = body.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;

Console.WriteLine($"Lines read on the second pass: {rows}");

if (rows != 3)
{
    throw new InvalidOperationException($"expected 3 lines on the second pass, read {rows}");
}

Console.WriteLine("Import read the full upload.");

// A helper that just reads the first line - wrapping the caller's stream to do it.
static string ReadHeader(Stream stream)
{
    using var reader = new StreamReader(stream); // owns `stream`; disposing the reader disposes it too
    return reader.ReadLine() ?? "";
}
