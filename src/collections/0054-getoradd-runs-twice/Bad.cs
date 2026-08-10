// Exhibit #0054: GetOrAdd runs your factory more than once

using System.Collections.Concurrent;

// A per-tenant connection cache. GetOrAdd is meant to open each tenant's connection
// exactly once and reuse it. Under a concurrent burst for the same tenant, the factory
// runs more than once - and every connection but the winner's is silently discarded.

const int tenants = 1000;
const int racersPerTenant = 8;

var cache = new ConcurrentDictionary<int, Connection>();
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
            cache.GetOrAdd(id, t => // 💥 the "runs exactly once" factory - not atomic
            {
                Interlocked.Increment(ref connectionsOpened);
                return new Connection(t);
            });
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
        $"opened {connectionsOpened} connections for {tenants} tenants - ConcurrentDictionary.GetOrAdd does not " +
        "run the factory atomically: concurrent callers for a missing key all run it, and every result but the " +
        "winner's is discarded (here, a leaked connection)");
}

Console.WriteLine("Exactly one connection opened per tenant.");

sealed class Connection
{
    public int TenantId { get; }
    public Connection(int tenantId) => TenantId = tenantId;
}
