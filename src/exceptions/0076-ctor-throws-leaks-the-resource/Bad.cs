// Exhibit #0076: a throwing constructor leaks the resource it acquired.
//
// A pooled connection whose constructor takes a slot from the pool, then
// validates. Wrapped in `using`, so cleanup "cannot" be forgotten - yet the
// failure path leaks a slot every time.

Pool.Reset();

// A batch of connects; one host is invalid (empty).
string[] hosts = { "db-1", "", "db-2" };

foreach (var host in hosts)
{
    try
    {
        using var conn = new PooledConnection(host); // 💥 ctor takes a slot, THEN throws
        conn.Send("SELECT 1");                       //    -> conn was never assigned, using disposes nothing
    }
    catch (ArgumentException)
    {
        // invalid host - skip and move on
    }
}

Console.WriteLine($"Slots taken: {Pool.Taken}, returned: {Pool.Returned}");

// Self-audit: every slot taken must come back. A failed connect must not keep its slot.
if (Pool.Taken != Pool.Returned)
{
    throw new InvalidOperationException(
        $"leaked {Pool.Taken - Pool.Returned} pool slot(s): the constructor threw AFTER taking a slot, so the " +
        "`using` variable was never assigned and Dispose never ran - a failed connect holds its slot until the " +
        "pool is exhausted and healthy requests start timing out");
}

Console.WriteLine("Every slot returned.");

static class Pool
{
    public static int Taken, Returned;
    public static void Reset() { Taken = Returned = 0; }
}

sealed class PooledConnection : IDisposable
{
    public PooledConnection(string host)
    {
        Pool.Taken++;                                     // acquire a slot
        if (string.IsNullOrEmpty(host))                   // 💥 validate AFTER acquiring
            throw new ArgumentException("host is required", nameof(host));
        // ... open the socket to `host` ...
    }

    public void Send(string sql) { /* ... */ }

    public void Dispose() => Pool.Returned++;             // return the slot
}
