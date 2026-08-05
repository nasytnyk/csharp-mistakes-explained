// Exhibit #0036: eliding await inside a using block

// A data-access helper: open a connection, run a query, return the result.
// Someone "optimized" it by dropping a redundant await and returning the Task
// directly - a real, widely-recommended micro-optimization.

var gate = new TaskCompletionSource();

// The query round-trip: wait for the database, then read from the connection.
async Task<string> QueryAsync(FakeConnection conn, Task roundTrip)
{
    await roundTrip;                        // the network wait that suspends us
    return conn.Read();                     // touches the connection AFTER resuming
}

// The helper. Its 'using' owns the connection for the body of this method.
Task<string> GetCustomerName()
{
    using var conn = FakeConnection.Open();
    return QueryAsync(conn, gate.Task);     // 💥 no await: 'using' disposes conn the instant we return
}

Console.WriteLine("Dispatching query...");
var pending = GetCustomerName();            // returns now - and 'using' just disposed conn

Console.WriteLine("Database responds; the query resumes and reaches for the connection...");
gate.SetResult();                           // the round-trip completes; QueryAsync runs on

string name = await pending;                // ObjectDisposedException surfaces here
Console.WriteLine($"Customer: {name}");

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
