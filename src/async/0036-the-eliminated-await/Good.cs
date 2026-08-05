// Exhibit #0036: the fix

// The same helper - but it awaits the query instead of returning the bare Task,
// so the 'using' scope stays alive until the read is done.

var gate = new TaskCompletionSource();

// The query round-trip: wait for the database, then read from the connection.
async Task<string> QueryAsync(FakeConnection conn, Task roundTrip)
{
    await roundTrip;                        // the network wait that suspends us
    return conn.Read();                     // touches the connection AFTER resuming
}

// The helper is async and awaits, so 'using' owns the connection until the query returns.
async Task<string> GetCustomerName()
{
    using var conn = FakeConnection.Open();
    return await QueryAsync(conn, gate.Task); // await keeps the 'using' scope alive to here
}

Console.WriteLine("Dispatching query...");
var pending = GetCustomerName();            // returns a Task; conn stays open inside it

Console.WriteLine("Database responds; the query resumes and reaches for the connection...");
gate.SetResult();                           // the round-trip completes; QueryAsync runs on

string name = await pending;                // reads from a live connection
Console.WriteLine($"Customer: {name}");
Console.WriteLine("Connection was still open when the query read from it. As it should be.");

sealed class FakeConnection : IDisposable
{
    private bool _disposed;
    public static FakeConnection Open() => new();

    public string Read()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return "Ada Lovelace";
    }

    public void Dispose() => _disposed = true;
}
