// Exhibit #0037: the fix

using System.Collections.Concurrent;

// The same startup warmup - but launched with Task.Run, which unwraps the async
// lambda's task, so awaiting it waits for the work to finish, not just to start.

var prices = new ConcurrentDictionary<string, decimal>();

// Fetch prices from the upstream feed, then fill the cache. The same one-second
// call as in Bad.cs.
async Task WarmCacheAsync()
{
    await Task.Delay(TimeSpan.FromSeconds(1)); // the price-feed round-trip
    prices["WIDGET"] = 9.99m;
    prices["GADGET"] = 19.99m;
}

// "Run the warmup and await it - once it returns, the cache is ready."
Task warmup = Task.Run(async () => await WarmCacheAsync()); // Task.Run unwraps: this awaits the real work
await warmup;

Console.WriteLine($"Cache warmed. {prices.Count} of 2 prices loaded. Serving traffic.");

if (prices.Count != 2)
{
    throw new InvalidOperationException(
        $"warmup awaited and returned, yet the cache holds {prices.Count} of 2 prices");
}

Console.WriteLine("Every price is in the cache. As it should be.");
