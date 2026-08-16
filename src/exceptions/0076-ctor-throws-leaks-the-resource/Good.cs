// Exhibit #0076 - the fix: an exception-safe constructor.
//
// The constructor still acquires a slot before it can throw - but it now
// releases that slot if anything after the acquire fails, before letting the
// exception propagate. The caller-side code is byte-for-byte identical to Bad.cs.

Pool.Reset();

string[] hosts = { "db-1", "", "db-2" };

foreach (var host in hosts)
{
    try
    {
        using var conn = new PooledConnection(host);
        conn.Send("SELECT 1");
    }
    catch (ArgumentException)
    {
        // invalid host - skip and move on
    }
}

Console.WriteLine($"Slots taken: {Pool.Taken}, returned: {Pool.Returned}");

if (Pool.Taken != Pool.Returned)
{
    throw new InvalidOperationException(
        $"leaked {Pool.Taken - Pool.Returned} pool slot(s)");
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
        try
        {
            if (string.IsNullOrEmpty(host))
                throw new ArgumentException("host is required", nameof(host));
            // ... open the socket to `host` ...
        }
        catch
        {
            Dispose();                                    // release the slot we took, then rethrow
            throw;
        }
    }

    public void Send(string sql) { /* ... */ }

    public void Dispose() => Pool.Returned++;
}
