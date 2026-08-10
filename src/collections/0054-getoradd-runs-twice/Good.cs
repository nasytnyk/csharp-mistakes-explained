// Exhibit #0054: the fix

using System.Collections.Concurrent;

// The same per-tenant connection cache - but the value is a Lazy<Connection>. GetOrAdd may
// still build several Lazy wrappers under contention, but only the winner's .Value is ever
// evaluated, so the expensive factory runs exactly once per tenant.

const int tenants = 1000;
const int racersPerTenant = 8;

var cache = new ConcurrentDictionary<int, Lazy<Connection>>();
int connectionsOpened = 0;

for (int tenant = 0; tenant < tenants; tenant++)
{
    // Release a burst of racers onto the same (still missing) key at the same instant.
    using var gate = new Barrier(racersPerTenant);
    var racers = new Thread[racersPerTenant];
    for (int i = 0; i < racersPerTenant; i++)
    {
        int id = tenant;
        racers[i] = new Thread(() =>
        {
            gate.SignalAndWait();
            _ = cache.GetOrAdd(id, t => new Lazy<Connection>(() => // only the winning Lazy is evaluated
            {
                Interlocked.Increment(ref connectionsOpened);
                return new Connection(t);
            })).Value;
        });
        racers[i].Start();
    }
    foreach (var r in racers) r.Join();
}

Console.WriteLine($"Tenants: {tenants}, connections opened: {connectionsOpened}");

// Self-audit: exactly one connection per tenant should ever be opened.
if (connectionsOpened != tenants)
{
    throw new InvalidOperationException(
        $"opened {connectionsOpened} connections for {tenants} tenants");
}

Console.WriteLine("Exactly one connection opened per tenant. As it should be.");

sealed class Connection
{
    public int TenantId { get; }
    public Connection(int tenantId) => TenantId = tenantId;
}
