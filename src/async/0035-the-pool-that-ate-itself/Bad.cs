// Exhibit #0035: blocking on async code with .Result under a busy thread pool

using System.Collections.Concurrent;

// A currency service. Reports need today's USD rate; the reporting layer is
// synchronous and "just needs the number", so it blocks on the async FX call.

// Pin the pool to stand in for a server already at its ceiling. In production
// the pool is huge, but under a request burst every worker is busy - this pin
// reproduces that saturation deterministically on a laptop (see README).
ThreadPool.SetMinThreads(4, 4);
ThreadPool.SetMaxThreads(4, 4);

var rates = new RateService();
var done = new ConcurrentQueue<int>();

// A burst of eight report requests arrives. Each grabs the rate and records it.
var reports = Enumerable.Range(1, 8)
    .Select(id => Task.Run(() =>
    {
        decimal rate = rates.GetUsdRate(); // 💥 blocks a pool thread on async work
        done.Enqueue(id);
    }))
    .ToArray();

// Three seconds is thirty times the 100 ms an FX call takes - not a race we are
// timing, but a ceiling on a hang: a deadlocked pool never makes progress, so
// the wait returns false and we do not sit here forever.
bool allDone = Task.WaitAll(reports, TimeSpan.FromSeconds(3));

Console.WriteLine($"Reports finished: {done.Count} of {reports.Length}.");

if (!allDone)
{
    throw new InvalidOperationException(
        $"thread pool deadlocked: {done.Count} of {reports.Length} reports done after 3s, zero CPU");
}

Console.WriteLine("Every report got its rate.");

sealed class RateService
{
    // The real FX call: an async round-trip, here a 100 ms network stand-in.
    public async Task<decimal> GetUsdRateAsync()
    {
        await Task.Delay(100); // the continuation after this needs a pool thread
        return 41.75m;
    }

    // The synchronous facade the reporting code calls. "We just need the value
    // here", so it blocks on the async call instead of awaiting it.
    public decimal GetUsdRate() => GetUsdRateAsync().GetAwaiter().GetResult();
}
