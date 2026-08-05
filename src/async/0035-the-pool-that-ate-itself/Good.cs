// Exhibit #0035: the fix

using System.Collections.Concurrent;

// A currency service. Reports need today's USD rate; the reporting layer is
// asynchronous too, so it awaits the FX call instead of blocking on it.

// Pin the pool to stand in for a server already at its ceiling. In production
// the pool is huge, but under a request burst every worker is busy - this pin
// reproduces that saturation deterministically on a laptop (see README).
ThreadPool.SetMinThreads(4, 4);
ThreadPool.SetMaxThreads(4, 4);

var rates = new RateService();
var done = new ConcurrentQueue<int>();

// A burst of eight report requests arrives. Each grabs the rate and records it.
var reports = Enumerable.Range(1, 8)
    .Select(id => Task.Run(async () =>
    {
        decimal rate = await rates.GetUsdRateAsync(); // await frees the pool thread
        done.Enqueue(id);
    }))
    .ToArray();

// Awaiting never parks a worker: each Task.Delay hands its thread back to the
// pool, so all eight overlap on four threads and finish in about 100 ms.
await Task.WhenAll(reports);

Console.WriteLine($"Reports finished: {done.Count} of {reports.Length}.");

if (done.Count != reports.Length)
{
    throw new InvalidOperationException(
        $"reported done with only {done.Count} of {reports.Length} reports");
}

Console.WriteLine("Every report got its rate. As it should be.");

sealed class RateService
{
    // The real FX call: an async round-trip, here a 100 ms network stand-in.
    public async Task<decimal> GetUsdRateAsync()
    {
        await Task.Delay(100); // the continuation after this needs a pool thread
        return 41.75m;
    }
}
